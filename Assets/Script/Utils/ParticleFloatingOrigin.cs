using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Utils
{
    public class ParticleFloatingOrigin : MonoBehaviour
    {
        public ParticleSystem[] registedParticles;

        public void Start()
        {
            if (registedParticles != null)
            {
                foreach (ParticleSystem particle in registedParticles)
                {
                    ParticleSystem.MainModule mainModule = particle.main;
                    mainModule.simulationSpace = ParticleSystemSimulationSpace.Custom;
                    mainModule.customSimulationSpace = FloatingOrigin.Instance.particleRoot;
                }
            }

            // after we completed all the things, the only thing we need to de is destroy or just hide this.
            //Destroy(this);
        }

        public void SearchAllParticles()
        {
            registedParticles = transform.GetComponentsInChildren<ParticleSystem>(false);
        }
    }
}