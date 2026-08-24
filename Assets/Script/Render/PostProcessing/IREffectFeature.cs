using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering.Universal;
using UnityEngine;

namespace AeroSim.Render
{
    public class IREffectFeature : ScriptableRendererFeature
    {
        public IREffectPass pass;

        public override void Create()
        {
            pass = new IREffectPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(pass);
        }
    }
}
