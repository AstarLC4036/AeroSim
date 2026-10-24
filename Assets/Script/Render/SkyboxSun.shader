// Standalone skybox with a single HDR sun disc. Assign a material using this shader to
// Lighting > Environment > Skybox Material (or RenderSettings.skybox) and it is ready to use.
//
// The sun follows the scene's main Directional Light, so there is no direction to type in by hand.
// Skybox meshes are boxes centred on the camera, which is why the object space position can be used
// directly as the view direction.
Shader "AeroSim/Skybox Sun"
{
    Properties
    {
        [HDR] _BackgroundColor ("Background (HDR)", Color) = (0, 0, 0, 1)
        _BackgroundBrightness ("Background Brightness", Float) = 1
        [HDR] _SunColor ("Sun Colour (HDR)", Color) = (1, 0.97, 0.92, 1)
        _SunIntensity ("Sun Intensity (HDR)", Float) = 100
        _SunSize ("Sun Size (degrees)", Range(0.1, 10)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // LerpWhiteTo lives in the core CommonMaterial.hlsl, and the light includes below pull in
            // Shadows.hlsl which uses it without defining it. Include it first.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            // Only the main light is needed, so the light-weight header is enough.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BackgroundColor;
                float  _BackgroundBrightness;
                float4 _SunColor;
                float  _SunIntensity;
                float  _SunSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir        : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.dir = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dir);

                // Direction towards the main light, i.e. towards the sun.
                float3 sunDir = GetMainLight().direction;

                // Disc edge in cosine space, antialiased with the screen space derivative so the
                // circle does not shimmer when the camera turns.
                float cosA = dot(dir, sunDir);
                float cosEdge = cos(radians(_SunSize * 0.5));
                float aa = max(fwidth(cosA) * 2.0, 1e-6);
                float disc = smoothstep(cosEdge - aa, cosEdge + aa, cosA);

                float3 color = _BackgroundColor.rgb * _BackgroundBrightness;
                color += _SunColor.rgb * _SunIntensity * disc;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
