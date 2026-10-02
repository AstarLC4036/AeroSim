using AeroSim.AeroPhysics;
using AeroSim.Audio;
using AeroSim.Cockpit;
using AeroSim.InputSystem;
using AeroSim.Utils;
using UnityEngine;

namespace AeroSim.AircraftModules
{
    /// <summary>
    /// Throttle detents, matching the cockpit panel markings one to one: cutoff 0 / idle 14 / max 68 / min AB 80 / full AB 100.
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
    /// Engine module with spool speed (N1) simulation.
    ///
    /// Three state variables — do not mix them up:
    ///   throttleLever  throttle lever position (the pilot's hand); the detents match the cockpit panel markings
    ///   rpm            N1 spool speed (the engine's response), first-order lag + rate limit
    ///   abLevel        afterburner stage (fuel added behind the turbine, with its own light-up/shut-down lag; spool speed barely moves while the burner is lit)
    ///
    /// Thrust = (military thrust(rpm) + afterburner thrust(abLevel)) * σ^densityExponent * thrustVsMach(mach)
    /// σ = ρ/ρ0, so thrust really does decay at altitude (the old constant thrust would send a J-10C to 34 km / M5).
    /// </summary>
    /// <summary>Twin-spool engine run state / start sequence.</summary>
    public enum EngineStartState
    {
        /// <summary>Shut down, no fuel, spools stationary.</summary>
        Off = 0,
        /// <summary>Starter motor winds the HP spool up (N1 only windmills).</summary>
        Crank = 1,
        /// <summary>Fuel on, ignition: temperature spikes while N2 keeps rising.</summary>
        Light = 2,
        /// <summary>Both spools at idle.</summary>
        Idle = 3,
        /// <summary>Normal operation, throttle lever drives the engine.</summary>
        Run = 4,
        /// <summary>Fuel cut: T4 falls first, then N2, then N1.</summary>
        Shutdown = 5
    }

