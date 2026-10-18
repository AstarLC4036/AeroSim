// Height and slope driven terrain colouring, for the chunk meshes built by TerrainGenerator.
//
// There is no texture anywhere: the layers are mixed from world space altitude and slope, which is
// exactly what an untextured DEM needs first. Swap any of the colour properties for a texture once the
// bands sit where you want them.
//
// Altitude is taken from the vertex world position plus _WorldOffset, which TerrainGenerator keeps up
// to date with the FloatingOrigin shift and the DEM's vertical shift. _WorldOffset covers all three
// axes, because the snow line noise is sampled in world space and would otherwise slide when the
// origin moves.
//
// Slope uses 1 - N.y, so 0 is flat ground and 1 is a vertical wall:
//   river bed / gravel : low altitude, flat
//   grass and meadow   : mid altitude, flat to moderate
//   bare rock          : any altitude, steep
//   snow               : high altitude and not too steep, so cliffs stay bare

Shader "AeroSim/Terrain Splat"
{
    Properties
    {
        // ---- world offset ----
        // Real world position = vertex world position + this. It carries the FloatingOrigin shift (all
        // three axes, so the snow line noise does not slide) and the DEM's vertical shift. TerrainGenerator
        // writes it, no need to set it by hand.
        _WorldOffset ("World Offset (xyz)", Vector) = (0, 0, 0, 0)
        _RiverLevel ("River / Valley Level (m)", Float) = 3200
        _MeadowLevel ("Alpine Meadow Level (m)", Float) = 4200
        _SnowLine ("Snow Line (m)", Float) = 5500
        _SnowLineJitter ("Snow Line Jitter (m)", Range(0, 800)) = 250
        _SnowBlend ("Snow Transition (m)", Range(10, 1500)) = 300
        _BandBlend ("Altitude Band Blend (m)", Range(10, 1000)) = 250

        // ---- slope: 1 - N.y, so 0 is flat ground and 1 is a vertical wall ----
        _RockSlope ("Rock From Slope", Range(0.05, 1)) = 0.35
        _RockBlend ("Rock Slope Blend", Range(0.01, 0.6)) = 0.18
        _SnowSlope ("Snow Stops At Slope", Range(0.05, 1)) = 0.45
        _SnowSlopeBlend ("Snow Slope Blend", Range(0.01, 0.6)) = 0.15

        // ---- variety ----
        // Procedural only, no textures. Two goals: the altitude bands should not sit at exactly the same
        // height everywhere, and a large area of one band should not read as a flat colour field.
        _RegionNoiseScale ("Region Noise Scale (1/m)", Float) = 0.00035
        _RegionVariation ("Region Variation (m)", Range(0, 900)) = 260
        _ColorVariation ("Colour Variation", Range(0, 1)) = 0.18
        _DetailNoiseScale ("Detail Noise Scale (1/m)", Float) = 0.004
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.12

        // ---- colours ----
        [HDR] _RiverColor ("River Bed / Gravel", Color) = (0.30, 0.29, 0.26, 1)
        [HDR] _GrassColor ("Grass", Color) = (0.24, 0.33, 0.15, 1)
        [HDR] _MeadowColor ("Alpine Meadow", Color) = (0.35, 0.40, 0.20, 1)
        [HDR] _RockColor ("Bare Rock", Color) = (0.32, 0.30, 0.28, 1)
        [HDR] _SnowColor ("Snow", Color) = (0.92, 0.94, 0.98, 1)

        // ---- noise ----
        _NoiseScale ("Noise Scale (1/m)", Float) = 0.0006

        // ---- lighting ----
        _Ambient ("Ambient", Range(0, 1)) = 0.25
        _Specular ("Specular", Range(0, 1)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // LerpWhiteTo is defined in the core package's CommonMaterial.hlsl, and neither Core.hlsl nor
        // Shadows.hlsl pulls that file in. URP's own shadow caster pass chain therefore only compiles
        // when something earlier brought it in (Lit shader does, through its LitInput.hlsl). Including
        // it explicitly here keeps every pass in this shader self sufficient.
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

        // One CBUFFER so the SRP batcher can keep every terrain chunk in a single batch.
        CBUFFER_START(UnityPerMaterial)
            float4 _WorldOffset;
            float  _RiverLevel;
            float  _MeadowLevel;
            float  _SnowLine;
            float  _SnowLineJitter;
            float  _SnowBlend;
            float  _BandBlend;
            float  _RockSlope;
            float  _RockBlend;
            float  _SnowSlope;
            float  _SnowSlopeBlend;
            float4 _RiverColor;
            float4 _GrassColor;
            float4 _MeadowColor;
            float4 _RockColor;
            float4 _SnowColor;
            float  _NoiseScale;
            float  _Ambient;
            float  _Specular;

            float  _RegionNoiseScale;
            float  _RegionVariation;
            float  _ColorVariation;
            float  _DetailNoiseScale;
            float  _DetailStrength;
        CBUFFER_END

        // Cheap world space value noise, used to keep the band edges from looking drawn with a ruler.
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);

            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));

            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float3 TerrainAlbedo(float3 positionWS, float3 normalWS)
        {
            // Everything below works in real world space: vertex positions are shifted by FloatingOrigin
            // and by the DEM's vertical offset, so they are only usable after adding _WorldOffset.
            float3 worldPosition = positionWS + _WorldOffset.xyz;
            float altitude = worldPosition.y;
            float slope = 1.0 - saturate(normalWS.y);

            // ---- variety ---------------------------------------------------------------------
            // Two independent low frequency fields sampled in real world space: one lifts and drops the
            // altitude bands, the other tints the result. Both stay put when the origin shifts.
            float region = ValueNoise(worldPosition.xz * _RegionNoiseScale);
            float tint = ValueNoise(worldPosition.xz * _RegionNoiseScale * 2.7 + 31.7);
            float bandAltitude = altitude + (region - 0.5) * 2.0 * _RegionVariation;

            // Slope noise breaks up the rock and snow contours: a real mountainside is a patchwork, not
            // a clean threshold across the height field.
            slope = saturate(slope + (ValueNoise(worldPosition.xz * _DetailNoiseScale * 0.4) - 0.5) * 0.12);

            // ---- altitude bands: valley -> grass -> meadow -> snow --------------------------------
            // The snow line wobbles so it does not read as a horizontal cut across the mountain.
            float jitter = (ValueNoise(worldPosition.xz * _NoiseScale) - 0.5) * 2.0 * _SnowLineJitter;
            float snow = smoothstep(_SnowLine + jitter - _SnowBlend, _SnowLine + jitter, bandAltitude);
            float meadow = smoothstep(_MeadowLevel - _BandBlend, _MeadowLevel, bandAltitude);
            float valley = 1.0 - smoothstep(_RiverLevel - _BandBlend, _RiverLevel, bandAltitude);

            float3 albedo = _GrassColor.rgb;
            albedo = lerp(albedo, _MeadowColor.rgb, meadow);
            albedo = lerp(albedo, _RiverColor.rgb, valley);

            // Snow only where it can actually cling to the slope.
            float snowHolds = 1.0 - smoothstep(_SnowSlope - _SnowSlopeBlend, _SnowSlope + _SnowSlopeBlend, slope);
            albedo = lerp(albedo, _SnowColor.rgb, snow * snowHolds);

            // Bare rock wins on anything steep, at every altitude.
            float rock = smoothstep(_RockSlope - _RockBlend, _RockSlope + _RockBlend, slope);
            albedo = lerp(albedo, _RockColor.rgb, rock);

            // Per region tint: a small warm/cool bias plus a value shift. Kept subtle on purpose; at the
            // default 0.18 it only means neighbouring areas are not the exact same colour.
            float warm = (tint - 0.5) * 2.0;
            float3 tintMul = 1.0 + _ColorVariation * float3(0.30, 0.16, 0.10) * warm;
            float valueMul = 1.0 + _ColorVariation * (region - 0.5) * 0.8;
            albedo *= valueMul * tintMul;

            // Fine detail at roughly the scale of one small valley, so a sunlit slope is not a single
            // smooth gradient. Costs one extra noise lookup per pixel.
            float detail = ValueNoise(worldPosition.xz * _DetailNoiseScale);
            albedo *= 1.0 + (detail - 0.5) * 2.0 * _DetailStrength * 0.5;

            return albedo;
        }
        ENDHLSL

        // ------------------------------------------------------------------ forward lit pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.shadowCoord = GetShadowCoord(positions);
                output.fogCoord = ComputeFogFactor(positions.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 albedo = TerrainAlbedo(input.positionWS, normalWS);

                Light mainLight = GetMainLight(input.shadowCoord);

                float3 lighting = albedo * (mainLight.color * (saturate(dot(normalWS, mainLight.direction)) *
                                                                mainLight.shadowAttenuation) + _Ambient);

                // A small amount of sky coloured ambient, so shaded faces do not go flat black.
                float3 skyAmbient = SampleSH(normalWS) * albedo;
                lighting += skyAmbient * 0.5;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDir);
                lighting += mainLight.color * _Specular * pow(saturate(dot(normalWS, halfDir)), 32.0)
                            * mainLight.shadowAttenuation;

                lighting = MixFog(lighting, input.fogCoord);

                return half4(lighting, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ depth only
        // URP fills _CameraDepthTexture by drawing every object that has a "DepthOnly" pass, and a
        // material without one is skipped silently. This uses URP's own pass header, the same approach
        // as the shadow caster below: a hand rolled version was silently ignored by URP, so the terrain
        // never reached the depth texture.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ depth normals
        // When anything in the pipeline asks for normals (SSAO, decals, some post effects) the camera
        // runs a DepthNormals prepass instead of the plain depth prepass, and that prepass is what fills
        // _CameraDepthTexture. Only objects with this pass take part in it, so the terrain needs it.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ shadow caster
        // URP's own pass header, exactly like Lit.shader does it. A hand rolled pass that only includes
        // Shadows.hlsl fails to compile with "undeclared identifier 'LerpWhiteTo'", because that helper
        // comes in through the lighting include chain.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
