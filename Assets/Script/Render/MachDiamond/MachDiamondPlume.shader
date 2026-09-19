// ============================================================================
//  MachDiamondPlume.shader  --  AeroSim / afterburner Mach-diamond plume
// ----------------------------------------------------------------------------
//  3D (axisymmetric volume) port of the Shadertoy "Mach Diamonds!" field
//  (image shader: vesica-lens shock shell + internal diamond carve;
//   common shader: iq's vesica SDF, ikuto's 1D perlin, smin, fade).
//
//  How it works
//    * The material is applied to a small closed cylinder mesh that acts only
//      as a container. The shading volume is analytic: a cylinder of radius
//      _ObjScale.x and length _ObjScale.z, both in the plume object's LOCAL
//      metric space, axis = local +Z (exhaust direction).
//    * Both container faces are submitted (Cull Off); the fragment shader keeps
//      only the fragment sitting at the analytic exit of the volume, so the ray
//      is integrated exactly once regardless of the container's winding, and it
//      still works when the camera is inside the plume or looks down its axis.
//    * The ray origin is the pixel's near-plane point (perspective and
//      orthographic alike), direction towards the fragment.
//    * The fragment ray is intersected with that analytic cylinder, then
//      marched with an emission/absorption integral. Every sample point is
//      mapped from cylindrical coordinates (z, r) to the original 2D pattern
//      coordinates:   px = frac((z + phase)/0.8)*0.8 - 0.4   ,  py = r
//      The original field is symmetric in y (its vesica SDF starts with
//      p = abs(p)), so the radial distance can be used directly: no mirroring,
//      no extra cost, and the shock shells become surfaces of revolution.
//    * Everything else (shell SDF, diamond carve, inside/outside two-tone tint,
//      downstream tint shift, perlin turbulence warp, axis wobble) is the
//      reference maths, evaluated per sample and integrated along the ray.
//
//  Units: _CellLength is metres per shock cell (the reference cell is 0.8
//  pattern units wide, so 1 pattern unit = _CellLength/0.8 metres).
//  Integration is done in pattern units, so changing _CellLength rescales the
//  pattern without changing the plume brightness.
// ============================================================================
Shader "AeroSim/MachDiamond/Plume"
{
    Properties
    {
        [Header(Volume)]
        _ObjScale ("Radius XY / Length Z  (m, local)", Vector) = (0.45, 0.45, 6.0, 1.0)
        _CellLength ("Shock Cell Length (m)", Float) = 1.0
        _StartPhase ("Cell Phase At Nozzle (0.4 = cell centre)", Range(0.0, 0.8)) = 0.4

        [Header(Density)]
        _Density ("Density", Float) = 1.0
        _Intensity ("Emission Intensity (HDR)", Float) = 1.6
        _Absorption ("Self Absorption", Range(0.0, 2.0)) = 0.9
        _FillCut ("Fill Cut (thins the solid core)", Range(0.0, 1.0)) = 0.55
        _MaxEmission ("Max Emission Clamp (HDR)", Float) = 8.0
        _SoftKnee ("Highlight Soft Knee (0 = off)", Range(0.0, 4.0)) = 1.2

        [Header(Axial Envelope)]
        _AxialDecay ("Downstream Dimming", Range(0.0, 1.0)) = 0.55
        _TipStart ("Tip Fade Start", Range(0.2, 1.0)) = 0.62
        _TipPower ("Tip Fade Power", Range(0.5, 5.0)) = 1.6
        _ExitFade ("Nozzle Fade In", Range(0.0, 0.2)) = 0.02

        [Header(Shock Structure)]
        _ShockGlow ("Shock Surface Glow", Range(0.0, 3.0)) = 1.0
        _ShockSharp ("Shock Surface Sharpness", Float) = 45.0
        _DiamondCarve ("Diamond Carve", Range(0.0, 1.5)) = 1.0

        [Header(Tint)]
        [HDR] _TintInside ("Inside Tint", Color) = (1.05, 1.00, 1.40, 1.0)
        [HDR] _TintOutside ("Outside Tint", Color) = (1.40, 1.00, 1.80, 1.0)
        _AxialTint ("Downstream Tint Shift (rgb)", Vector) = (0.0, 0.2, 0.6, 0.0)

        [Header(Turbulence)]
        _Turbulence ("Turbulence Amount", Range(0.0, 3.0)) = 1.0
        _FlowSpeed ("Flow Speed", Float) = 1.0
        _NoiseSpan ("Noise Span Along Plume", Float) = 3.0
        _Wobble ("Axis Wobble", Range(0.0, 2.0)) = 0.5
        _Seed ("Seed", Float) = 0.0

        [Header(Raymarch)]
        _Steps ("Ray Steps", Range(8, 64)) = 28
        [Toggle(_SOFT_PARTICLES)] _SoftParticlesOn ("Soft Particles", Float) = 0
        _SoftFade ("Soft Fade Distance (m)", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "MachDiamondPlume"
            Tags { "LightMode" = "UniversalForward" }

            // additive, no depth write: order independent, cheap, bloom friendly
            Blend One One
            ZWrite Off
            ZTest LEqual
            // Both faces are submitted on purpose: the fragment shader keeps only
            // the fragment sitting at the analytic exit of the volume, so the ray
            // is integrated exactly once no matter how the container is wound
            // (a flipped side wall used to make the sides vanish entirely).
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MachVert
            #pragma fragment MachFrag
            #pragma shader_feature_local _SOFT_PARTICLES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_SOFT_PARTICLES)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            #define MACH_REF_CELL   0.8     // reference pattern cell width (units)
            #define MACH_MAX_STEPS  64u

            CBUFFER_START(UnityPerMaterial)
                float4 _ObjScale;
                float  _CellLength, _StartPhase;
                float  _Density, _Intensity, _Absorption, _MaxEmission;
                float  _FillCut, _SoftKnee;
                float  _AxialDecay, _TipStart, _TipPower, _ExitFade;
                float  _ShockGlow, _ShockSharp, _DiamondCarve;
                float4 _TintInside, _TintOutside, _AxialTint;
                float  _Turbulence, _FlowSpeed, _NoiseSpan, _Wobble, _Seed;
                float  _Steps, _SoftParticlesOn, _SoftFade;
            CBUFFER_END

            // ------------------------------------------------------------------
            // reference maths (ported 1:1 from the Shadertoy common tab)
            // ------------------------------------------------------------------
            float MachHash(float p)
            {
                float3 p3 = frac(float3(p, p, p) * 0.1031);
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.x + p3.y) * p3.z);
            }

            float MachFade(float t) { return t * t * t * (t * (6.0 * t - 15.0) + 10.0); }

            float MachGrad(float h, float p)
            {
                int i = int(1e4 * h);
                return ((i & 1) == 0) ? p : -p;
            }

            // 1D perlin, ikuto (shadertoy lt3BWM)
            float MachPerlin(float p)
            {
                float pi = floor(p);
                float pf = p - pi;
                float w = MachFade(pf);
                return lerp(MachGrad(MachHash(pi), pf),
                            MachGrad(MachHash(pi + 1.0), pf - 1.0), w) * 2.0;
            }

            // 2D vesica piscis SDF, iq (shadertoy XtVfRW) -- note p = abs(p)
            float MachVesica(float2 p, float r, float dd)
            {
                p = abs(p);
                float b = sqrt(r * r - dd * dd);
                return ((p.x - b) * dd > p.y * b)
                    ? length(p - float2(b, 0.0)) * sign(dd)
                    : length(p - float2(0.0, -dd)) - r;
            }

            float MachSMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            // chain of lens shells = the exhaust body
            float MachShell(float px, float py)
            {
                float m = MachVesica(float2(px, py), 1.0, 0.8);
                float n = min(MachVesica(float2(px + MACH_REF_CELL, py), 1.0, 0.8),
                              MachVesica(float2(px - MACH_REF_CELL, py), 1.0, 0.8));
                return MachSMin(m, n, 0.15);
            }

            float MachHash2(float2 v)
            {
                return frac(52.9829189 * frac(dot(v, float2(0.06711056, 0.00583715))));
            }

            // keeps mid tones linear and rolls the hot core off instead of
            // letting it clip into a flat white blob (knee is in HDR units)
            float3 MachSoftClip(float3 c, float knee)
            {
                float k = max(knee, 1e-4);
                float3 over = max(c - k, 0.0);
                return min(c, k) + k * (1.0 - exp(-over / k));
            }

            // ------------------------------------------------------------------
            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;   // container-local, pre-scale
                float4 screenPos  : TEXCOORD1;
            };

            Varyings MachVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;

                // screen position the way ComputeScreenPos does it (w = eye depth)
                float4 sp = OUT.positionCS * 0.5;
                sp.xy = float2(sp.x, sp.y * _ProjectionParams.x) + sp.w;
                sp.zw = OUT.positionCS.zw;
                OUT.screenPos = sp;
                return OUT;
            }

            // ------------------------------------------------------------------
            float4 MachFrag(Varyings IN) : SV_Target
            {
                // ---- metric volume (the container mesh is a unit cylinder) ----
                float3 scale   = max(abs(_ObjScale.xyz), 1e-4);
                float  lengthM = max(scale.z, 1e-3);
                float  radiusM = max(scale.x, 1e-4);
                float  unit    = max(_CellLength / MACH_REF_CELL, 1e-5); // m per pattern unit

                // ---- ray in the plume's metric-local space --------------------
                // Origin = this pixel's NEAR PLANE point, direction = towards the
                // fragment. One formulation covers perspective and orthographic
                // cameras, and every fragment gets a distance along the ray so the
                // entry fragment can be rejected further down.
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
                float3 originWS = ComputeWorldSpacePosition(screenUV, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                float3 originM  = TransformWorldToObject(originWS) * scale;
                float3 fragM    = IN.positionOS * scale;

                float3 dirM     = fragM - originM;
                float  fragDist = length(dirM);
                dirM = (fragDist > 1e-6) ? dirM / fragDist : float3(0.0, 0.0, 1.0);

                // ---- analytic cylinder intersection --------------------------
                float2 oc = originM.xy;
                float2 dc = dirM.xy;
                float a = dot(dc, dc);
                float b = dot(oc, dc);
                float c = dot(oc, oc) - radiusM * radiusM;

                float tNear, tFar;
                if (a < 1e-8)
                {
                    // ray parallel to the plume axis
                    if (abs(originM.x) > radiusM || abs(originM.y) > radiusM) discard;
                    tNear = -1e30;
                    tFar  =  1e30;
                }
                else
                {
                    float disc = b * b - a * c;
                    if (disc < 0.0) discard;
                    float sq = sqrt(disc);
                    tNear = (-b - sq) / a;
                    tFar  = (-b + sq) / a;
                }

                // axial slab 0..length
                if (abs(dirM.z) < 1e-6)
                {
                    if (originM.z < 0.0 || originM.z > lengthM) discard;
                }
                else
                {
                    float t0 = (0.0     - originM.z) / dirM.z;
                    float t1 = (lengthM - originM.z) / dirM.z;
                    tNear = max(tNear, min(t0, t1));
                    tFar  = min(tFar,  max(t0, t1));
                }

                // Two fragments reach this shader (Cull Off). Keep the one at the
                // analytic exit: it is the farthest along the ray and therefore the
                // one whose distance matches tFar. Winding independent.
                if (abs(fragDist - tFar) > abs(fragDist - tNear)) discard;

                tFar  = min(tFar, fragDist);
                tNear = max(tNear, 0.0);
                if (tFar <= tNear) discard;

                // ---- march ---------------------------------------------------
                float steps = clamp(_Steps, 8.0, (float)MACH_MAX_STEPS);
                float dt    = (tFar - tNear) / steps;
                float dtU   = dt / unit;                 // integrate in pattern units
                float jitter = MachHash2(IN.positionCS.xy);

                float3 acc = 0.0;
                float  T   = 1.0;

                float3 tintIn  = max(_TintInside.rgb,  0.0);
                float3 tintOut = max(_TintOutside.rgb, 0.0);
                bool   turb    = _Turbulence > 0.0005;

                [loop]
                for (uint i = 0u; i < MACH_MAX_STEPS; i++)
                {
                    if ((float)i >= steps) break;

                    float t = tNear + ((float)i + jitter) * dt;
                    float3 pM = originM + dirM * t;

                    float z = pM.z;
                    float r = length(pM.xy);
                    float zU = z / unit;
                    float rU = r / unit;
                    float axial01 = saturate(z / lengthM);

                    // --- turbulence warp (reference uv jitter) ---------------
                    if (turb)
                    {
                        float t3  = _Time.y * _FlowSpeed + _Seed;
                        float amp = _Turbulence * (0.5 + axial01);
                        float n0 = MachPerlin(t3 * 0.5   + axial01 * _NoiseSpan);
                        float n1 = MachPerlin(t3 * 7.0   + axial01 * _NoiseSpan);
                        float n2 = MachPerlin(t3 * 67.0  + axial01 * _NoiseSpan);
                        float n3 = MachPerlin(t3 * 101.0 + axial01 * _NoiseSpan);

                        float wx = (0.03 * n0 + 0.01 * n1) * amp;
                        float wy = (0.03 * n0 + 0.01 * n1 + 0.01 * n2 + 0.005 * n3) * amp;
                        // uv.x += .5*(.2-abs(uv.y))*perlin(t*3.)
                        wx += _Wobble * (0.2 - min(rU, 0.2)) * MachPerlin(t3 * 3.0);

                        zU += wx;
                        rU  = max(rU + wy, 0.0);
                    }

                    // --- reference pattern ----------------------------------
                    float cell = frac((zU + _StartPhase) / MACH_REF_CELL);
                    float px   = cell * MACH_REF_CELL - MACH_REF_CELL * 0.5;

                    float dA = MachVesica(float2(px - (MACH_REF_CELL - 0.4), rU), 0.3, 0.15);
                    float dB = max(MachVesica(float2(px + (MACH_REF_CELL - 0.4), rU), 0.85, 0.7), 0.0)
                             + 0.05 * (1.0 - 2.0 * abs(px - 0.1));
                    float diamonds = max(MachSMin(dA, dB, 0.1), 0.0);

                    float exhaust = MachShell(px, rU);
                    float outside = (exhaust > 0.0) ? 1.0 : 0.0;
                    float inside  = 1.0 - outside;

                    float d   = abs(MachSMin(exhaust, -diamonds, -0.03));
                    float lum = 1.0 - d * (2.0 * inside + 5.0 * outside / (axial01 + 0.2));
                    lum = saturate(lum);
                    lum = pow(lum, 0.9 * inside + 1.5 * outside);

                    float shock = exp(-(d * d) * _ShockSharp);

                    // --- axial envelope -------------------------------------
                    float tip = 1.0 - pow(saturate((axial01 - _TipStart) / max(1.0 - _TipStart, 1e-4)), _TipPower);
                    float env = (1.0 - _AxialDecay * axial01) * tip;
                    env *= smoothstep(0.0, max(_ExitFade, 1e-4), axial01);

                    // The reference law saturates to a solid fill wherever the
                    // diamond term vanishes; integrated along a ray that reads as
                    // one featureless glowing tube. _FillCut removes part of that
                    // fill so the shock shells / diamonds carry the image, and
                    // _ShockGlow lights the shock fronts themselves.
                    float body = max(lum - _FillCut, 0.0);
                    float dens = max(body - diamonds * _DiamondCarve, 0.0) + _ShockGlow * shock;
                    dens *= env;
                    if (dens <= 0.0) continue;

                    float3 tint = lerp(tintIn, tintOut, outside) - axial01 * _AxialTint.rgb;
                    tint = max(tint, 0.0);

                    acc += T * dens * tint * _Density * dtU;
                    T   *= exp(-dens * _Density * _Absorption * dtU);
                    if (T < 0.004) break;
                }

                float3 col = acc * _Intensity;
                if (_SoftKnee > 0.0) col = MachSoftClip(col, _SoftKnee);
                col = min(col, _MaxEmission);

                #if defined(_SOFT_PARTICLES)
                    float2 suv = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
                    float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float partZ  = IN.screenPos.w;
                    col *= saturate((sceneZ - partZ) / max(_SoftFade, 1e-4));
                #endif

                // additive: alpha is ignored, HDR values feed bloom
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
