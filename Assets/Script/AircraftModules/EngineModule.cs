using AeroSim.AeroPhysics;
using AeroSim.Audio;
using AeroSim.InputSystem;
using AeroSim.Utils;
using System.Collections;
using UnityEngine;

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

        [Header("Effect")]
        public EffectController flameEffect;
        public Transform flameMainEffect;
        public float defaultMaxThurstScale = 0.25f;

        public override void Init(Aircraft aircraft)
        {
            base.Init(aircraft);
            isEngineToggled = true;
            flameEffect.Play();
        }

        // Use this for initialization
        void Start()
        {
            audioController = transform.GetComponentInChildren<EngineAudio>();
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

            if(audioController != null)
            {
                float thurstPercent = thurst / maxThurst;
                float thurstPercentClamped = Mathf.Clamp01(thurstPercent);
                audioController.volume = Mathf.Lerp(minStrength, maxStrength, thurstPercentClamped);
                audioController.pitch = Mathf.Lerp(minPitch, maxPitch, thurstPercentClamped);

                if(thurstPercent <= 1)
                {
                    flameMainEffect.localScale = new Vector3(flameMainEffect.localScale.x, flameMainEffect.localScale.y, Mathf.Lerp(0, defaultMaxThurstScale, thurstPercent));
                }
                else
                {
                    float scaleParam = (thurstPercent - 1) / 0.1f;
                    flameMainEffect.localScale = new Vector3(flameMainEffect.localScale.x, flameMainEffect.localScale.y, Mathf.Lerp(defaultMaxThurstScale, 1, scaleParam));
                }
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