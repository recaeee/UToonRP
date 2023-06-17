using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    private const string bufferName = "Post FX";

    private CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;
    private CullingResults cullingResults;

    struct VolumeLightData
    {
        public int lightIndex;
        public int VolumeIndex;
        public VolumeLightRenderer VolumeLightRenderer;
    }

    private List<VolumeLightData> volumeLightDatas = new List<VolumeLightData>();

    //后处理各Pass名
    enum Pass
    {
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        BloomPrefilter,
        Copy
    }

    private int tempRTId = Shader.PropertyToID("_TempRT");
    private Hashtable doubleBufferSystems = new Hashtable();

    private DoubleBufferSystem doubleBufferSystem
    {
        set
        {
            doubleBufferSystems[camera] = value;
        }
        get
        {
            return doubleBufferSystems[camera] as DoubleBufferSystem;
        }
    }

    //Bloom
    //Bloom降采样最大次数
    private const int maxBloomPyramidLevels = 16;

    //第一张降采样RT，后续直接数组索引++
    private int bloomPyramidId;

    private int bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        //Bloom半分辨率初始RT
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        //亮度阈值
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        //Bloom强度
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2");

    //控制后处理堆栈是否激活，如果Settings资源为null，则跳过后处理阶段
    public bool IsActive => settings != null;

    public PostFXStack()
    {
        //构造时连续请求所有BloomPyramid标识符
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings,
        CullingResults cullingResults, int sourceId, int depthBufferId)
    {
        this.context = context;
        this.camera = camera;
        //只对Game和Scene摄像机起作用
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        //对不同Scene窗口摄像机提供开关
        ApplySceneViewState();

        if (doubleBufferSystem == null)
        {
            doubleBufferSystem = new DoubleBufferSystem();
            doubleBufferSystem.Create(sourceId, depthBufferId, camera.pixelWidth, camera.pixelHeight);
        }
        
        this.cullingResults = cullingResults;
    }

    public void Render(int sourceId)
    {
        if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
        {
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
            return;
        }
        //用单个三角面片实现后处理
        
        //Full Screen Volume Light
        DoFullScreenVolumeLight();
        //FXAA
        DoFxaa();
        //Bloom
        DoBloom();
        context.ExecuteCommandBuffer(buffer);
        if (doubleBufferSystem.FrontColorId != sourceId)
        {
            doubleBufferSystem.Swap();
        }
        
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, int pass, Material material,
        RenderBufferLoadAction loadAction = RenderBufferLoadAction.DontCare,
        RenderBufferStoreAction storeAction = RenderBufferStoreAction.Store, bool clear = false)
    {
        if (material == null)
        {
            Debug.LogError("Post Processing Error: Material is null");
            return;
        }

        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(to, loadAction, storeAction);
        if (clear)
        {
            buffer.ClearRenderTarget(false, true, Color.clear);
        }
        buffer.DrawProcedural(Matrix4x4.identity, material, (int)pass, MeshTopology.Triangles, 3);
    }

    #region Bloom

    //Bloom后处理
    void DoBloom()
    {
        buffer.BeginSample("Bloom");
        //获取Bloom配置
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        //将相机的各维度像素数减半
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;

        //是否需要阻断Bloom
        if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
            height < bloom.downScaleLimit * 2 || width < bloom.downScaleLimit * 2)
        {
            Draw(doubleBufferSystem.FrontColorId, BuiltinRenderTextureType.CameraTarget, (int)Pass.Copy, settings.Material);
            buffer.EndSample("Bloom");
            return;
        }

        RenderTextureFormat format = RenderTextureFormat.Default;
        //计算亮度阈值
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);
        //初始状态为半分辨率
        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        Draw(doubleBufferSystem.FrontColorId, bloomPrefilterId, (int)Pass.BloomPrefilter, settings.Material);
        width /= 2;
        height /= 2;
        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
        //生成所有Pyramid
        int i;
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downScaleLimit || width < bloom.downScaleLimit)
            {
                break;
            }

            //构造中间RT，用于存储横向高斯滤波结果
            int midId = toId - 1;
            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
            //横向
            Draw(fromId, midId, (int)Pass.BloomHorizontal, settings.Material);
            //纵向
            Draw(midId, toId, (int)Pass.BloomVertical, settings.Material);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }

        //释放半分辨率RT
        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        //是否使用双三次上采样
        buffer.SetGlobalFloat(bloomBicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
        //强度，在上采样时混合权重为1
        buffer.SetGlobalFloat(bloomIntensityId, 1f);
        //叠加不同Pyramid颜色——上采样
        if (i > 1)
        {
            //先释放最上层的HorizonMidRT
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            //释放Pyramid内存
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, (int)Pass.BloomCombine, settings.Material);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }

        buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
        buffer.SetGlobalTexture(fxSource2Id, doubleBufferSystem.FrontColorId);
        //最后叠加时，使用intensity作为混合系数
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, (int)Pass.BloomCombine, settings.Material);
        buffer.ReleaseTemporaryRT(fromId);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        buffer.EndSample("Bloom");
    }

    #endregion


    #region Full Screen Volume Light

    private int cameraDepthTexId = Shader.PropertyToID("_CameraDepthTex"),
        spotBoundaryPlanesId = Shader.PropertyToID("_SpotBoundaryPlanes"),
        //spotVolumeLightInfo: x:lightIndex y:planesCount
        spotVolumeLightInfoId = Shader.PropertyToID("_SpotVolumeLightInfo"),
        _DepthParams = Shader.PropertyToID("_DepthParams");
    
    void DoFullScreenVolumeLight()
    {
        buffer.BeginSample("Volume Light");
        PostFXSettings.FullScreenVolumeLightSettings fullScreenVolumeLight = settings.FullScreenVolumeLight;
        if (!fullScreenVolumeLight.enable || settings.fullScreenVolumeLightMaterial == null ||
            settings.MeshVolumeLightMaterial == null)
        {
            buffer.EndSample("Volume Light");
            return;
        }

        //Mesh Volume Light
        //传递聚光灯锥体范围
        volumeLightDatas.Clear();
        int spotLightIndex = 0;
        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            var light = cullingResults.visibleLights[i];
            if (light.light.TryGetComponent(out VolumeLightRenderer volumeLightRenderer))
            {
                volumeLightDatas.Add(new VolumeLightData()
                {
                    lightIndex = spotLightIndex++,
                    VolumeIndex = volumeLightDatas.Count,
                    VolumeLightRenderer = volumeLightRenderer
                });
            }
        }
        
        buffer.SetRenderTarget(doubleBufferSystem.FrontColorId, RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store);

        buffer.SetGlobalTexture(cameraDepthTexId, doubleBufferSystem.DepthId);
        var tanFov = Mathf.Tan(camera.fieldOfView / 2 * Mathf.Deg2Rad);
        var tanFovWidth = tanFov * camera.aspect;
        buffer.SetGlobalVector(_DepthParams, new Vector2(tanFovWidth, tanFov));
        
        for (int i = 0; i < volumeLightDatas.Count; i++)
        {
            var volumeLightData = volumeLightDatas[i];
            if (!volumeLightData.VolumeLightRenderer.enabled)
            {
                continue;
            }

            var spotBoundaryPlanes = volumeLightData.VolumeLightRenderer.GetVolumeBound();
            buffer.SetGlobalVectorArray(spotBoundaryPlanesId, spotBoundaryPlanes);
            buffer.SetGlobalVector(spotVolumeLightInfoId,
                new Vector4(volumeLightData.lightIndex, spotBoundaryPlanes.Length));
            buffer.DrawMesh(volumeLightData.VolumeLightRenderer.VolumeMesh,
                volumeLightData.VolumeLightRenderer.transform.localToWorldMatrix, settings.MeshVolumeLightMaterial, 0,
                0);
        }
        
        //Full Screen Volume Light
        // buffer.SetGlobalTexture(fxSource2Id, doubleBufferSystem.FrontColorId);
        // Draw(doubleBufferSystem.FrontColorId, doubleBufferSystem.BackColorId, 0, settings.fullScreenVolumeLightMaterial);
        // doubleBufferSystem.Swap();
        
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        buffer.EndSample("Volume Light");
    }

    #endregion

    #region FXAA

    static class FxaaShaderConstants
    {
        public static readonly int _MainTex = Shader.PropertyToID("_MainTex"),
            _MainTex_TexelSize = Shader.PropertyToID("_MainTex_TexelSize"),
            _QualitySettings = Shader.PropertyToID("_QualitySettings");

    }

    void DoFxaa()
    {
        buffer.BeginSample("FXAA");
        PostFXSettings.FxaaSettings fxaa = settings.Fxaa;
        if (!fxaa.enable || settings.fxaaMaterial == null)
        {
            buffer.EndSample("FXAA");
            return;
        }

        buffer.SetGlobalTexture(FxaaShaderConstants._MainTex, doubleBufferSystem.FrontColorId);
        buffer.SetGlobalVector(FxaaShaderConstants._MainTex_TexelSize,
            new Vector4(1f / doubleBufferSystem.colorDesc.width, 1f / doubleBufferSystem.colorDesc.height,
                doubleBufferSystem.colorDesc.width, doubleBufferSystem.colorDesc.height));
        buffer.SetGlobalVector(FxaaShaderConstants._QualitySettings, new Vector4(0.0f, 0.333f, 0.0833f));
        Draw(doubleBufferSystem.FrontColorId, doubleBufferSystem.BackColorId, 0, settings.fxaaMaterial);
        doubleBufferSystem.Swap();
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        buffer.EndSample("FXAA");
    }

    #endregion
}