Shader "AeroSim/PostEffect/AtomsohereScattering"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WorldOffset ("World Offset", Vector) = (0,0,0,0)
        _Center ("Center", Vector) = (0,0,0,0)
        _Radius ("Radius", float) = 1
        _Height ("Height", float) = 1
        _Exposure ("Exposure", float) = 1
        _ViewSteps ("View Steps", int) = 8
        _LightSteps ("Light Steps", int) = 2
        _RayleighHeight ("Rayleigh Height", float) = 8
        _RayleighScatteringCoeff ("Rayleigh Scattering Coeff", Vector) = (0.0055, 0.013, 0.0224)
        _MieHeight ("Mie Height", float) = 12
        _MieScatteringCoeff ("Mie Scattering Coeff", float) = 0.021
        _MieG ("Mie Anisotropic Param", float) = 0.76
         _OzoneHeight ("Ozone Height (km)", float) = 25
        _OzoneWidth ("Ozone Width (km)", float) = 15
        _OzoneCoeff ("Ozone Coeff", Vector) = (0.00065, 0.00188, 0.000085, 0)
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../../Shaders/Shader Common/AtmosphereCommon.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };
            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainColor;
            sampler2D _MainTex;
            float4 _WorldOffset;
            float4 _Center;
            float4 _RayleighScatteringCoeff;
            float4 _OzoneCoeff;
            float _MieScatteringCoeff;
            int _ViewSteps;
            int _LightSteps;
            float _Radius;
            float _Height;
            float _RayleighHeight;
            float _MieHeight;
            float _OzoneHeight;
            float _OzoneWidth;
            float _MieG;
            float _Exposure;
            CBUFFER_END

            float sceneRayDistance(float2 uv, float3 rayDir, float3 viewDir, float3 cameraPos)
            {
                float raw = SampleSceneDepth(uv);
                
                #if UNITY_REVERSED_Z
                    bool isSky = raw <= 1e-12;
                #else
                    bool isSky = raw >= 1 - 1e-12;
                #endif

                if(isSky)
                {
                    return 1e20;
                }
                
                float3 scenePos = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float dst =  distance(scenePos, cameraPos);

                return dst;
                //float linearZDepth = LinearEyeDepth(raw, _ZBufferParams);
                //float cosAngle = max(dot(rayDir, viewDir), 1e-4);
    
                //return linearZDepth / cosAngle;
            }

            float3 atmosphereScattering(float3 rayDir, float2 rayResult, float3 cameraPos, float radiusNorm, float atmRadiusNorm, out float3 transmittanceOut)
            {
                Light mainLight = GetMainLight();
                float3 sunDir = mainLight.direction;

                // normalize parameters prevent the floating-point precision issues
                float rayleighHeightNorm = _RayleighHeight / _Radius;
                float mieHeightNorm = _MieHeight / _Radius;
                float ozoneHeightNorm = _OzoneHeight / _Radius;
                float ozoneWidthNorm = _OzoneWidth / _Radius;

                float3 rayleighScatteringCoeffNorm = _RayleighScatteringCoeff.xyz * _Radius;
                float mieScatteringCoeffNorm = _MieScatteringCoeff * _Radius;
                float3 ozoneCoeffNorm = _OzoneCoeff.xyz * _Radius;

                float3 totalScattering = 0;
                float3 opticalDepthCam = 0;

                // nonlinear sampling for Mie Scattering
                float prevT = 0.0;
                float rayHeight = distance(cameraPos, float3(0,0,0)) - radiusNorm;
                float exponent = 1.0 + saturate(1.0 - rayHeight / (atmRadiusNorm - radiusNorm)) * 8.0;

                for(int i = 0; i < _ViewSteps; i++)
                {
                    // stepsize
                    float t  = pow((float)i / _ViewSteps, exponent) * rayResult.y;
                    float ds = t - prevT; // nonlinear stepsize

                    // camera transmittance
                    //float3 samplePoint = cameraPos + (viewStepsize * i + rayResult.x) * rayDir;
                    float3 samplePoint = cameraPos + (rayResult.x + t) * rayDir;
                    float height = distance(samplePoint, float3(0,0,0)) - radiusNorm;

                    // calcucate density
                    float rayleighDensity = heightDensityDecay(height, rayleighHeightNorm);
                    float mieDensity = heightDensityDecay(height, mieHeightNorm);
                    float ozoneDensity = ozoneDensityDecay(height, ozoneHeightNorm, ozoneWidthNorm);

                    // final optical depth
                    opticalDepthCam += (rayleighScatteringCoeffNorm.xyz * rayleighDensity + mieScatteringCoeffNorm * mieDensity + ozoneCoeffNorm.xyz * ozoneDensity) * ds;
                    float3 transmittanceView = exp(-opticalDepthCam);

                    // light sphere intersection
                    float2 lightRaySphereResult = raySphere(float3(0,0,0), atmRadiusNorm, samplePoint, sunDir);
                    float lightStepsize = lightRaySphereResult.y / _LightSteps;

                    // light transmittance
                    float3 opticalDepthLight = 0;
                    if(lightRaySphereResult.y > 0) // space view, ignore the light transmittance calcucation
                    {
                        for(int j = 0; j < _LightSteps; j++)
                        {
                            // sampling light
                            float3 lightSamplePoint = samplePoint + (lightStepsize * (j + 0.5)+ lightRaySphereResult.x) * sunDir; // + 0.5 -> midpoint sampling
                            float lightPointHeight = distance(lightSamplePoint, float3(0,0,0)) - radiusNorm;

                            // density
                            float rayleighDensityL = heightDensityDecay(lightPointHeight, rayleighHeightNorm);
                            float mieDensityL = heightDensityDecay(lightPointHeight, mieHeightNorm);
                            float ozoneDensityL = ozoneDensityDecay(height, ozoneHeightNorm, ozoneWidthNorm);

                            opticalDepthLight += (rayleighScatteringCoeffNorm.xyz * rayleighDensityL + mieScatteringCoeffNorm * mieDensityL + ozoneCoeffNorm * ozoneDensityL) * lightStepsize;
                        }
                    }
                    float3 transmittanceLight = exp(-opticalDepthLight); // final light transmittance

                    // full scattering
                    float theta = dot(rayDir, sunDir);
                    float3 scattering = rayleighScatteringCoeffNorm.xyz * rayleighDensity * rayleightPhase(theta) + mieScatteringCoeffNorm * mieDensity * miePhase(theta, _MieG);
                    totalScattering += transmittanceView * transmittanceLight * scattering * ds;

                    // update nonlinear stepsize
                    prevT = t;
                }

                transmittanceOut = exp(-opticalDepthCam);

                return totalScattering;
            }

            v2f vert(appdata v)
            {
                v2f o;
                //o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;

                // get view directions by project matrix
                float4 viewDir = mul(UNITY_MATRIX_I_VP, o.vertex);
                o.viewDir = viewDir.xyz / viewDir.w;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);

                //float2 ndc = i.uv * 2.0 - 1.0;
                //float4 clipPos = float4(ndc, 0.5, 1.0);
                //float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
                float3 cameraPos = GetCameraPositionWS();
                float3 cameraPosPlanet = cameraPos + _WorldOffset;
                float3 cameraRelativePos = cameraPosPlanet / 1000 - _Center.xyz; // Solving floating-point precision issues using a planetary coordinate system
                float3 cameraPosNorm = cameraRelativePos / _Radius;
                float radiusNorm = 1.0;
                float atmRadiusNorm = 1.0 + (_Height / _Radius);
                float3 rayDir = normalize(i.viewDir - cameraPos);
                //float3 rayDir = normalize(worldPos.xyz / worldPos.w - cameraPos);

                float2 raySphereResult = raySphere(float3(0,0,0), atmRadiusNorm, cameraPosNorm, rayDir);
                float2 rayGroundSphereResult = raySphere(float3(0,0,0), radiusNorm, cameraPosNorm, rayDir);

                if(raySphereResult.y > 0)
                {
                    // If the ray hits the ground, cut it off
                    if(rayGroundSphereResult.x > 0 && rayGroundSphereResult.y > 0)
                    {
                        raySphereResult.y = rayGroundSphereResult.x - raySphereResult.x;
                    }

                    // handling occlusion 
                    float3 cameraFwd = -UNITY_MATRIX_V[2].xyz;
                    float sceneDst = sceneRayDistance(i.uv, rayDir, cameraFwd, cameraPos);

                    // if it hits object
                    if(sceneDst < 1e10)
                    {
                        float sceneDstNorm = sceneDst / 1000 / _Radius;
                        float sceneLength = sceneDstNorm - raySphereResult.x;

                        if(sceneLength > 0 && sceneLength < raySphereResult.y)
                        {
                            raySphereResult.y = sceneLength;
                        }
                    }

                    float3 transmittance = 0;
                    float3 scattering = atmosphereScattering(rayDir, raySphereResult, cameraPosNorm, radiusNorm, atmRadiusNorm, transmittance);
                    //float4 resultCol = float4(compressScreenLuminance(color.rgb * transmittance + scattering * _Exposure), 1);
                    float4 resultCol = float4(color.rgb * transmittance + scattering * _Exposure, 1);
                    return resultCol;
                }
                else
                {
                    return color;
                }
            }

            ENDHLSL
        }
    }
}
