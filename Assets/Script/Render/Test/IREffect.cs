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

        public bool IsActive() => enabled.value;
    }
}
