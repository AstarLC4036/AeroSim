// Farmland terrain for the flat plains: the ground is almost featureless geometry, so the whole look
// comes from a procedural patchwork of fields in world space. No textures are sampled.
//
// Colour rules, in order: one crop per cell -> field rows -> hedging on the cell borders -> centre pivot
// irrigation circles -> roads -> fine detail. Every lookup uses the real world position (see
// _WorldOffset) so the pattern stays put when the floating origin shifts.
Shader "AeroSim/Terrain Farmland"
{
    Properties
    {
        // ---- world offset ----
        // Real world position = vertex world position + this. Carries the FloatingOrigin shift (all three
        // axes, so the field grid does not slide) and the DEM's vertical shift. TerrainGenerator writes it.
        _WorldOffset ("World Offset (xyz)", Vector) = (0, 0, 0, 0)

        // ---- field grid ----
        _FieldSize ("Field Size (m)", Float) = 800
        _GridRotation ("Grid Rotation (deg)", Range(0, 90)) = 8
        _RowScale ("Crop Row Scale (1/m)", Float) = 0.06
        _RowStrength ("Crop Row Strength", Range(0, 1)) = 0.12

        // ---- hedges, roads, pivots ----
        // Widths are in metres, converted inside the shader, so the borders stay a few metres wide
        // instead of turning into a soft gradient across a quarter of the field.
        _HedgeWidth ("Hedge Width (m)", Range(0, 40)) = 4
        [HDR] _HedgeColor ("Hedge / Ditch Colour", Color) = (0.12, 0.14, 0.09, 1)
        _RoadEvery ("One Road Every N Fields", Float) = 8
        _RoadWidth ("Road Width (m)", Range(0, 40)) = 6
        [HDR] _RoadColor ("Road Colour", Color) = (0.24, 0.23, 0.21, 1)
        _PivotChance ("Pivot Field Chance", Range(0, 1)) = 0.12
        [HDR] _PivotColor ("Pivot Crop Colour", Color) = (0.30, 0.38, 0.18, 1)

        // ---- crops ----
        // Real farmland seen from the air is mostly one family of tans and greens per area, not a full
        // rainbow. Keep these close together and let Fields Per Block group the fields.
        _BlockFields ("Fields Per Block", Float) = 8
        [HDR] _CropGreen ("Crop: Green", Color) = (0.24, 0.29, 0.16, 1)
        [HDR] _CropStraw ("Crop: Straw", Color) = (0.42, 0.38, 0.24, 1)
        [HDR] _CropBrown ("Crop: Ploughed", Color) = (0.28, 0.25, 0.20, 1)

        // ---- detail ----
        _DetailScale ("Detail Noise Scale (1/m)", Float) = 0.01
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.08

        // ---- material ----
        _Ambient ("Ambient", Range(0, 1)) = 0.25
        _Specular ("Specular", Range(0, 1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // LerpWhiteTo comes from the core CommonMaterial.hlsl, which neither Core.hlsl nor Shadows.hlsl
        // pulls in. URP's own Lit shader gets it through its input includes; include it explicitly so the
        // shadow caster below compiles on its own.
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _WorldOffset;
            float  _FieldSize;
            float  _GridRotation;
            float  _RowScale;
            float  _RowStrength;
            float  _HedgeWidth;
            float4 _HedgeColor;
            float  _RoadEvery;
            float  _RoadWidth;
            float4 _RoadColor;
            float  _PivotChance;
            float4 _PivotColor;
            float  _BlockFields;
            float4 _CropGreen;
            float4 _CropStraw;
            float4 _CropBrown;
            float  _DetailScale;
            float  _DetailStrength;
            float  _Ambient;
            float  _Specular;
        CBUFFER_END

        // Deterministic per cell hash: same cell always gets the same crop, so fields never shimmer when
        // the camera moves.
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

        float3 FarmlandAlbedo(float3 positionWS)
        {
            // Real world space: vertex positions are shifted by FloatingOrigin and by the DEM's vertical
            // offset, so the field grid is only stable after adding _WorldOffset.
            float3 worldPosition = positionWS + _WorldOffset.xyz;

            // Survey grid, rotated a little. Real farmland rarely lines up with the world axes, and the
            // rotation alone does a lot to make the patchwork read as man made rather than procedural.
            float2 p = worldPosition.xz;
            float s = sin(radians(_GridRotation));
            float c = cos(radians(_GridRotation));
            float2 rotated = float2(p.x * c - p.y * s, p.x * s + p.y * c);

            float2 cellUV = rotated / max(_FieldSize, 1.0);
            float2 cell = floor(cellUV);
            float2 f = frac(cellUV);

            // Crop family per block of fields. Real farmland is divided by roads into sections, and the
            // fields inside one section tend to be similar; without this grouping the pattern reads as a
            // colour checkerboard instead of fields.
            float2 block = floor(cell / max(_BlockFields, 1.0));
            float blockPick = Hash21(block + 3.7);
            float3 albedo = lerp(_CropGreen.rgb, _CropStraw.rgb, saturate(blockPick * 1.6 - 0.2));

            // Inside a block each field differs only a little: a fallow field here, a slightly different
            // green there.
            float r = Hash21(cell);
            albedo = lerp(albedo, _CropBrown.rgb, step(0.80, Hash21(cell + 7.3)) * 0.55);
            albedo *= 1.0 + (r - 0.5) * 0.16;

            // Rows: each field is ploughed along one axis, chosen per cell, so the lines do not all line
            // up. The noise is stretched along that axis, which is what makes it read as rows.
            float alongX = step(0.5, Hash21(cell + 41.9));
            float2 rowUV = alongX > 0.5
                ? float2(rotated.x * _RowScale, rotated.y * _RowScale * 0.12)
                : float2(rotated.x * _RowScale * 0.12, rotated.y * _RowScale);
            albedo *= 1.0 + (ValueNoise(rowUV) - 0.5) * _RowStrength;

            // Hedges and ditches: a dark band a few metres wide along every field border.
            float edge = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
            float hedge = max(_HedgeWidth, 0.0) / max(_FieldSize, 1.0);
            albedo = lerp(_HedgeColor.rgb, albedo, smoothstep(0.0, max(hedge, 1e-6), edge));

            // Centre pivot irrigation: the big circles of the high plains.
            float isPivot = step(1.0 - _PivotChance, Hash21(cell + 19.1));
            float circle = 1.0 - smoothstep(0.38, 0.42, length(f - 0.5) * 2.0);
            albedo = lerp(albedo, _PivotColor.rgb, isPivot * circle);

            // Roads: one straight road every N fields, on both axes. The width is measured against the
            // road spacing rather than a single field, so 6 m stays 6 m.
            float2 roadUV = frac(cellUV / max(_RoadEvery, 1.0));
            float2 roadDist = min(roadUV, 1.0 - roadUV);
            float roadFrac = max(_RoadWidth, 0.0) / (max(_RoadEvery, 1.0) * max(_FieldSize, 1.0));
            float road = 1.0 - smoothstep(0.0, max(roadFrac, 1e-6), min(roadDist.x, roadDist.y));
            albedo = lerp(albedo, _RoadColor.rgb, road);

            // Fine detail, so a large field is not one flat colour under the sun.
            float detail = ValueNoise(rotated * _DetailScale);
            albedo *= 1.0 + (detail - 0.5) * 2.0 * _DetailStrength;

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
                float3 albedo = FarmlandAlbedo(input.positionWS);

                Light mainLight = GetMainLight(input.shadowCoord);

                float3 lighting = albedo * (mainLight.color * (saturate(dot(normalWS, mainLight.direction)) *
                                                                mainLight.shadowAttenuation) + _Ambient);

                // A little sky coloured ambient so shaded faces do not go flat black.
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
        // material without one is skipped silently.
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
        // When anything in the pipeline asks for normals the camera runs a DepthNormals prepass instead of
        // the plain depth prepass, and that prepass is what fills _CameraDepthTexture.
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
