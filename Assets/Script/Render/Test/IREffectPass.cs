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
    public class IREffectPass : ScriptableRenderPass
    {
        public static readonly string k_RenderTag = "IR Render";

        public static readonly int mainTexId = Shader.PropertyToID("_MainTex");
        public static readonly int tempTargetId = Shader.PropertyToID("_IRTempTarget");
        public static readonly int strengthId = Shader.PropertyToID("_Strength");
        public static readonly int contrastId = Shader.PropertyToID("_Contrast");
        public static readonly int blackhotId = Shader.PropertyToID("_BlackHot");
        public static readonly int noiseId = Shader.PropertyToID("_NoiseAmount");
        public static readonly int noiseDeltaId = Shader.PropertyToID("_NoiseDelta");

        public IREffect effect;
        public RenderTargetIdentifier targetIdentifier;
        public Material effectMat;

        public IREffectPass(RenderPassEvent e)
        {
            renderPassEvent = e;

            Shader effectShader = Shader.Find("PostEffect/IR");

            if(effectShader == null)
            {
                Debug.LogWarning("Cannot find target shader");
            }

            effectMat = CoreUtils.CreateEngineMaterial(effectShader);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            targetIdentifier = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //base.Execute(context, ref renderingData);

            if (!renderingData.postProcessingEnabled)
                return;

            VolumeStack stack = VolumeManager.instance.stack;
            effect = stack.GetComponent<IREffect>();

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
            RenderTargetIdentifier source = targetIdentifier;
            int destination = tempTargetId;

            int w = cameraData.camera.scaledPixelWidth;
            int h = cameraData.camera.scaledPixelHeight;

            effectMat.SetInt(strengthId, effect.strength.value);
            effectMat.SetFloat(blackhotId, effect.blackhot.value);
            effectMat.SetFloat(contrastId, effect.contrast.value);
            effectMat.SetFloat(noiseId, effect.noise.value);
            effectMat.SetFloat(noiseDeltaId, effect.noiseDelta.value);

            int shaderPass = 0;

            cmd.SetGlobalTexture(mainTexId, source);
            cmd.GetTemporaryRT(destination, w, h, 0, FilterMode.Point, RenderTextureFormat.Default);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, effectMat, shaderPass);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            base.FrameCleanup(cmd);
        }
    }
}
