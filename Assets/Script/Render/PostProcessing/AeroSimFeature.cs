using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using AeroSim.Render.PostProcessing.AtomsphereScattering;

namespace AeroSim.Render
{
    public class AeroSimFeatures : ScriptableRendererFeature
    {
        public IREffectPass passInfared;
        public AtmosphereScatteringPass passAtmosphere;

        public override void Create()
        {
            passInfared = new IREffectPass(RenderPassEvent.BeforeRenderingPostProcessing);
            passAtmosphere = new AtmosphereScatteringPass(RenderPassEvent.AfterRenderingTransparents);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(passInfared);
            renderer.EnqueuePass(passAtmosphere);
        }
    }
}
