using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.VFX;

namespace AeroSim.Utils
{
    public class EffectController : MonoBehaviour
    {
        public GameObject[] activeObjects;
        public ParticleSystem[] particleSystems;
        public VisualEffect[] visualEffects;
        public bool IsPlaying => isPlaying;

        private bool isPlaying = false;

        public void Play()
        {
            isPlaying = true;

            if (activeObjects != null && activeObjects.Length > 0)
            {
                foreach (GameObject activeObject in activeObjects)
                {
                    activeObject.SetActive(true);
                }
            }

            if (particleSystems != null && particleSystems.Length > 0)
            {
                foreach (ParticleSystem particle in particleSystems)
                {
                    particle.Play();
                }
            }

            if (visualEffects != null && visualEffects.Length > 0)
            {
                foreach (VisualEffect effect in visualEffects)
                {
                    effect.Play();
                }
            }
        }

        public void Stop()
        {
            isPlaying = false;

            if (activeObjects != null && activeObjects.Length > 0)
            {
                foreach (GameObject activeObject in activeObjects)
                {
                    activeObject.SetActive(false);
                }
            }

            if (particleSystems != null && particleSystems.Length > 0)
            {
                foreach (ParticleSystem particle in particleSystems)
                {
                    particle.Stop();
                }
            }

            if (visualEffects != null && visualEffects.Length > 0)
            {
                foreach (VisualEffect effect in visualEffects)
                {
                    effect.Stop();
                }
            }
        }
    }
}
