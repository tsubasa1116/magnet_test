using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class PostEffectRenderPass : ScriptableRenderPass
{
    private readonly List<Material> _material;

    public PostEffectRenderPass(List<Material> material, RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing)
    {
        _material = material;
        this.renderPassEvent = renderPassEvent;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var activeColorTexture = resourceData.activeColorTexture;
        TextureDesc desc = renderGraph.GetTextureDesc(activeColorTexture);
        desc.name = "PostEffectTempTexture";
        TextureHandle tempTexture = renderGraph.CreateTexture(desc);

        int loop;
        for (loop = 0; loop < _material.Count; loop++)
        {
            if (loop % 2 == 0)
            {
                RenderGraphUtils.BlitMaterialParameters param = new(activeColorTexture, tempTexture, _material[loop], 0);
                renderGraph.AddBlitPass(param);
            }
            else
            {
                RenderGraphUtils.BlitMaterialParameters param = new(tempTexture, activeColorTexture, _material[loop], 0);
                renderGraph.AddBlitPass(param);
            }

        }

        // 奇数ならコピーする
        if (loop % 2 == 1)
        {
            renderGraph.AddCopyPass(tempTexture, activeColorTexture);
        }

    }
}