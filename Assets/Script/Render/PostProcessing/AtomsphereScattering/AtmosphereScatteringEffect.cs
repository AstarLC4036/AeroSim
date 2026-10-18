using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.Render.PostProcessing
{
    [VolumeComponentMenu("AeroSim/Atomsphere Scattering")]
    public class AtmosphereScatteringEffect : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter enabled = new BoolParameter(false);

        [Header("Floating Origin(Auto)")]
        public Vector3Parameter worldOffset = new Vector3Parameter(Vector3.zero);
        [Header("Geometry")]
        public Vector3Parameter center = new Vector3Parameter(Vector3.zero);
        public FloatParameter height = new FloatParameter(10);
        public FloatParameter radius = new FloatParameter(1);
        public ClampedFloatParameter exposure = new ClampedFloatParameter(1, 0, 50);
        [Header("Rayleigh Scattering")]
        [Tooltip("Reference height in kilometres")]
        public FloatParameter rayleighHeight = new FloatParameter(8);
        public Vector3Parameter rayleighScatteringCoeff = new Vector3Parameter(new Vector3(5.5f * 0.001f, 13.0f * 0.001f, 22.4f * 0.001f));
        [Header("Mie Scattering")]
        [Tooltip("Reference height in kilometres")]
        public FloatParameter mieHeight = new FloatParameter(12);
        public FloatParameter mieScatteringCoeff = new FloatParameter(21.0f * 0.001f);
        public ClampedFloatParameter anisotropicParam = new ClampedFloatParameter(0.76f, 0, 1);
        [Header("Ray Marching")]
        public IntParameter viewSteps = new IntParameter(8);
        public IntParameter lightSteps = new IntParameter(2);

        public bool IsActive() => enabled.value;
    }
}
