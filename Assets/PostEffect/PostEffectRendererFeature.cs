using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PostEffectRendererFeature : ScriptableRendererFeature
{
    [SerializeField]
    private List<Material> _material;
    private PostEffectRenderPass _postEffectRenderPass;

    public override void Create()
    {
        _postEffectRenderPass = new PostEffectRenderPass(_material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_postEffectRenderPass);
    }
}