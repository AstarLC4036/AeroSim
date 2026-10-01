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
            // Timbre is driven by N1 spool speed (not by a thrust percentage: thrust decays at altitude, but spool speed should not follow it)
            float n1 = Mathf.Clamp01(engine.Rpm1);
            // wepLevel must be greater than 0.9 while in afterburner, because FMOD only plays the ignition sound when wepLevel exceeds 0.9
            // (DeepSeek v4.1-flash has no Grep for FMOD files)
            //float wepLevel = Mathf.Clamp01(engine.AbLevel);
            int wepLevel = engine.AbLevel > 0 ? 1 : 0;

            //float volumeParam = 1;
            float dstToListener = 10000;
            if (AudioManager.FmodListener != null)
            {
                dstToListener = Vector3.Distance(transform.position, AudioManager.FmodListener.transform.position);

                //float volume = referenceVolume - 20 * Mathf.Log10(dstToListener / referenceDistance);
                //volumeParam = Mathf.Clamp01((1 / (referenceMaxVolume - referenceMinVolume)) * (volume - referenceMinVolume));
            }

            if (engineEmitter != null)
            {
                bool isCockpitView = CameraController.Instance != null && CameraController.CurrentView.view == CameraController.CameraView.ViewType.Cockpit;

                engineEmitter.SetParameter("Throttle", n1);
                engineEmitter.SetParameter("WEP", wepLevel);
                engineEmitter.SetParameter("Inner", isCockpitView ? 1 : 0);
                //engineEmitter.SetParameter("Volume", volumeParam);
                engineEmitter.SetParameter("dist", dstToListener / 1000);
            }
        }
    }
}
