using System.Collections;
using UnityEngine;

namespace AeroSim.Audio
{
    public class EngineAudio : MonoBehaviour
    {
        public DirectionalAudio audioStrength;
        public DopplerPitch audioPitch;

        public float volume = 1.0f;
        public float pitch = 1.0f;

        // Update is called once per frame
        void FixedUpdate()
        {
            if (audioStrength != null)
                audioStrength.baseVolume = volume;

            if (audioPitch != null)
                audioPitch.defaultPitch = pitch;
        }
    }
}
