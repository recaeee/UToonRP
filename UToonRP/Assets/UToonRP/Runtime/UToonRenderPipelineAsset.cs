using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class UToonRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private bool useDynamicBatching = true,
        useGPUInstancing = true,
        useSRPBatcher = true,
        useLightsPerObject = true;
    
    //后处理配置
    [SerializeField] PostFXSettings postFXSettings = default;
    //Shadow Map配置
    [SerializeField] private ShadowSettings shadows = default;
    //额外Pass
    [SerializeField] private List<AdditionalRenderPass> additionalRenderPasses;
    protected override RenderPipeline CreatePipeline()
    {
        return new UToonRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject, shadows, postFXSettings, additionalRenderPasses);
    }
}
