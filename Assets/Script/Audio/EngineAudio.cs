using AeroSim.AircraftModules;
using AeroSim.InputSystem;
using System.Collections;
using UnityEngine;
using FMODUnity;

namespace AeroSim.Audio
{
    public class EngineAudio : MonoBehaviour
    {
        public EngineModule engine;
        public StudioEventEmitter engineEmitter;
        //public DirectionalAudio audioStrength;
        //public DopplerPitch audioPitch;

        public float referenceMaxVolume = 0; // dB
        public float referenceMinVolume = -65;
        public float referenceVolume = 0;
        public float referenceDistance = 1;

        // Update is called once per frame
        void FixedUpdate()
        {
            //if (audioStrength != null)
            //    audioStrength.baseVolume = volume;

            //if (audioPitch != null)
            //    audioPitch.defaultPitch = pitch;

            if (engine == null)
                return;

            UpdateEmit();
        }

        void UpdateEmit()
        {
            float thrustPercent = Mathf.Clamp01(engine.thurst / engine.maxThurst);
            bool isWep = engine.isEngineToggled && engine.thurst > engine.maxThurst;

            float volumeParam = 1;
            if (AudioManager.FmodListener != null)
            {
                float dstToListener = Vector3.Distance(transform.position, AudioManager.FmodListener.transform.position);

                float volume = referenceVolume - 20 * Mathf.Log10(dstToListener / referenceDistance);
                volumeParam = Mathf.Clamp01((1 / (referenceMaxVolume - referenceMinVolume)) * (volume - referenceMinVolume));
            }

            if (engineEmitter != null)
            {
                float wep = isWep ? 1f : 0f;
                bool isCockpitView = CameraController.Instance != null && CameraController.CurrentView.view == CameraController.CameraView.ViewType.Cockpit;

                engineEmitter.SetParameter("Throttle", thrustPercent);
                engineEmitter.SetParameter("WEP", wep);
                engineEmitter.SetParameter("Inner", isCockpitView ? 1 : 0);
                engineEmitter.SetParameter("Volume", volumeParam);
            }
        }
    }
}
