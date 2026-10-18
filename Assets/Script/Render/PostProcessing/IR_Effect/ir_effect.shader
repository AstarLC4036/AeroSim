Shader "PostEffect/IR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Strength", int) = 1
        _BlackHot ("Block Hot", Range(0,1)) = 0
        _Contrast ("Contrast", Range(0.5,3)) = 1.2
        _NoiseAmount ("Noise", Range(0,1)) = 0.05
        _NoiseDelta ("Noise Delta", Range(0,100)) = 20
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

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            int _Strength;
            float _BlackHot;
            float _Contrast;
            float _NoiseAmount;
            float _NoiseDelta;
            //float4 _Time;
            //float4 _ZBufferParams;

            float GetLinearDepth(float2 uv)
            {
                float raw = tex2D(_CameraDepthTexture, uv).r;
                return 1 / (_ZBufferParams.x * raw + _ZBufferParams.y);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainColor;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;
                //o.vertex = TransformObjectToHClip(v.vertex);
                o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // float depth = tex2D(_CameraDepthTexture, i.uv).r;;
                // float3 depthCol = lerp(color, float3(1,1,1), clamp(1 - depth * _Strength, 0, 1));
                // //color.rgb = depthCol;

                // // float center = (float)_Strength / 1000;
                // // if(depth == 0)
                // // {
                // //     return color;
                // // }
                // // if(depth > center - 0.0001 && depth < center + 0.0001)
                // // {
                // //     return float4(1,0,0,1);
                // // }
                // // if(depth > center)
                // // {
                // //     float grayscale = color.r * 0.299 + color.g * 0.587 + color.b * 0.114;
                // //     float4 newCol = float4(grayscale, 0, 0, 1);
                // //     return newCol;
                // // }
                // color.rgb = depthCol;
                // float grayscale = color.r * 0.299 + color.g * 0.587 + color.b * 0.114;
                // //float4 newCol;
                // float4 newCol = float4(lerp(color.rgb, grayscale, _Strength / 100.0), 1);
                // return newCol;

                //TODO: make better IR
                float4 color = tex2D(_MainTex, i.uv);
                float brightness = dot(color.rgb, float3(0.299, 0.587, 0.114));

                // 白热：亮度越高越白
                float ir = saturate((brightness - 0.3) * _Contrast);  // 对比度拉伸
                ir = pow(ir, 1.5);  // 非线性增强

                // 黑热：反相
                if (_BlackHot > 0.5)
                    ir = 1.0 - ir;

                // 添加噪点
                float noise = frac(sin(dot(i.uv * 100, float2(12.9898, 78.233))) * 43758.5453 + _Time.y * _NoiseDelta);
                ir += (noise - 0.5) * _NoiseAmount;
                ir = saturate(ir);

                return float4(ir, ir, ir, 1.0);
            }

            ENDHLSL

            /*
            // CGPROGRAM
            // #pragma vertex vert
            // #pragma fragment frag

            // #include "UnityCG.cginc"

            // struct appdata
            // {
            //     float4 vertex : POSITION;
            //     float2 uv : TEXCOORD0;
            // };

            // struct v2f
            // {
            //     float2 uv : TEXCOORD0;
            //     float4 vertex : SV_POSITION;
            // };

            // v2f vert (appdata v)
            // {
            //     v2f o;
            //     o.vertex = UnityObjectToClipPos(v.vertex);
            //     o.uv = v.uv;
            //     return o;
            // }

            // sampler2D _MainTex;
            // int _Strength;

            // CBUFFER_START(UnityPerMaterial)
            // float4 _MainTex_TexelSize;
            // CBUFFER_END

            // fixed4 frag (v2f i) : SV_Target
            // {
            //     fixed4 col = tex2D(_MainTex, i.uv);

            //     // 反色
            //     // col.rgb = 1 - col.rgb;

            //     // RGB分离
            //     // float2 texelSize = 1 / _ScreenParams.xy;
            //     // float2 uvLeft = i.uv - float2(texelSize.x, 0) * _Strength;
            //     // float2 uvRight = i.uv + float2(texelSize.x, 0) * _Strength;

            //     // fixed4 colLeft = tex2D(_MainTex, uvLeft);
            //     // fixed4 colRight = tex2D(_MainTex, uvRight);

            //     // fixed4 newCol = fixed4(colLeft.r, col.g, colRight.b, 1);

            //     //blackwhite
            //     // float grayscale = col.r * 0.299 + col.g * 0.587 + col.b * 0.114;
            //     // fixed4 newCol;
            //     // newCol.rgb = grayscale;
            //     // newCol.a = 1;

            //     return col;
            // }
            // ENDCG
            */
        }
    }
}
