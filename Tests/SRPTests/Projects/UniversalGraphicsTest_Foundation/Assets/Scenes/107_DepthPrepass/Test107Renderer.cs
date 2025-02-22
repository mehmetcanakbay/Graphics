using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    // This test checks that depth buffer is not cleared by ScriptableRenderer.ExecuteRenderPass the second time it is bound in the frame.
    // Cubes in the scene use a SimpleLit shader with ZWrite=off - therefore they will not write to depth during the forward pass.
    // If the depth buffer is correct cubes will look like they intersect ; if the depthbuffer is incorrect cubes will appear one in front of the other.
    public sealed class Test107Renderer : ScriptableRenderer
    {
        StencilState m_DefaultStencilState;

        DepthOnlyPass m_DepthPrepass;
        DrawObjectsPass m_RenderOpaqueForwardPass;
        FinalBlitPass m_FinalBlitPass;

        RTHandle m_CameraColor;
        RTHandle m_CameraDepth;

        Material m_BlitMaterial;

        string m_profilerTag = "Test 107 Renderer";

        public Test107Renderer(Test107RendererData data) : base(data)
        {
            m_DefaultStencilState = new StencilState();

            m_DepthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrePasses, RenderQueueRange.opaque, -1 /*data.opaqueLayerMask*/);
            m_RenderOpaqueForwardPass = new DrawObjectsPass("Render Opaques", false, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, -1 /*data.opaqueLayerMask*/, m_DefaultStencilState, 0 /*stencilData.stencilReference*/);

            m_BlitMaterial = CoreUtils.CreateEngineMaterial(data.shaders.blitPS);
            m_FinalBlitPass = new FinalBlitPass(RenderPassEvent.AfterRendering, m_BlitMaterial);

            m_CameraColor = RTHandles.Alloc("_CameraColor", "_CameraColor");
            m_CameraDepth = RTHandles.Alloc("_CameraDepth", "_CameraDepth");
        }

        /// <inheritdoc />
        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_profilerTag);

            cmd.GetTemporaryRT(Shader.PropertyToID(m_CameraColor.name), 1280, 720);
            cmd.GetTemporaryRT(Shader.PropertyToID(m_CameraDepth.name), 1280, 720, 16);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            ConfigureCameraTarget(m_CameraColor.nameID, m_CameraDepth.nameID);

            // 1) Depth pre-pass
            m_DepthPrepass.Setup(renderingData.cameraData.cameraTargetDescriptor, m_CameraDepth);
            EnqueuePass(m_DepthPrepass);

            // 2) Forward opaque
            EnqueuePass(m_RenderOpaqueForwardPass); // will render to renderingData.cameraData.camera

            // 3) Final blit to the backbuffer
            m_FinalBlitPass.Setup(renderingData.cameraData.cameraTargetDescriptor, m_CameraColor);
            EnqueuePass(m_FinalBlitPass);
        }

        /// <inheritdoc />
        public override void FinishRendering(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(Shader.PropertyToID(m_CameraColor.name));
            cmd.ReleaseTemporaryRT(Shader.PropertyToID(m_CameraDepth.name));
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_BlitMaterial);
            m_FinalBlitPass.Dispose();
        }
    }
}
