using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class PlanarReflection : MonoBehaviour
{
    private Camera reflectionCamera = null;
    private RenderTexture refectionRT = null;
    private static bool isReflectionCameraRendering = false;
    private Material reflectionMaterial = null;
    public Camera mainCamera;

    //如果挂载该脚本的对象可见，则为每个摄像机调用一次
    private void OnWillRenderObject()
    {
        Camera cam = Camera.current;
        if (!cam)
        {
            return;
        }
        if (isReflectionCameraRendering)
        {
            return;
        }

        isReflectionCameraRendering = true;

        if (reflectionCamera == null)
        {
            var go = new GameObject("Reflection Camera_" + cam.name);
            reflectionCamera = go.AddComponent<Camera>();
            reflectionCamera.CopyFrom(Camera.current);
        }
        

        if (refectionRT == null)
        {
            refectionRT = RenderTexture.GetTemporary(1024, 1024, 0);
            refectionRT.name = "_ReflectionTex";
        }
        
        //同步Editor下摄像机参数
        UpdateCameraParams(Camera.current, reflectionCamera);
        reflectionCamera.targetTexture = refectionRT;
        reflectionCamera.enabled = false;
        
        //计算反射变换函数
        var reflectionM = CalculateReflectionMatrix(transform.up, transform.position);

        reflectionCamera.worldToCameraMatrix = Camera.current.worldToCameraMatrix * reflectionM;
        
        //将背面裁剪反过来，仅改变了顶点，法向量需要再绕序反向，
        GL.invertCulling = true;
        // CustomRenderPipeline.customRenderPipeline.AddCameraBeforeMainRender(reflectionCamera);
        GL.invertCulling = false;

        if (reflectionMaterial == null)
        {
            var renderer = GetComponent<Renderer>();
            reflectionMaterial = renderer.sharedMaterial;
        }

        reflectionMaterial.SetTexture("_ReflectionTex", refectionRT);

        isReflectionCameraRendering = false;
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