    public class EngineModule : AircraftModule
    {
        [Header("Thrust (N)")]
        [Tooltip("Sea-level static thrust in the military (max dry) setting. Total afterburner thrust = maxThurst * (1 + abThrustRatio)")]
        public float maxThurst = 97000f;
        [Tooltip("Extra afterburner thrust as a fraction of military thrust (0.4 ~ 0.6 for modern turbofans)")]
        public float abThrustRatio = 0.5f;
        [Tooltip("Relative thrust coefficient versus Mach number: a slight rise through the ram range, then a fall-off")]
        public AnimationCurve thrustVsMach = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.8f, 1.12f),
            new Keyframe(1.2f, 1.0f),
            new Keyframe(2f, 0.7f),
            new Keyframe(3f, 0.45f),
            new Keyframe(5f, 0.2f));
        [Tooltip("Exponent of thrust versus density ratio σ (turbofans commonly use 0.7 ~ 1.0)")]
        [Range(0.4f, 1.2f)] public float densityExponent = 0.8f;

        [Header("Spool simulation (N1, normalised 0~1)")]
        [Range(0.3f, 0.9f), Tooltip("Idle N1 spool speed")] public float idleRpm = 0.65f;
        [Range(0f, 1f), Tooltip("Idle N2 spool speed")] public float idleRpm2 = 0.72f;   // Idle N2 (higher than the 0.65 of N1)
        [Range(0.1f, 1f), Tooltip("N2 response speed")] public float n2Response = 0.6f; // <1 → at low thrust N2 rises faster than N1 (the core spools up first)
        [Tooltip("Idle thrust as a fraction of military thrust (about 5% on the real aircraft)")]
        [Range(0f, 0.2f)] public float idleThrustFraction = 0.05f;
        [Tooltip("Acceleration time constant (s): idle -> max. Big turbofans are slow, which is what makes the throttle feel like it has weight")]
        public float spoolUpTau = 5f;
        [Tooltip("Deceleration time constant (s)")]
        public float spoolDownTau = 6f;
        [Tooltip("Start-up (cutoff -> idle) time constant (s)")]
        public float startTau = 10f;
        [Tooltip("Afterburner light-up/shut-down time constant (s)")]
        public float abSpoolTau = 1.5f;
        [Tooltip("Spool rate limit (/s); 0 = unlimited")]
        public float rpmRateLimit = 0.15f;

        [Header("N2 (HP spool / core)")]
        [Tooltip("N2 spool-up time constant (s): the core responds much faster than the fan")]
        public float n2SpoolUpTau = 2f;
        [Tooltip("N2 spool-down time constant (s)")]
        public float n2SpoolDownTau = 2.5f;

        [Header("T4 / EGT (deg C)")]
        [Tooltip("Idle turbine-exit temperature")]
        public float idleT4 = 450f;
        [Tooltip("Military (max dry) turbine-exit temperature")]
        public float maxT4 = 950f;
        [Tooltip("Extra temperature with the afterburner fully lit")]
        public float abT4Rise = 250f;
        [Tooltip("Thermal inertia time constant (s): slower than the spool, so the needle lags the throttle")]
        public float t4Tau = 5f;
        [Tooltip("Gauge red line")]
        public float t4Limit = 1000f;
        [Tooltip("Light-off overshoot (start-up runs fuel rich)")]
        public float startT4Peak = 220f;
        [Tooltip("Afterburner light-up overshoot")]
        public float abT4Peak = 80f;
        [Tooltip("How fast the light-off / AB overshoot decays (s)")]
        public float t4PeakDecay = 2.5f;
        [Tooltip("How much the ambient temperature shifts T4 (1 = OAT offset passes straight through)")]
        public float oatInfluence = 1f;

        [Header("Start sequence")]
        [Tooltip("State on spawn. Off = realistic cold start; Run = spawn already running (test aircraft / air start)")]
        public EngineStartState initialStartState = EngineStartState.Off;
        [Tooltip("N2 the starter motor winds the HP spool up to before light-off")]
        [Range(0.05f, 0.4f)] public float starterN2 = 0.22f;
        [Tooltip("Starter motor time constant (s)")]
        public float crankTau = 3.5f;

        [Header("Throttle lever detents (lever travel 0~1)")]
        [Range(0f, 0.5f)] public float idleLever = 0.14f;
        [Range(0.3f, 0.9f)] public float maxLever = 0.68f;
        [Range(0.5f, 1f)] public float minAbLever = 0.80f;
        [Tooltip("Afterburner engagement threshold (upper hysteresis edge)")]
        [Range(0f, 1f)] public float abEnterLever = 0.79f;
        [Tooltip("Afterburner disengagement threshold (lower hysteresis edge); must be below the engagement threshold")]
        [Range(0f, 1f)] public float abExitLever = 0.74f;
        [Tooltip("Afterburner step present as soon as the min-AB detent is reached (on the real aircraft minimum afterburner thrust jumps by a notch)")]
        [Range(0f, 1f)] public float minAbLevel = 0.3f;
        [Tooltip("Travel rate of the lever while a throttle key is held (/s)")]
        public float leverSlewRate = 0.7f;
        [Tooltip("Initial lever position when entering Play: 0 = start from cutoff (default, correct behaviour); set it to 1 for testing to get full throttle/afterburner immediately")]
        [Range(0f, 1f)] public float initialLever = 0f;
        [Tooltip("Below this lever position the engine counts as shut down (fuel cutoff)")]
        [Range(0f, 0.1f)] public float cutoffLever = 0.02f;


        [Header("State (read-only)")]
        [SerializeField] private float throttleLever;
        [SerializeField] private float rpm1;
        [SerializeField] private float rpm2;
        [SerializeField] private EngineStartState startState;
        [SerializeField] private float t4;
        /// <summary>Transient temperature overshoot (light-off / AB light-up), decays into the steady target.</summary>
        [SerializeField] private float t4Boost;
        private bool abWasLit;
        [SerializeField] private float abLevel;
        [SerializeField] private bool abEngaged;
        [Tooltip("Current actual thrust (N), already including atmospheric decay")]
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

        // ---------------------------------------------------------------- Read-only interface

        /// <summary>Throttle lever position 0~1 (100% = full afterburner detent).</summary>
        public float ThrottleLever => throttleLever;
        /// <summary>Throttle lever position in percent, for the HUD/cockpit instruments.</summary>
        public float ThrottlePercent => throttleLever * 100f;
        /// <summary>N1 normalised spool speed 0~1.</summary>
        public float Rpm1 => rpm1;
        /// <summary>N1 percentage.</summary>
        public float N1Percent => rpm1 * 100f;
        /// <summary>Afterburner stage 0~1 (0 = afterburner off).</summary>
        public float AbLevel => abLevel;
        /// <summary>Whether the afterburner is "really lit" (do not set the threshold too low, or the decaying tail will keep the HUD lamp on).</summary>
        public bool IsAfterburner => abLevel > 0.05f;
        /// <summary>Current actual thrust (N).</summary>
        public float Thrust => thurst;

        /// <summary>N2 normalised spool speed 0~1 (the HP spool / core).</summary>
        public float Rpm2 => rpm2;
        /// <summary>N2 percentage.</summary>
        public float N2Percent => rpm2 * 100f;
        /// <summary>Turbine-exit temperature the gauge shows (deg C) — not the physical combustor-exit value.</summary>
        public float T4 => t4;
        /// <summary>Current engine state (start sequence).</summary>
        public EngineStartState StartState => startState;

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

        /// <summary>True altitude (with the floating-origin offset removed); the same convention as Aircraft.cs.</summary>
        public float TrueAltitude
        {
            get
            {
                if (parentAircraft == null) return 0f;
                return parentAircraft.transform.position.y - FloatingOrigin.origin.y;
            }
        }

        // ---------------------------------------------------------------- Lifecycle

        public override void Init(Aircraft aircraft)
        {
            base.Init(aircraft);
            //isEngineToggled = true;

            // Initial lever position (default 0 = start from cutoff).
            // Note: thurst is now recomputed every frame instead of being a leftover serialised value,
            // so the 141942.9 previously stored in the prefab no longer makes the engine "hit full thrust the moment Play starts".
            throttleLever = Mathf.Clamp01(initialLever);

            // Spawn state. With Off the engine has to be started from the lever (crank -> light -> idle);
            // test aircraft and air starts can set Run to spawn already at idle.
            startState = initialStartState;
            if (startState == EngineStartState.Run || startState == EngineStartState.Idle)
            {
                rpm1 = idleRpm;
                rpm2 = idleRpm2;
                t4 = idleT4;
            }

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

        // Input: only the lever position changes. The lever is the pilot's hand; the response is left to the spool speed
        void Update()
        {
            UpdateInput();
        }

        // Dynamics: integration and force application both happen in the physics step using fixedDeltaTime, independent of frame rate
        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            UpdateSpool(dt);
            UpdateT4(dt);
            UpdateThrust(dt);
        }

        public void StartEngine()
        {
            bool leverOff = throttleLever <= cutoffLever;
            if (startState == EngineStartState.Off && !leverOff && isEngineToggled)
            {
                startState = EngineStartState.Crank;
            }
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

        // ---------------------------------------------------------------- Spool / afterburner

        void UpdateSpool(float dt)
        {
            bool leverOff = throttleLever <= cutoffLever;

            // ---- Start sequence state machine ----
            EngineStartState previous = startState;
            switch (startState)
            {
                case EngineStartState.Off:
                    //if (!leverOff && isEngineToggled) startState = EngineStartState.Crank; // See 'StartEngine()'
                    break;
                case EngineStartState.Crank:
                    if (leverOff) startState = EngineStartState.Shutdown;
                    else if (rpm2 >= starterN2 * 0.98f) startState = EngineStartState.Light;
                    break;
                case EngineStartState.Light:
                    if (leverOff) startState = EngineStartState.Shutdown;
                    else if (rpm2 >= idleRpm2 * 0.97f) startState = EngineStartState.Idle;
                    break;
                case EngineStartState.Idle:
                    if (leverOff) startState = EngineStartState.Shutdown;
                    else if (throttleLever > Mathf.Lerp(idleLever, maxLever, 0.5f)) startState = EngineStartState.Run;
                    break;
                case EngineStartState.Run:
                    if (leverOff) startState = EngineStartState.Shutdown;
                    break;
                case EngineStartState.Shutdown:
                    if (rpm1 <= 0.02f && rpm2 <= 0.02f) startState = EngineStartState.Off;
                    break;
            }
            if (previous == EngineStartState.Crank && startState == EngineStartState.Light)
                t4Boost += startT4Peak;   // Light-off runs fuel rich: the temperature spikes, then settles

            // ---- N1 command: continuous across the whole sequence, so N2 (which is just its mapping) never steps ----
            float targetRpm;
            switch (startState)
            {
                case EngineStartState.Off:
                case EngineStartState.Shutdown:
                    targetRpm = 0f;
                    break;
                case EngineStartState.Crank:
                    targetRpm = idleRpm * CrankN1Fraction;   // Windmilling only
                    break;
                case EngineStartState.Light:
                    // Ramp from the cranking value up to idle as N2 comes up. This is what keeps the target
                    // continuous across the Crank -> Light transition (a hard switch would step N2's target).
                    targetRpm = Mathf.Lerp(idleRpm * CrankN1Fraction, idleRpm,
                                           Mathf.InverseLerp(starterN2 * 0.98f, idleRpm2 * 0.97f, rpm2));
                    break;
                default:
                    // Idle and Run share the same formula, so there is no step when the lever crosses into Run.
                    targetRpm = Mathf.Lerp(idleRpm, 1f, Mathf.InverseLerp(idleLever, maxLever, throttleLever));
                    break;
            }

            // ---- N1: first-order lag (exponential form, never overshoots for any dt) ----
            float tau1 = targetRpm >= rpm1
                ? (rpm1 < idleRpm ? startTau : spoolUpTau)   // Start-up is slower than acceleration
                : spoolDownTau;
            float nextRpm = targetRpm + (rpm1 - targetRpm) * Mathf.Exp(-dt / Mathf.Max(tau1, 0.01f));
            if (rpmRateLimit > 0f)
                nextRpm = Mathf.MoveTowards(rpm1, nextRpm, rpmRateLimit * dt);
            rpm1 = Mathf.Clamp01(nextRpm);

            // ---- N2: always the mapping of the *commanded* N1, with a faster lag, so N2 leads N1 during
            //      transients and its target is continuous everywhere (no steps at state transitions) ----
            float n2Target = (startState == EngineStartState.Off || startState == EngineStartState.Shutdown)
                ? 0f
                : N2FromN1(targetRpm);

            float tau2 = n2Target >= rpm2
                ? (startState == EngineStartState.Crank ? crankTau : n2SpoolUpTau)
                : n2SpoolDownTau;
            // Same exponential form as N1: target + (current - target) * exp(-dt / tau).
            // (current + (target - current) * exp(...) would snap straight to the target when dt is small.)
            rpm2 = Mathf.Clamp01(n2Target + (rpm2 - n2Target) * Mathf.Exp(-dt / Mathf.Max(tau2, 0.01f)));

            // ---- Afterburner: detent crossing + hysteresis, so the lever does not flicker around the detent ----
            if (!abEngaged && throttleLever >= abEnterLever) abEngaged = true;
            else if (abEngaged && throttleLever <= abExitLever) abEngaged = false;

            float targetAb = abEngaged
                ? Mathf.Lerp(minAbLevel, 1f, Mathf.InverseLerp(minAbLever, 1f, throttleLever))
                : 0f;
            abLevel = Mathf.Clamp01(targetAb + (abLevel - targetAb) * Mathf.Exp(-dt / Mathf.Max(abSpoolTau, 0.01f)));

            // No light-up until the spool speed is up (the real afterburner interlock)
            if (rpm1 < idleRpm * 0.95f)
                abLevel = 0f;
        }

        /// <summary>
        /// N1 to N2 mapping, anchored at (0,0), (idleRpm, idleRpm2) and (1,1), so it is valid over the whole
        /// range: N2 is 0 when stopped, about 72% at idle (higher than N1) and equal to N1 at max thrust.
        /// A plain Lerp(idleRpm2, 1, ...) would report 72% at N1 = 0, which is nonsense.
        /// </summary>
        float N2FromN1(float n1)
        {
            n1 = Mathf.Clamp01(n1);
            if (n1 <= idleRpm)
                return idleRpm2 * Mathf.Pow(idleRpm > 0f ? n1 / idleRpm : 0f, 0.8f);   // Cranking / light-off segment
            return Mathf.Lerp(idleRpm2, 1f, Mathf.Pow(Mathf.InverseLerp(idleRpm, 1f, n1), n2Response));
        }

        /// <summary>
        /// N1 the windmilling fan reaches while cranking, derived from <see cref="starterN2"/> so the two can
        /// never desync: by construction N2FromN1(idleRpm * CrankN1Fraction) == starterN2, which is what keeps
        /// the N2 target continuous at the Crank -> Light transition.
        /// </summary>
        float CrankN1Fraction => idleRpm > 0f
            ? Mathf.Clamp01(Mathf.Pow(starterN2 / Mathf.Max(idleRpm2, 1e-4f), 1f / 0.8f))
            : 0f;

        // ---------------------------------------------------------------- Turbine temperature (T4 / EGT)

        /// <summary>
        /// T4 is driven by N2 — the core decides the combustor temperature, not the fan — which is exactly why
        /// the two gauges sit together on the panel. Thermal inertia is slower than the spool, so the needle
        /// lags the throttle, and light-off / afterburner light-up add a brief overshoot that then settles.
        /// </summary>
        void UpdateT4(float dt)
        {
            float ambient = AtmosphereEnv.Temperature(TrueAltitude);   // deg C

            float target;
            if (startState == EngineStartState.Off || startState == EngineStartState.Shutdown)
                target = ambient;                                       // Soaked / cooling down
            else if (rpm2 < idleRpm2)
                target = Mathf.Lerp(ambient, idleT4, Mathf.InverseLerp(starterN2 * 0.85f, idleRpm2, rpm2));
            else
                target = Mathf.Lerp(idleT4, maxT4, Mathf.Pow(Mathf.InverseLerp(idleRpm2, 1f, rpm2), 1.2f));

            target += abT4Rise * abLevel;
            target += (ambient - 15f) * oatInfluence;
            target += t4Boost;

            t4Boost *= Mathf.Exp(-dt / Mathf.Max(t4PeakDecay, 0.05f));

            if (!abWasLit && abLevel > 0.05f) t4Boost += abT4Peak;
            abWasLit = abLevel > 0.05f;

            t4 += (target - t4) * (1f - Mathf.Exp(-dt / Mathf.Max(t4Tau, 0.05f)));
        }

        // ---------------------------------------------------------------- Thrust

        void UpdateThrust(float dt)
        {
            if (parentAircraft == null || parentAircraft.Rb == null)
            {
                thurst = 0f;
                return;
            }

            // Military thrust: 5% at idle, rising non-linearly with spool speed
            float milFraction = Mathf.Lerp(idleThrustFraction, 1f,
                Mathf.Pow(Mathf.InverseLerp(idleRpm, 1f, rpm1), 1.6f));
            float milThrust = maxThurst * milFraction;
            float abThrust = maxThurst * abThrustRatio * abLevel;

            // Atmospheric decay: sigma^densityExponent * f(Mach)
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

        // Exhaust flame: dry thrust follows spool speed, afterburner follows abLevel. No longer uses a thrust percentage that shrinks with altitude
        void UpdateEffects()
        {
            if (flameMainEffect == null)
                return;

            float dry = Mathf.InverseLerp(idleRpm, 1f, rpm1);
            float flameScale = abLevel > 0f
                ? Mathf.Lerp(defaultMaxThurstScale, 1f, abLevel)
                : Mathf.Lerp(defaultMaxThurstScale * 0.4f, defaultMaxThurstScale, dry);

            Vector3 scale = flameMainEffect.localScale;
            flameMainEffect.localScale = new Vector3(scale.x, scale.y, flameScale);
        }
    }
}
