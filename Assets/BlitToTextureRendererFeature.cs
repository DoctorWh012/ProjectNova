using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// RenderFeature that blits the current content of the screen to a render texture
/// </summary>
internal class ColorBlitPass : ScriptableRenderPass
{
    ProfilingSampler m_ProfilingSampler = new ProfilingSampler("ColorBlit");
<<<<<<< HEAD
    RTHandle m_CameraColorTarget;
=======
    RenderTargetIdentifier m_CameraColorTarget;
>>>>>>> e0f72b9182fdb57dddbf09ee3a28ffa6af6518e2
    float m_Intensity;
    RenderTexture m_renderTexture = null;

    public ColorBlitPass(RenderTexture renderTexture, RenderPassEvent renderEvent)
    {
        renderPassEvent = renderEvent;
        m_renderTexture = renderTexture;
    }

<<<<<<< HEAD
    public void SetTarget(RTHandle colorHandle, float intensity)
=======
    public void SetTarget(RenderTargetIdentifier colorHandle, float intensity)
>>>>>>> e0f72b9182fdb57dddbf09ee3a28ffa6af6518e2
    {
        m_CameraColorTarget = colorHandle;
        m_Intensity = intensity;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        ConfigureTarget(m_CameraColorTarget);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        if (cameraData.camera.cameraType != CameraType.Game)
            return;

        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            //Blitter.BlitCameraTexture(cmd, m_CameraColorTarget, m_renderTexture, m_Material, 0);
            //Graphics.Blit(m_CameraColorTarget, m_renderTexture);
            Blit(cmd, m_CameraColorTarget, m_renderTexture);
        }
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }
}

internal class BlitToTextureRendererFeature : ScriptableRendererFeature
{
    public float m_Intensity = 0;
    public RenderTexture m_renderTexture;
    public RenderPassEvent m_event;

    ColorBlitPass m_RenderPass = null;

<<<<<<< HEAD
    public override void AddRenderPasses(ScriptableRenderer renderer,
                                    ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(m_RenderPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer,
                                        in RenderingData renderingData)
=======
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
>>>>>>> e0f72b9182fdb57dddbf09ee3a28ffa6af6518e2
    {
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            // Calling ConfigureInput with the ScriptableRenderPassInput.Color argument
            // ensures that the opaque texture is available to the Render Pass.
            m_RenderPass.ConfigureInput(ScriptableRenderPassInput.Color);
<<<<<<< HEAD
            m_RenderPass.SetTarget(renderer.cameraColorTargetHandle, m_Intensity);
        }
=======
            m_RenderPass.SetTarget(renderer.cameraColorTarget, m_Intensity);
        }

        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(m_RenderPass);
>>>>>>> e0f72b9182fdb57dddbf09ee3a28ffa6af6518e2
    }

    public override void Create()
    {
        m_RenderPass = new ColorBlitPass(m_renderTexture, m_event);
    }

    protected override void Dispose(bool disposing)
    {
        //CoreUtils.Destroy(m_Material);
    }
}