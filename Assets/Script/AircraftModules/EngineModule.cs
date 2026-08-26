using AeroSim.AeroPhysics;
using AeroSim.Audio;
using AeroSim.InputSystem;
using AeroSim.Utils;
using System.Collections;
using UnityEngine;
using FMODUnity;

namespace AeroSim.AircraftModules
{
    public class EngineModule : AircraftModule
    {
        public bool isEngineToggled = false;
        public float maxThurst = 10000;
        public float thurst = 0;
        public float deltaThrust = 100;

        [Header("Audio")]
        public EngineAudio audioController;
        public float maxPitch;
        public float minPitch;
        public float maxStrength;
        public float minStrength;
        private StudioEventEmitter fmodEmitter;
        [Header("Effect")]
        public EffectController flameEffect;
        public Transform flameMainEffect;
        public float defaultMaxThurstScale = 0.25f;

        public override void Init(Aircraft aircraft)
        {
            base.Init(aircraft);
            isEngineToggled = true;

            if (flameEffect != null)
                flameEffect.Play();
        }

        // Use this for initialization
        void Start()
        {
            audioController = transform.GetComponentInChildren<EngineAudio>();
            fmodEmitter = transform.GetComponentInChildren<StudioEventEmitter>();
        }

        // Update is called once per frame
        void Update()
        {
            UpdateInput();
        }

        private void FixedUpdate()
        {
            UpdateThrust();
        }

        public void UpdateThrust()
        {
            if (thurst != 0 && isEngineToggled)
                parentAircraft.Rb.AddForce(transform.forward * thurst);

            float thrustPercent = Mathf.Clamp01(thurst / maxThurst);
            bool isWep = isEngineToggled && thurst > maxThurst;

            if (flameMainEffect != null)
            {
                float flameScale = thurst <= maxThurst
                    ? Mathf.Lerp(defaultMaxThurstScale * 0.4f, defaultMaxThurstScale, thrustPercent)
                    : Mathf.Lerp(defaultMaxThurstScale, 1, (thurst - maxThurst) / (maxThurst * 0.1f));

                Vector3 scale = flameMainEffect.localScale;
                flameMainEffect.localScale = new Vector3(scale.x, scale.y, flameScale);
            }

            if (fmodEmitter != null)
            {
                float wep = isWep ? 1f : 0f;
                float inner = CameraController.Instance != null && CameraController.Instance.CurrentViewIndex != 0 ? 1f : 0f;

                fmodEmitter.SetParameter("Throttle", thrustPercent);
                fmodEmitter.SetParameter("WEP", wep);
                fmodEmitter.SetParameter("Inner", inner);
            }
        }

        void UpdateInput()
        {
            if (parentAircraft.isControlling)
            {
                if (Input.GetKey(Keybindings.thurstUp))
                {
                    if (thurst < maxThurst * 1.1f)
                        thurst += deltaThrust * Time.deltaTime;
                    if (thurst > maxThurst * 1.1f)
                        thurst = maxThurst * 1.1f;
                }
                else if (Input.GetKey(Keybindings.thurstDown))
                {
                    if (thurst > 0)
                        thurst -= deltaThrust * Time.deltaTime;
                    if (thurst < 0)
                        thurst = 0;
                }
            }
        }
    }

}
