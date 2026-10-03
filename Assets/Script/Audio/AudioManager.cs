using FMODUnity;
using System.Collections;
using UnityEngine;

namespace AeroSim.Audio
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance => instance;

        public StudioListener fmodListener;
        public static StudioListener FmodListener => instance.fmodListener;

        public AudioSource rwrAudio;
        public StudioEventEmitter irMslScanAudio;
        public StudioEventEmitter irMslTrackAudio;
        public StudioEventEmitter rwrScan;
        public StudioEventEmitter rwrLock;
        public StudioEventEmitter rwrMsl;
        //public AudioClip irSearch;
        //public AudioClip irLock;

        public string cockpitSwitchClickPath;
        public string cockpitButtonClickPath;

        public static bool IsPlayingRwr => rwrState != 0;

        private static int rwrState = 0;

        public void Awake()
        {
            instance = this;
            fmodListener = FindAnyObjectByType<StudioListener>();
        }

        // Update is called once per frame
        void Update()
        {

        }

        public static void RWRScan()
        {
            if (rwrState == 1 && instance.rwrScan.IsPlaying())
                return;

            RWRStop();

            instance.rwrScan.Play();
            rwrState = 1;
        }

        public static void RWRLock()
        {
            if (rwrState == 2)
                return;

            RWRStop();

            instance.rwrLock.Play();
            rwrState = 2;
        }

        public static void RWRMsl()
        {
            if (rwrState == 3)
                return;

            RWRStop();

            instance.rwrMsl.Play();
            rwrState = 3;
        }

        public static void RWRStop()
        {
            instance.rwrScan.Stop();
            instance.rwrLock.Stop();
            instance.rwrMsl.Stop();

            rwrState = 0;
        }

        public static void MissileIRSearch()
        {
            Instance.irMslTrackAudio.Stop();
            Instance.irMslScanAudio.Play();
        }

        public static void MissileIRLock()
        {
            Instance.irMslScanAudio.Stop();
            Instance.irMslTrackAudio.Play();
        }

        public static void MissileStop()
        {
            Instance.irMslScanAudio.Stop();
            Instance.irMslTrackAudio.Stop();
        }

        public static void CockpitButtonClick(Vector3 position)
        {
            RuntimeManager.PlayOneShot(instance.cockpitButtonClickPath, position);
        }

        public static void CockpitSwitchClick(Vector3 position)
        {
            RuntimeManager.PlayOneShot(instance.cockpitSwitchClickPath, position);
        }
    }
}