using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AeroSim.Render
{
    public class IREffect : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter enabled = new BoolParameter(false); 
        [Range(0,100)]
        public IntParameter strength = new IntParameter(10);
        public FloatParameter blackhot = new FloatParameter(0);
        public FloatParameter contrast = new FloatParameter(1.5f);
        public FloatParameter noise = new FloatParameter(0.05f);
        public FloatParameter noiseDelta = new FloatParameter(0.05f);

        public bool IsActive() => enabled.value;
    }
}
