using System.Collections;
using UnityEngine;

namespace AeroSim.Audio
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance => instance;

        public AudioSource rwrAudio;
        public AudioClip rwrScan;
        public AudioClip rwrLock;
        public AudioClip rwrMsl;

        public static bool IsPlayingLock => Instance.rwrAudio.clip == Instance.rwrLock && Instance.rwrAudio.isPlaying;
        public static bool IsPlayingMsl => Instance.rwrAudio.clip == Instance.rwrMsl && Instance.rwrAudio.isPlaying;
        public static bool IsPlayingRwr => Instance.rwrAudio.isPlaying;

        public void Awake()
        {
            instance = this;
        }

        // Update is called once per frame
        void Update()
        {

        }

        public static void RWRScan()
        {
            if (Instance.rwrAudio.clip == Instance.rwrScan && IsPlayingRwr)
                return;

            RWRStop();

            Instance.rwrAudio.clip = Instance.rwrScan;
            Instance.rwrAudio.loop = false;
            Instance.rwrAudio.Play();
        }

        public static void RWRLock()
        {
            if (Instance.rwrAudio.clip == Instance.rwrLock && IsPlayingRwr)
                return;

            RWRStop();

            Instance.rwrAudio.clip = Instance.rwrLock;
            Instance.rwrAudio.loop = true;
            Instance.rwrAudio.Play();
        }

        public static void RWRMsl()
        {
            if (Instance.rwrAudio.clip == Instance.rwrMsl && IsPlayingRwr)
                return;

            RWRStop();

            Instance.rwrAudio.clip = Instance.rwrMsl;
            Instance.rwrAudio.loop = true;
            Instance.rwrAudio.Play();
        }

        public static void RWRStop()
        {
            if (Instance.rwrAudio.isPlaying)
                Instance.rwrAudio.Stop();
        }
    }
}