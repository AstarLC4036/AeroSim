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
            // 音色由 N1 转速驱动（不是推力百分比：高空推力会衰减，但转速不该跟着变）
            float n1 = Mathf.Clamp01(engine.Rpm);
            float wepLevel = Mathf.Clamp01(engine.AbLevel);

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
