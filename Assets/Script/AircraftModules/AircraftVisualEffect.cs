using AeroSim.AeroPhysics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace AeroSim.AircraftModules
{
    [ExecuteInEditMode]
    public class AircraftVisualEffect : MonoBehaviour
    {
        public AeroSurface leftTipWing;
        public AeroSurface rightTipWing;

        [Header("World Space Effects")]
        public ParticleSystem leftWingTipVortex;
        public ParticleSystem rightWingTipVortex;
        public ParticleSystem leftWingTipSomke;
        public ParticleSystem rightWingTipSomke;

        [Header("Local Space Effects")]
        public VisualEffect leftVortexCloud;
        public bool isLeftVortexCloudPlaying = false;
        public VisualEffect rightVortexCloud;
        public bool isRightVortexCloudPlaying = false;

        [Header("Vortex Parameters")]
        public float wingTipVotexCLThreshold = 0.5f;
        public float wingTipVotexSpeedThreshold = 15f;
        public float vortexCloudAoaThreshold = 0.5f;
        public float vortexCloudSpeedThreshold = 200f;

        void OnEnable()
        {
            leftVortexCloud.Stop();
            rightVortexCloud.Stop();
        }

        void Start()
        {
            leftVortexCloud.Stop();
            rightVortexCloud.Stop();
        }

        public void FixedUpdate()
        {
            if (Application.isPlaying)
            {
                UpdateWingTipVortex();
                UpdateVortexCloud();
            }
            else
            {
                if(isLeftVortexCloudPlaying)
                {
                    leftVortexCloud.Stop();
                    isLeftVortexCloudPlaying = false;
                }
                if(isRightVortexCloudPlaying)
                {
                    rightVortexCloud.Stop();
                    isRightVortexCloudPlaying = false;
                }
            }
        }

        private void UpdateWingTipVortex()
        {
            if(leftTipWing.coefficientsInfo.x > wingTipVotexCLThreshold && leftTipWing.LocalVelocity.magnitude > wingTipVotexSpeedThreshold)
            {
                if(!leftWingTipVortex.isPlaying)
                    leftWingTipVortex.Play();
            }
            else if(leftWingTipVortex.isPlaying)
            {
                leftWingTipVortex.Stop();
            }

            if(rightTipWing.coefficientsInfo.x > wingTipVotexCLThreshold && rightTipWing.LocalVelocity.magnitude > wingTipVotexSpeedThreshold)
            {
                if (!rightWingTipVortex.isPlaying)
                    rightWingTipVortex.Play();
            }
            else if(rightWingTipVortex.isPlaying)
            {
                rightWingTipVortex.Stop();
            }

            if (leftWingTipVortex != null && leftWingTipVortex.isPlaying)
            {
                ParticleSystem.MainModule main = leftWingTipVortex.main;
                main.startSpeed = leftTipWing.LocalVelocity.magnitude;
            }
            if (rightWingTipVortex != null && rightWingTipVortex.isPlaying)
            {
                ParticleSystem.MainModule main = rightWingTipVortex.main;
                main.startSpeed = rightTipWing.LocalVelocity.magnitude;
            }
        }

        private void UpdateVortexCloud()
        {
            if(leftTipWing.aoa > vortexCloudAoaThreshold && leftTipWing.LocalVelocity.magnitude > vortexCloudSpeedThreshold)
            {
                if (!isLeftVortexCloudPlaying)
                {
                    leftVortexCloud.Play();
                    isLeftVortexCloudPlaying = true;
                }
            }
            else if(isLeftVortexCloudPlaying)
            {
                leftVortexCloud.Stop();
                isLeftVortexCloudPlaying = false;
            }

            if(rightTipWing.aoa > vortexCloudAoaThreshold && rightTipWing.LocalVelocity.magnitude > vortexCloudSpeedThreshold)
            {
                if (!isRightVortexCloudPlaying)
                {
                    rightVortexCloud.Play();
                    isRightVortexCloudPlaying = true;
                }
            }
            else if(isRightVortexCloudPlaying)
            {
                rightVortexCloud.Stop();
                isRightVortexCloudPlaying = false;
            }
        }
    }
}
