using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Planar Reflection Pass")]
public class PlanarReflectionPass : AdditionalRenderPass
{
    public Material reflectionMaterial = null;
    public Transform transform;
    public int reflectionRTSize = 1024;
    
    private Hashtable reflectionCameras = new Hashtable();
    private Hashtable reflectionRTs = new Hashtable();
    
    public override void CameraSetup()
    {
        Camera cam = Camera.current;
        if (!cam)
        {
            cam = Camera.main;
        }
        if (!cam || !transform)
        {
            return;
        }

        Camera reflectionCamera;
        RenderTexture reflectionRT;

        //配置反射摄像机
        CreateMirrorObjects(cam, out reflectionCamera, out reflectionRT);
        //同步Editor下摄像机参数
        UpdateCameraParams(cam, reflectionCamera);
        reflectionCamera.targetTexture = reflectionRT;
        reflectionCamera.enabled = false;
        //计算反射变换函数
        var reflectionM = CalculateReflectionMatrix(transform.up, transform.position);
        reflectionCamera.worldToCameraMatrix = cam.worldToCameraMatrix * reflectionM;
        //将背面裁剪反过来，仅改变了顶点，法向量需要再绕序反向，
        // GL.invertCulling = true;
        reflectionMaterial.SetFloat("_Cull", 1);
        camera = reflectionCamera;
    }

    public override void Setup()
    {
        context.SetupCameraProperties(camera);
        buffer.SetRenderTarget(camera.targetTexture);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, true,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        // buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    public override void RenderObjects(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
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


    public override void EndRender()
    {
        foreach (var reflectionRT in reflectionRTs)
        {
            RenderTexture.ReleaseTemporary(reflectionRT as RenderTexture);
        }
        
        // GL.invertCulling = false;
        reflectionMaterial.SetTexture("_ReflectionTex", reflectionRTs[camera] as RenderTexture);
        reflectionMaterial.SetFloat("_Cull", 2);
    }

    private void CreateMirrorObjects(Camera currentCamera, out Camera reflectionCamera, out RenderTexture reflectionRT)
    {
        reflectionCamera = null;
        reflectionCamera = reflectionCameras[currentCamera] as Camera;
        if (!reflectionCamera)
        {
            GameObject go =
                new GameObject("Reflection Camera id" + GetInstanceID() + " for " + currentCamera.name);
            reflectionCamera = go.AddComponent<Camera>();
            reflectionCamera.enabled = false;
            reflectionCamera.CopyFrom(currentCamera);
            // go.hideFlags = HideFlags.HideAndDontSave;
            reflectionCameras[currentCamera] = reflectionCamera;
        }

        reflectionRT = null;
        reflectionRT = reflectionRTs[reflectionCamera] as RenderTexture;
        if (!reflectionRT)
        {
            reflectionRT = RenderTexture.GetTemporary(reflectionRTSize, reflectionRTSize, 16);
            reflectionRT.name = "_ReflectionTex_" + GetInstanceID();
            reflectionRTs[reflectionCamera] = reflectionRT;
        }
    }

    Matrix4x4 CalculateReflectionMatrix(Vector3 normal, Vector3 positionOnPlane)
    {
        var d = -Vector3.Dot(normal, positionOnPlane);
        var reflectionM = Matrix4x4.identity;
        reflectionM.m00 = 1 - 2 * normal.x * normal.x;
        reflectionM.m01 = -2 * normal.x * normal.y;
        reflectionM.m02 = -2 * normal.x * normal.z;
        reflectionM.m03 = -2 * d * normal.x;

        reflectionM.m10 = -2 * normal.x * normal.y;
        reflectionM.m11 = 1 - 2 * normal.y * normal.y;
        reflectionM.m12 = -2 * normal.y * normal.z;
        reflectionM.m13 = -2 * d * normal.y;

        reflectionM.m20 = -2 * normal.x * normal.z;
        reflectionM.m21 = -2 * normal.y * normal.z;
        reflectionM.m22 = 1 - 2 * normal.z * normal.z;

        return reflectionM;
    }

    private void UpdateCameraParams(Camera srcCamera, Camera destCamera)
    {
        if (destCamera == null || srcCamera == null)
        {
            return;
        }

        destCamera.clearFlags = srcCamera.clearFlags;
        destCamera.backgroundColor = srcCamera.backgroundColor;
        destCamera.farClipPlane = srcCamera.farClipPlane;
        destCamera.nearClipPlane = srcCamera.nearClipPlane;
        destCamera.orthographic = srcCamera.orthographic;
        destCamera.fieldOfView = srcCamera.fieldOfView;
        destCamera.aspect = srcCamera.aspect;
        destCamera.orthographicSize = srcCamera.orthographicSize;
    }
}
