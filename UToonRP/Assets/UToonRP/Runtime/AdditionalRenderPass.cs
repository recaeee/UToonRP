using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum PassExecuteTime
{
    BeforeMainPass
}

[System.Serializable]
// [CreateAssetMenu(menuName = "Rendering/Additional Render Pass")]
public partial class AdditionalRenderPass : ScriptableObject
{
    [Header("Render Pass Name")]
    public string passName = null;
    [Header("Pass Execute Time")]
    public PassExecuteTime passExecuteTime;
    [Tooltip("Which shader passes want to render.")]
    public List<string> shaderPasses = null;

    [Tooltip("Camera used to render")] public Camera camera = null;

    protected CommandBuffer buffer = null;

    private List<ShaderTagId> shaderTagIds = null;
    

    protected List<ShaderTagId> ShaderTagIds
    {
        get
        {
            if (shaderTagIds == null)
            {
                shaderTagIds = new List<ShaderTagId>();
                for (int i = 0; i < shaderPasses.Count; i++)
                {
                    shaderTagIds.Add(new ShaderTagId(shaderPasses[i]));
                }
            }

            return shaderTagIds;
        }
    }

    protected ScriptableRenderContext context;
    // private Camera camera;
    protected CullingResults cullingResults;

    private void OnEnable()
    {
        buffer = new CommandBuffer()
        {
            name = passName
        };
    }

    public virtual void CameraSetup()
    {
        
    }

    public virtual void Render(ScriptableRenderContext context, bool useDynamicBatching = false,
        bool useGPUInstancing = false, bool useLightsPerObject = false)
    {
        this.context = context;
        
        CameraSetup();

        if(!Cull())
        {
            return;
        }
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        Setup();
        RenderObjects(useDynamicBatching, useGPUInstancing, useLightsPerObject);
        EndRender();
        buffer.EndSample(SampleName);
        ExecuteBuffer();
    }

    //可覆写，默认是设置摄像机
    public virtual void Setup()
    {
        context.SetupCameraProperties(camera);
        buffer.SetRenderTarget(camera.targetTexture);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        // buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    //可覆写，默认是绘制
    public virtual void RenderObjects(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
    {
        //是否使用每物体光源数据
        PerObjectData lightsPerObjectFlags =
            useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        //决定物体绘制顺序是正交排序还是基于深度排序的配置
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        //决定摄像机支持的Shader Pass和绘制顺序等的配置
        var drawingSettings = new DrawingSettings(ShaderTagId.none, sortingSettings)
        {
            //启用动态批处理
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            //传递场景中所有参与GI的物体在光照贴图上的UV、每个物体的光照探针信息、遮蔽探针、大型物体的LPPV信息、阴影遮罩信息、遮挡LPPV、反射探针、每物体光源信息
            perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe | PerObjectData.OcclusionProbe | PerObjectData.LightProbeProxyVolume | PerObjectData.ShadowMask | PerObjectData.OcclusionProbeProxyVolume
             | PerObjectData.ReflectionProbes | lightsPerObjectFlags
        };
        for (int i = 0; i < ShaderTagIds.Count; i++)
        {
            drawingSettings.SetShaderPassName(i + 1, ShaderTagIds[i]);
        }
        //决定过滤哪些Visible Objects的配置，包括支持的RenderQueue等
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        //渲染CullingResults内不透明的VisibleObjects
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        //添加“绘制天空盒”指令，DrawSkybox为ScriptableRenderContext下已有函数，这里就体现了为什么说Unity已经帮我们封装好了很多我们要用到的函数，SPR的画笔~
        context.DrawSkybox(camera);
        //渲染透明物体
        //设置绘制顺序为从后往前
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        //注意值类型
        drawingSettings.sortingSettings = sortingSettings;
        //过滤出RenderQueue属于Transparent的物体
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        //绘制透明物体
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    public virtual void EndRender()
    {
        
    }
    
    
    bool Cull()
    {
        //获取摄像机用于剔除的参数
        if (camera != null && camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            cullingResults = context.Cull(ref p);
            return true;
        }

        return false;
    }
    
    protected void ExecuteBuffer()
    {
        //我们默认在CommandBuffer执行之后要立刻清空它，如果我们想要重用CommandBuffer，需要针对它再单独操作（不使用ExecuteBuffer），舒服的方法给常用的操作~
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
