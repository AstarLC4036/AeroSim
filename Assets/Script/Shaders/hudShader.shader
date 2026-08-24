Shader "Custom/HollowShapes"
{
    Properties
    {
        _ShapeType ("Shape Type", Float) = 0   // 0=空心圆, 1=空心矩形
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0.3          // 圆半径（UV空间）
        _RectSize ("Rect Size", Vector) = (0.6, 0.4, 0, 0) // 矩形宽高（UV空间）
        _Thickness ("Thickness", Float) = 0.02   // 边框厚度（UV空间）
        _Softness ("Softness", Float) = 0.005    // 边缘柔化
        _Color ("Color", Color) = (0, 1, 0, 0.8)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _ShapeType;
            float2 _Center;
            float _Radius;
            float2 _RectSize;
            float _Thickness;
            float _Softness;
            float4 _Color;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 空心圆
            float sdRing(float2 p, float2 center, float radius, float thickness)
            {
                return abs(length(p - center) - radius) - thickness * 0.5;
            }

            // 空心矩形
            float sdRectOutline(float2 p, float2 center, float2 halfSize, float thickness)
            {
                float2 d = abs(p - center) - halfSize;
                return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - thickness * 0.5;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv;
                float dist;

                float alpha;
                if (_ShapeType < 0.5f)
                {
                    dist = sdRing(p, _Center, _Radius, _Thickness);
                    alpha = 1.0 - smoothstep(0.0, _Softness, dist);
                }
                else
                {
                    dist = sdRectOutline(p, _Center, _RectSize * 0.5, -_Thickness);
                    alpha = 1.0 - smoothstep(0.0, -_Softness, dist);
                }

                half4 color = _Color;
                color.a *= alpha;
                return color;
            }
            ENDHLSL
        }
    }
}