using AeroSim.Audio;
using AeroSim.InputSystem;
using FMODUnity;
using System.Collections;
using UnityEngine;

namespace AeroSim.Audio
{
    public class MissileAudio : MonoBehaviour
    {
        public StudioEventEmitter emitter;

        public void Play()
        {
            emitter.Play();
        }

        public void Stop()
        {
            emitter.Stop();
        }

        // Update is called once per frame
        void Update()
        {
            UpdateEmit();
        }

        void UpdateEmit()
        {

            //float volumeParam = 1;
            float dstToListener = 10000;
            if (AudioManager.FmodListener != null)
            {
                dstToListener = Vector3.Distance(transform.position, AudioManager.FmodListener.transform.position);
            }

            if (emitter != null)
            {
                bool isCockpitView = CameraController.Instance != null && CameraController.CurrentView.view == CameraController.CameraView.ViewType.Cockpit;

                emitter.SetParameter("Inner", isCockpitView ? 1 : 0);
                emitter.SetParameter("dist", dstToListener / 1000);
            }
        }
    }
}