using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class UToonRenderPipeline : RenderPipeline
{
    public static UToonRenderPipeline uToonRenderPipeline;
    //摄像机渲染器实例，用于管理所有摄像机的渲染
    private CameraRenderer renderer = new CameraRenderer();
    
    //批处理配置
    private bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
    //Shadow Map配置
    private ShadowSettings shadowSettings;
    //后处理配置
    private PostFXSettings postFXSettings;

    private List<AdditionalRenderPass> additionalRenderPasses;

    //构造函数，初始化管线的一些属性
    public UToonRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, ShadowSettings shadowSettings,
        PostFXSettings postFXSettings, List<AdditionalRenderPass> additionalRenderPasses)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        this.additionalRenderPasses = additionalRenderPasses;
        //配置SRP Batch
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        //设置光源颜色为线性空间
        GraphicsSettings.lightsUseLinearIntensity = true;
        
        //考虑点光源和聚光灯烘培时的衰减
        InitializeForEditor();

        uToonRenderPipeline = this;
    }
    
    //必须重写Render函数，渲染管线实例每帧执行Render函数
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        //提前渲染Pass
        BeforeMainPass(context, additionalRenderPasses);
        //按顺序渲染每个摄像机
        foreach (var camera in cameras)
        {
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing, useLightsPerObject, shadowSettings, postFXSettings);
        }
    }

    private void BeforeMainPass(ScriptableRenderContext context, List<AdditionalRenderPass> additionalRenderPasses)
    {
        for (int i = 0; i < additionalRenderPasses.Count; i++)
        {
            if (additionalRenderPasses[i] == null)
            {
                continue;
            }
            if (additionalRenderPasses[i].passExecuteTime == PassExecuteTime.BeforeMainPass)
            {
                additionalRenderPasses[i].Render(context);
            }
        }
    }
}
