using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlanarReflectionRenderer : MonoBehaviour
{
    public PlanarReflectionPass pass;
    private void OnEnable()
    {
        if (!pass.transform)
        {
            pass.transform = transform;
        }
    }

    private void OnDisable()
    {
        pass.transform = null;
    }
}
