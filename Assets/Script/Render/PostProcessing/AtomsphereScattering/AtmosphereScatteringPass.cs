using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AeroSim.Utils;

namespace AeroSim.Render.PostProcessing.AtomsphereScattering
{
    public class AtmosphereScatteringPass : ScriptableRenderPass
    {
        public static readonly string k_RenderTag = "Atomsphere Scattering";

        public static readonly int mainTexId = Shader.PropertyToID("_MainTex");
        public static readonly int tempTargetId = Shader.PropertyToID("_IRTempTarget");

        // Shader params
        public static readonly int worldOffsetId = Shader.PropertyToID("_WorldOffset");
        public static readonly int centerId = Shader.PropertyToID("_Center");
        public static readonly int radiusId = Shader.PropertyToID("_Radius");
        public static readonly int heightId = Shader.PropertyToID("_Height");
        public static readonly int exposureId = Shader.PropertyToID("_Exposure");
        public static readonly int rayleighHeightId = Shader.PropertyToID("_RayleighHeight");
        public static readonly int mieHeightId = Shader.PropertyToID("_MieHeight");
        public static readonly int mieAnisotropicId = Shader.PropertyToID("_MieG");
        public static readonly int rayleighScatteringCoeffId = Shader.PropertyToID("_RayleighScatteringCoeff");
        public static readonly int mieScatteringCoeffId = Shader.PropertyToID("_MieScatteringCoeff");
        public static readonly int ozoneCoeffId = Shader.PropertyToID("_OzoneCoeff");
        public static readonly int viewStepsId = Shader.PropertyToID("_ViewSteps");
        public static readonly int lightStepsId = Shader.PropertyToID("_LightSteps");

        public AtmosphereScatteringEffect effect;
        public RenderTargetIdentifier targetIdentifier;
        public Material effectMat;

        public AtmosphereScatteringPass(RenderPassEvent e)
        {
            renderPassEvent = e;

            Shader effectShader = Shader.Find("AeroSim/PostEffect/AtomsohereScattering");

            if (effectShader == null)
            {
                Debug.LogWarning("Cannot find target shader");
            }

            effectMat = CoreUtils.CreateEngineMaterial(effectShader);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);

            targetIdentifier = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!renderingData.postProcessingEnabled)
                return;

            VolumeStack stack = VolumeManager.instance.stack;
            effect = stack.GetComponent<AtmosphereScatteringEffect>();

            if (effect == null || !effect.IsActive())
                return;

            CommandBuffer cmd = CommandBufferPool.Get(k_RenderTag);
            Render(cmd, ref renderingData);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Render(CommandBuffer cmd, ref RenderingData data)
        {
            ref CameraData cameraData = ref data.cameraData;
            var descriptor = cameraData.cameraTargetDescriptor;
            RenderTargetIdentifier source = targetIdentifier;
            int destination = tempTargetId;

            int w = cameraData.camera.scaledPixelWidth;
            int h = cameraData.camera.scaledPixelHeight;

            // update paramers
            UpdateParam();

            int shaderPass = 0;

            cmd.SetGlobalTexture(mainTexId, source);
            cmd.GetTemporaryRT(destination, descriptor, FilterMode.Point);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, effectMat, shaderPass);
            cmd.ReleaseTemporaryRT(tempTargetId);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            base.FrameCleanup(cmd);
        }

        public void UpdateParam()
        {
            //effectMat.SetVector(worldOffsetId, effect.worldOffset.value);
            effectMat.SetVector(centerId, effect.center.value);
            effectMat.SetVector(rayleighScatteringCoeffId, effect.rayleighScatteringCoeff.value);
            effectMat.SetFloat(radiusId, effect.radius.value);
            effectMat.SetFloat(exposureId, effect.sunIntensity.value);
            effectMat.SetFloat(rayleighHeightId, effect.rayleighHeight.value);
            effectMat.SetFloat(mieHeightId, effect.mieHeight.value);
            effectMat.SetFloat(mieScatteringCoeffId, effect.mieScatteringCoeff.value);
            effectMat.SetVector(ozoneCoeffId, effect.ozoneCoeff.value);
            effectMat.SetFloat(mieAnisotropicId, effect.anisotropicParam.value);
            effectMat.SetFloat(heightId, effect.height.value);
            effectMat.SetInt(viewStepsId, effect.viewSteps.value);
            effectMat.SetInt(lightStepsId, effect.lightSteps.value);
            effectMat.SetVector(worldOffsetId, -FloatingOrigin.origin);
        }
    }
}
