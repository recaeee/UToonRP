using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Light))]
public class VolumeLightRenderer : MonoBehaviour
{
    public Light Light { set; get; }
    private Vector4[] planes = null;

    private void Awake()
    {
        Light = GetComponent<Light>();
        planes = new Vector4[6];
        // VolumeMesh = new Mesh();
        Reset();
    }

    private void Update()
    {
        if (Light.type != LightType.Spot)
            return;
        UpdateMesh();
    }

    public Vector4[] GetVolumeBound()
    {
        if (Light.type != LightType.Spot)
        {
            return null;
        }

        
        Matrix4x4 viewProjection = Matrix4x4.identity;
        viewProjection = Matrix4x4.Perspective(Light.spotAngle, 1, 0.03f, Light.range)
                         * Matrix4x4.Scale(new Vector3(1, 1, -1)) * Light.transform.worldToLocalMatrix;
        var m0 = viewProjection.GetRow(0);
        var m1 = viewProjection.GetRow(1);
        var m2 = viewProjection.GetRow(2);
        var m3 = viewProjection.GetRow(3);
        planes[0] = -(m3 + m0);
        planes[1] = -(m3 - m0);
        planes[2] = -(m3 + m1);
        planes[3] = -(m3 - m1);
        //ignore near
        planes[4] = -(m3 - m2);
        
        return planes;
    }
    
    //Draw Gizmos
    private Mesh volumeMesh;

    public Mesh VolumeMesh
    {
        get
        {
            if (!volumeMesh)
            {
                volumeMesh = new Mesh();
            }

            return volumeMesh;
        }

    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireMesh(VolumeMesh, 0, transform.position, transform.rotation, transform.lossyScale);
    }

    void Reset()
    {
        VolumeMesh.vertices = new Vector3[]
        {
            new Vector3(-1, -1, -1),
            new Vector3(-1,  1, -1),
            new Vector3( 1,  1, -1),
            new Vector3( 1, -1, -1),
            new Vector3(-1, -1,  1),
            new Vector3(-1,  1,  1),
            new Vector3( 1,  1,  1),
            new Vector3( 1, -1,  1),
        };
        VolumeMesh.triangles = new int[]
        {
            0,1,2, 0,2,3,
            0,4,5, 0,5,1,
            1,5,6, 1,6,2,
            2,6,7, 2,7,3,
            0,3,7, 0,7,4,
            4,6,5, 4,7,6,
        };
        UpdateMesh();
    }
    
    void UpdateMesh()
    {
        if(Light.type == LightType.Spot)
        {
            var tanFOV = Mathf.Tan(Light.spotAngle / 2 * Mathf.Deg2Rad);
            var verts = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(-tanFOV, -tanFOV, 1) * Light.range,
                new Vector3(-tanFOV,  tanFOV, 1) * Light.range,
                new Vector3( tanFOV,  tanFOV, 1) * Light.range,
                new Vector3( tanFOV, -tanFOV, 1) * Light.range,
            };
            VolumeMesh.Clear();
            VolumeMesh.vertices = verts;
            VolumeMesh.triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 1,
                1, 4, 3,
                1, 3, 2,
            };
            VolumeMesh.RecalculateNormals();
        }
    }
}
