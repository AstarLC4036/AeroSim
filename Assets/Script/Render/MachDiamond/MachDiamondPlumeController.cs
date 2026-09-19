using AeroSim.AircraftModules;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AeroSim.Render
{
    /// <summary>
    /// Drives <c>AeroSim/MachDiamond/Plume</c> on a container cylinder.
    ///
    /// Responsibilities
    ///   * size the container so that the transform scale equals the metric
    ///     volume the shader raymarches (radius on x/y, length on z, axis = +Z),
    ///   * convert engine thrust into an afterburner 0..1 value,
    ///   * push per-instance shader values through a MaterialPropertyBlock so
    ///     several plumes can share one material,
    ///   * keep the raymarch step count proportional to the on-screen size,
    ///   * drive an optional point light, and disable the renderer when the
    ///     plume is invisible (engine off) so it costs nothing.
    ///
    /// Put this on a child of the engine nozzle and rotate it so that local +Z
    /// points along the exhaust direction. It follows the parent transform, so
    /// rotation/translation of the aircraft comes for free.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    [AddComponentMenu("AeroSim/Rendering/Mach Diamond Plume Controller")]
    public class MachDiamondPlumeController : MonoBehaviour
    {
        const string ShaderName = "AeroSim/MachDiamond/Plume";
        const float RefCellWidth = 0.8f;      // reference pattern cell width (units)
        const float RefVisibleRadius = 0.36f; // measured radial extent of the reference glow (units)

        static readonly int ObjScaleId = Shader.PropertyToID("_ObjScale");
        static readonly int CellLengthId = Shader.PropertyToID("_CellLength");
        static readonly int StepsId = Shader.PropertyToID("_Steps");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("Source")]
        [Tooltip("Engine that drives the plume. Empty = use the manual Afterburner value below.")]
        public EngineModule engine;
        [Range(0f, 1f)] public float afterburner = 0f;

        [Header("Intensity")]
        public float dryIntensity = 0.18f;
        public float afterburnerIntensity = 1.4f;
        [Tooltip("How fast the plume reacts (1/s). 0 = instant.")]
        public float smoothing = 7f;

        [Header("Geometry")]
        [Tooltip("On: the visible plume radius matches the nozzle radius (shock cell length follows). Off: cell length drives the scale.")]
        public bool sizeFromNozzleRadius = true;
        public float nozzleRadius = 0.45f;
        [Tooltip("Shock cell length in metres (used when sizeFromNozzleRadius is off).")]
        public float cellLength = 1.0f;
        [Tooltip("Full-afterburner plume length in metres.")]
        public float plumeLength = 6.0f;
        [Range(0.05f, 1f)] public float dryLengthFactor = 0.45f;
        [Tooltip("Visible radius of the volume in reference units (body ~0.20, glow reaches ~0.36).")]
        public float volumeRadiusUnits = 0.42f;
        public float sizeMultiplier = 1f;

        [Header("Raymarch Budget")]
        public int maxSteps = 36;
        public int minSteps = 12;
        [Tooltip("Screen-height fraction at which maxSteps is used. Smaller = more quality far away.")]
        public float fullDetailFraction = 0.25f;

        [Header("Optional Light")]
        public Light glowLight;
        public float glowLightIntensity = 6f;

        MeshRenderer _renderer;
        MeshFilter _filter;
        Material _runtimeMaterial;
        MaterialPropertyBlock _mpb;
        float _afterburnerSmooth;
        float _seed;
        int _steps = -1;
        bool _ownsRuntimeMaterial;
        bool _materialNoticeLogged;

        /// <summary>Smoothed afterburner level actually being rendered (0..1).</summary>
        public float CurrentAfterburner => _afterburnerSmooth;

        void OnEnable()
        {
            _renderer = GetComponent<MeshRenderer>();
            _filter = GetComponent<MeshFilter>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_seed == 0f) _seed = Random.value * 100f;

            MachDiamondPlumeMesh.GetOrCreate(_filter);
            EnsureMaterial();
            ConfigureRenderer();

            _afterburnerSmooth = DriveTarget();   // snap, so enabling does not fade in
            Apply();
        }

        void OnDisable()
        {
            if (_ownsRuntimeMaterial && _runtimeMaterial != null && Application.isPlaying)
            {
                Destroy(_runtimeMaterial);
            }
            _runtimeMaterial = null;
            _ownsRuntimeMaterial = false;
        }

        void LateUpdate()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
            float target = DriveTarget();

            if (smoothing <= 0f || dt <= 0f)
            {
                _afterburnerSmooth = target;
            }
            else
            {
                float k = 1f - Mathf.Exp(-smoothing * dt);
                _afterburnerSmooth = Mathf.Lerp(_afterburnerSmooth, target, k);
                if (Mathf.Abs(_afterburnerSmooth - target) < 1e-4f) _afterburnerSmooth = target;
            }

            Apply();
        }

        // ---------------------------------------------------------------- public

        /// <summary>Manual afterburner control (0 = dry, 1 = full afterburner).</summary>
        public void SetAfterburner(float value)
        {
            afterburner = Mathf.Clamp01(value);
            if (engine == null) _afterburnerSmooth = afterburner;
        }

        /// <summary>Bind an engine module at runtime.</summary>
        public void SetEngine(EngineModule module) => engine = module;

        // --------------------------------------------------------------- internal

        /// <summary>
        /// Engine thrust above military thrust is the afterburner range: the
        /// engine allows up to 1.1 * maxThurst, which maps to 0..1 here (the
        /// same split EngineModule uses for its flame scaling).
        /// </summary>
        float DriveTarget()
        {
            if (engine == null) return Mathf.Clamp01(afterburner);
            if (!engine.isEngineToggled) return 0f;

            float max = Mathf.Max(engine.maxThurst, 1e-3f);
            return Mathf.Clamp01((engine.thurst - max) / (max * 0.1f));
        }

        void EnsureMaterial()
        {
            Material current = _renderer.sharedMaterial;
            bool usesPlumeShader = current != null && current.shader != null && current.shader.name == ShaderName;

#if UNITY_EDITOR
            // A material created at runtime is not an asset: it would serialize
            // as a missing reference the next time the scene is loaded, so it has
            // to be upgraded to the saved material as well (this is what leaves a
            // stale "(runtime)" material in the slot otherwise).
            bool needsAsset = !usesPlumeShader || !EditorUtility.IsPersistent(current);
            if (!needsAsset)
            {
                _ownsRuntimeMaterial = false;
                return;
            }

            Material asset = MachDiamondPlumeAssets.EnsureMaterial();
            if (asset == null) return;

            if (current != null && current != asset && !_materialNoticeLogged)
            {
                string replaced = current.shader != null ? current.shader.name : "(no shader)";
                Debug.LogWarning(
                    $"[MachDiamond] This renderer had material '{current.name}' (shader '{replaced}'); " +
                    "it has been swapped for the Mach diamond plume material asset. Press Ctrl+Z to restore it and, " +
                    "if you wanted to keep both effects, create the plume as its own object instead: " +
                    "GameObject > AeroSim > Mach Diamond Plume.", this);
                _materialNoticeLogged = true;
            }

            Undo.RecordObject(_renderer, "Assign Mach Diamond Plume Material");
            _renderer.sharedMaterial = asset;
            EditorUtility.SetDirty(_renderer);
            _ownsRuntimeMaterial = false;
#else
            if (usesPlumeShader)
            {
                _ownsRuntimeMaterial = false;
                return;
            }

            if (current != null && current.shader != null)
            {
                // Never clobber a foreign material in a player build.
                if (!_materialNoticeLogged)
                {
                    Debug.LogError(
                        $"[MachDiamond] Renderer already uses shader '{current.shader.name}'; refusing to replace it at runtime. " +
                        "Assign MachDiamondPlume.mat to this renderer in the editor instead.", this);
                    _materialNoticeLogged = true;
                }
                return;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[MachDiamond] Shader '{ShaderName}' not found.", this);
                return;
            }

            _runtimeMaterial = new Material(shader)
            {
                name = "MachDiamondPlume (runtime)",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            _renderer.sharedMaterial = _runtimeMaterial;
            _ownsRuntimeMaterial = true;

            if (!_materialNoticeLogged)
            {
                Debug.LogWarning("[MachDiamond] No plume material assigned; created a runtime one. " +
                                 "Use GameObject > AeroSim > Mach Diamond Plume to get a saved material asset.", this);
                _materialNoticeLogged = true;
            }
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Assign Mach Diamond Plume Material")]
        void AssignMaterialFromContextMenu()
        {
            _materialNoticeLogged = false;
            EnsureMaterial();
        }
#endif

        void ConfigureRenderer()
        {
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        void Apply()
        {
            float level = Mathf.Clamp01(_afterburnerSmooth);

            // metres per reference pattern unit
            float unit = sizeFromNozzleRadius
                ? Mathf.Max(nozzleRadius, 1e-3f) / RefVisibleRadius
                : Mathf.Max(cellLength, 1e-3f) / RefCellWidth;

            float metresPerCell = unit * RefCellWidth;
            float radius = Mathf.Max(volumeRadiusUnits, 0.05f) * unit * sizeMultiplier;
            float length = Mathf.Max(plumeLength, 0.05f) * Mathf.Lerp(dryLengthFactor, 1f, level) * sizeMultiplier;

            // container scale == shading volume (the shader reads the same numbers)
            var scale = new Vector3(radius, radius, length);
            if ((transform.localScale - scale).sqrMagnitude > 1e-10f) transform.localScale = scale;

            float intensity = Mathf.Lerp(dryIntensity, afterburnerIntensity, level);

            _mpb.SetVector(ObjScaleId, new Vector4(radius, radius, length, 0f));
            _mpb.SetFloat(CellLengthId, metresPerCell);
            _mpb.SetFloat(SeedId, _seed);
            _mpb.SetFloat(IntensityId, intensity);

            int steps = ComputeSteps(radius);
            if (steps != _steps)
            {
                _steps = steps;
                _mpb.SetFloat(StepsId, steps);
            }

            _renderer.SetPropertyBlock(_mpb);
            _renderer.enabled = intensity > 0.01f;

            if (glowLight != null)
            {
                float glow = Mathf.Lerp(0f, glowLightIntensity, level);
                glowLight.intensity = glow;
                glowLight.enabled = glow > 0.01f;
            }
        }

        /// <summary>Step count from the plume's apparent size, so small/far plumes stay cheap.</summary>
        int ComputeSteps(float radius)
        {
            Camera cam = Camera.main;
            if (cam == null) return maxSteps;

            float dist = Vector3.Distance(cam.transform.position, transform.position);
            float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Max(dist, 0.01f);
            float screenFraction = (radius * 2f) / Mathf.Max(halfHeight * 2f, 1e-4f);
            float k = Mathf.Clamp01(screenFraction / Mathf.Max(fullDetailFraction, 1e-3f));

            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minSteps, maxSteps, k)), minSteps, maxSteps);
        }

        void OnDrawGizmosSelected()
        {
            float radius = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 1e-4f);
            float length = Mathf.Max(Mathf.Abs(transform.lossyScale.z), 1e-4f);
            Matrix4x4 previous = Gizmos.matrix;
            // rotation only: the radii below are already in world units
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.85f);
            DrawRing(radius, 0f);
            DrawRing(radius, length);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 0.5f * i;
                var offset = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Gizmos.DrawLine(offset, offset + new Vector3(0f, 0f, length));
            }
            Gizmos.DrawLine(Vector3.zero, new Vector3(0f, 0f, length));

            // shock cell markers: shows the pattern scale while authoring
            float cell = sizeFromNozzleRadius
                ? Mathf.Max(nozzleRadius, 1e-3f) / RefVisibleRadius * RefCellWidth
                : Mathf.Max(cellLength, 1e-3f);
            Gizmos.color = new Color(0.25f, 0.75f, 1f, 0.7f);
            for (float z = cell * 0.5f; z < length; z += cell) DrawRing(radius * 0.35f, z);

            Gizmos.matrix = previous;
        }

        static void DrawRing(float radius, float z)
        {
            const int segments = 32;
            float step = Mathf.PI * 2f / segments;
            var previous = new Vector3(radius, 0f, z);
            for (int i = 1; i <= segments; i++)
            {
                float a = step * i;
                var current = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }
}
