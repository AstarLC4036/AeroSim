using AeroSim.AeroPhysics;
using AeroSim.Audio;
using AeroSim.InputSystem;
using AeroSim.Utils;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    /// <summary>
    /// 油门卡位，与座舱面板刻度一一对应：停车 0 / 慢车 14 / 最大 68 / 小加力 80 / 全加力 100。
    /// </summary>
    public enum ThrottleDetent
    {
        Cutoff = 0,
        Idle = 1,
        Max = 2,
        MinAB = 3,
        FullAB = 4,
    }

    /// <summary>
    /// 带转速（N1）仿真的发动机模块。
    ///
    /// 三个状态量，别混：
    ///   throttleLever  油门杆位置（飞行员的手），卡位对应座舱面板刻度
    ///   rpm            N1 转速（发动机的响应），一阶滞后 + 变化率上限
    ///   abLevel        加力段（涡轮后补油，独立点火/熄灭滞后；开加力时转速基本不变）
    ///
    /// 推力 = (军用推力(rpm) + 加力推力(abLevel)) * σ^densityExponent * thrustVsMach(mach)
    /// σ = ρ/ρ0，所以高空推力会真实衰减（之前恒定推力会把 J-10C 送到 34 km / M5）。
    /// </summary>
    public class EngineModule : AircraftModule
    {
        [Header("推力 (N)")]
        [Tooltip("军用(最大)状态的海平面静推力。加力总推力 = maxThurst * (1 + abThrustRatio)")]
        public float maxThurst = 97000f;
        [Tooltip("加力额外推力占军用推力的比例（现代涡扇 0.4 ~ 0.6）")]
        public float abThrustRatio = 0.5f;
        [Tooltip("推力随马赫数的相对系数：冲压段略升，之后下降")]
        public AnimationCurve thrustVsMach = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.8f, 1.12f),
            new Keyframe(1.2f, 1.0f),
            new Keyframe(2f, 0.7f),
            new Keyframe(3f, 0.45f),
            new Keyframe(5f, 0.2f));
        [Tooltip("推力随密度比 σ 的指数（涡扇常用 0.7 ~ 1.0）")]
        [Range(0.4f, 1.2f)] public float densityExponent = 0.8f;

        [Header("转速仿真 (N1, 归一化 0~1)")]
        [Range(0.3f, 0.9f)] public float idleRpm = 0.65f;
        [Tooltip("慢车推力占军用推力的比例（真机约 5%）")]
        [Range(0f, 0.2f)] public float idleThrustFraction = 0.05f;
        [Tooltip("加速时间常数 (s)：慢车 -> 最大。大涡扇很慢，这是推油门「有分量」的来源")]
        public float spoolUpTau = 5f;
        [Tooltip("减速时间常数 (s)")]
        public float spoolDownTau = 6f;
        [Tooltip("启动（停车 -> 慢车）时间常数 (s)")]
        public float startTau = 10f;
        [Tooltip("加力点火/熄灭时间常数 (s)")]
        public float abSpoolTau = 1.5f;
        [Tooltip("转速变化率上限 (/s)，0 = 不限制")]
        public float rpmRateLimit = 0.15f;
        [Tooltip("慢车 N2")]
        [Range(0f, 1f)] public float idleRpm2 = 0.72f;   // 慢车 N2（比 N1 的 0.65 高）
        [Range(0.1f, 1f)] public float n2Response = 0.6f; // <1 → 低推力段 N2 涨得比 N1 快（核心机先起）

        [Header("油门杆卡位 (杆行程 0~1)")]
        [Range(0f, 0.5f)] public float idleLever = 0.14f;
        [Range(0.3f, 0.9f)] public float maxLever = 0.68f;
        [Range(0.5f, 1f)] public float minAbLever = 0.80f;
        [Tooltip("进加力阈值（迟滞上沿）")]
        [Range(0f, 1f)] public float abEnterLever = 0.79f;
        [Tooltip("退加力阈值（迟滞下沿），必须小于进加力阈值")]
        [Range(0f, 1f)] public float abExitLever = 0.74f;
        [Tooltip("小加力卡位一上来就有的加力台阶（真机最小加力推力会跳一截）")]
        [Range(0f, 1f)] public float minAbLevel = 0.3f;
        [Tooltip("按油门键时杆的移动速率 (/s)")]
        public float leverSlewRate = 0.7f;
        [Tooltip("进入 Play 时的初始杆位：0 = 停车起步（默认，正确行为）；测试时填 1 就直接满油门/加力")]
        [Range(0f, 1f)] public float initialLever = 0f;
        [Tooltip("低于该杆位视为停车（燃油切断）")]
        [Range(0f, 0.1f)] public float cutoffLever = 0.02f;


        [Header("状态 (只读)")]
        [SerializeField] private float throttleLever;
        [SerializeField] private float rpm;
        [SerializeField] private float abLevel;
        [SerializeField] private bool abEngaged;
        [Tooltip("当前实际推力 (N)，已含大气衰减")]
        public float thurst;
        public bool isEngineToggled = false;

        [Header("Audio")]
        public EngineAudio audioController;
        public float maxPitch;
        public float minPitch;
        public float maxStrength;
        public float minStrength;
        [Header("Effect")]
        public EffectController flameEffect;
        public Transform flameMainEffect;
        public float defaultMaxThurstScale = 0.25f;

        // ---------------------------------------------------------------- 只读接口

        /// <summary>油门杆位置 0~1（100% = 全加力卡位）。</summary>
        public float ThrottleLever => throttleLever;
        /// <summary>油门杆位置百分比，给 HUD/座舱仪表用。</summary>
        public float ThrottlePercent => throttleLever * 100f;
        /// <summary>N1 归一化转速 0~1。</summary>
        public float Rpm => rpm;
        /// <summary>N1 百分比。</summary>
        public float N1Percent => rpm * 100f;
        /// <summary>加力段 0~1（0 = 未开加力）。</summary>
        public float AbLevel => abLevel;
        /// <summary>是否"真的点着了"加力（阈值别设太小，否则衰减尾巴也会让 HUD 亮灯）。</summary>
        public bool IsAfterburner => abLevel > 0.05f;
        /// <summary>当前实际推力 (N)。</summary>
        public float Thrust => thurst;

        public float Rpm2
        {
            get
            {
                float n1n = Mathf.InverseLerp(idleRpm, 1f, rpm);                  // N1 归一化到 0~1
                return Mathf.Lerp(idleRpm2, 1f, Mathf.Pow(n1n, n2Response));      // → N2
            }
        }
        public float N2Percent => Rpm2 * 100f;

        public ThrottleDetent Detent
        {
            get
            {
                if (throttleLever <= cutoffLever) return ThrottleDetent.Cutoff;
                if (throttleLever < Mathf.Lerp(idleLever, maxLever, 0.5f)) return ThrottleDetent.Idle;
                if (!abEngaged) return ThrottleDetent.Max;
                if (throttleLever < Mathf.Lerp(minAbLever, 1f, 0.5f)) return ThrottleDetent.MinAB;
                return ThrottleDetent.FullAB;
            }
        }

        public string DetentName
        {
            get
            {
                switch (Detent)
                {
                    case ThrottleDetent.Cutoff: return "停车";
                    case ThrottleDetent.Idle: return "慢车";
                    case ThrottleDetent.Max: return "最大";
                    case ThrottleDetent.MinAB: return "小加力";
                    default: return "全加力";
                }
            }
        }

        /// <summary>真实高度（去掉浮动原点偏移），和 Aircraft.cs 用的是同一个口径。</summary>
        public float TrueAltitude
        {
            get
            {
                if (parentAircraft == null) return 0f;
                return parentAircraft.transform.position.y - FloatingOrigin.origin.y;
            }
        }

        // ---------------------------------------------------------------- 生命周期

        public override void Init(Aircraft aircraft)
        {
            base.Init(aircraft);
            isEngineToggled = true;

            // 初始杆位（默认 0 = 停车起步）。
            // 注意：thurst 现在是每帧算出来的，不再是序列化的残留值，
            // 所以以前 prefab 里存着的 141942.9 不会再让引擎"一进 Play 就满推力"。
            throttleLever = Mathf.Clamp01(initialLever);

            if (flameEffect != null)
                flameEffect.Play();
        }

        void Start()
        {
            if (audioController != null)
            {
                audioController.engine = this;
            }
            else
            {
                Debug.Log("This engine has no audio source.");
            }
        }

        private void OnValidate()
        {
            if (abExitLever > abEnterLever) abExitLever = abEnterLever - 0.01f;
            if (minAbLever < maxLever) minAbLever = maxLever + 0.01f;
            if (maxLever <= idleLever) maxLever = idleLever + 0.05f;
        }

        // 输入：只改「杆位」。杆是飞行员的手，响应留给转速
        void Update()
        {
            UpdateInput();
        }

        // 动力学：积分与施力都在物理步里，用 fixedDeltaTime，和帧率无关
        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            UpdateSpool(dt);
            UpdateThrust(dt);
        }

        void UpdateInput()
        {
            if (parentAircraft == null || !parentAircraft.isControlling)
                return;

            float dir = 0f;
            if (Input.GetKey(Keybindings.thurstUp)) dir += 1f;
            if (Input.GetKey(Keybindings.thurstDown)) dir -= 1f;

            if (dir != 0f)
                throttleLever = Mathf.Clamp(throttleLever + dir * leverSlewRate * Time.deltaTime, 0f, 1f);
        }

        // ---------------------------------------------------------------- 转速 / 加力

        void UpdateSpool(float dt)
        {
            bool running = isEngineToggled && throttleLever > cutoffLever;

            // 1) 转速目标：停车段 -> 0；启动段 -> 慢车；正常段 -> 慢车..最大
            float targetRpm = 0f;
            if (running)
            {
                if (throttleLever <= idleLever)
                    targetRpm = Mathf.Lerp(0f, idleRpm, Mathf.InverseLerp(cutoffLever, idleLever, throttleLever));
                else
                    targetRpm = Mathf.Lerp(idleRpm, 1f, Mathf.InverseLerp(idleLever, maxLever, throttleLever));
            }

            // 2) 一阶滞后：指数形式，任何 dt 都不会过冲（欧拉式在 dt > tau 时会震荡）
            float tau = targetRpm >= rpm
                ? (rpm < idleRpm ? startTau : spoolUpTau)   // 启动比加速更慢
                : spoolDownTau;
            float nextRpm = targetRpm + (rpm - targetRpm) * Mathf.Exp(-dt / Mathf.Max(tau, 0.01f));

            // 3) 转速变化率上限
            if (rpmRateLimit > 0f)
                nextRpm = Mathf.MoveTowards(rpm, nextRpm, rpmRateLimit * dt);

            rpm = Mathf.Clamp01(nextRpm);

            // 4) 加力：过卡位 + 迟滞，避免杆在卡位附近来回抖
            if (!abEngaged && throttleLever >= abEnterLever) abEngaged = true;
            else if (abEngaged && throttleLever <= abExitLever) abEngaged = false;

            float targetAb = abEngaged
                ? Mathf.Lerp(minAbLevel, 1f, Mathf.InverseLerp(minAbLever, 1f, throttleLever))
                : 0f;
            abLevel = Mathf.Clamp01(targetAb + (abLevel - targetAb) * Mathf.Exp(-dt / Mathf.Max(abSpoolTau, 0.01f)));

            // 转速没起来不许点火（真实的加力连锁）
            if (rpm < idleRpm * 0.95f)
                abLevel = 0f;
        }

        // ---------------------------------------------------------------- 推力

        void UpdateThrust(float dt)
        {
            if (parentAircraft == null || parentAircraft.Rb == null)
            {
                thurst = 0f;
                return;
            }

            // 军用推力：慢车有 5%，随转速非线性上升
            float milFraction = Mathf.Lerp(idleThrustFraction, 1f,
                Mathf.Pow(Mathf.InverseLerp(idleRpm, 1f, rpm), 1.6f));
            float milThrust = maxThurst * milFraction;
            float abThrust = maxThurst * abThrustRatio * abLevel;

            // 大气衰减：σ^densityExponent × f(Mach)
            float altitude = TrueAltitude;
            float sigma = Mathf.Max(AtmosphereEnv.Density(altitude) / 1.225f, 1e-4f);
            float speed = parentAircraft.Rb.velocity.magnitude;
            float mach = speed / Mathf.Max(AtmosphereEnv.SonicSpeed(altitude), 1f);
            float machFactor = (thrustVsMach != null && thrustVsMach.length > 0)
                ? Mathf.Max(thrustVsMach.Evaluate(mach), 0f)
                : 1f;
            float lapse = Mathf.Pow(sigma, densityExponent) * machFactor;

            thurst = (milThrust + abThrust) * lapse;

            if (isEngineToggled && thurst > 0f)
                parentAircraft.Rb.AddForce(transform.forward * thurst);

            UpdateEffects();
        }

        // 尾焰：干推看转速，加力看 abLevel。不再用会随高度缩水的推力百分比
        void UpdateEffects()
        {
            if (flameMainEffect == null)
                return;

            float dry = Mathf.InverseLerp(idleRpm, 1f, rpm);
            float flameScale = abLevel > 0f
                ? Mathf.Lerp(defaultMaxThurstScale, 1f, abLevel)
                : Mathf.Lerp(defaultMaxThurstScale * 0.4f, defaultMaxThurstScale, dry);

            Vector3 scale = flameMainEffect.localScale;
            flameMainEffect.localScale = new Vector3(scale.x, scale.y, flameScale);
        }
    }
}
