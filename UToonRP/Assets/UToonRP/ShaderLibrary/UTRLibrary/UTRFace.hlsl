#ifndef UTR_FACE_INCLUDE
#define UTR_FACE_INCLUDE

//脸部Diffuse部分
float3 GetFaceDiffuse(UTRSurface surface, Light light, float4 lightInfoMapValue)
{
    float3 diffuse = 0.0;
    //先获取面部的整体朝向（世界空间）
    float3 faceForwardDir = TransformObjectToWorldDir(float3(0,0,1));
    //根据世界空间上方向判断光源方向在faceForwardDir的左边还是右边(lightLeft意为光源是否照射到左脸）
    bool lightLeft = cross(faceForwardDir, light.direction).y < 0 ? true : false;
    float3 lightDir = light.direction;
    lightDir.y = 0.0;
    lightDir = normalize(lightDir);
    //计算整个面部的反射出的Diffuse能量比例
    float faceLambert = dot(faceForwardDir, lightDir);
    if(faceLambert < 0)
    {
        //当光源背向面部，整个面部都在阴影中
        float3 ramp = SAMPLE_TEXTURE2D(_SkinRampMap, sampler_SkinRampMap, float2(0.5, 0.05));
        diffuse = ramp;
        return surface.color * diffuse;
    }
    else
    {
        //采样Face SDF Map
        float2 uv = surface.baseUV * _FaceSDFMap_ST.xy + _FaceSDFMap_ST.zw;
        if(!lightLeft)
        {
            uv.x = 1 - uv.x;
        }
        float sdf = SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_FaceSDFMap, uv);
        //光照衰减度
        // float lightAttenuation = sdf < 1.0 - faceLambert ? 0.95 : 0.05;
        float lightAttenuation = 0.05 + 0.9 * (1 - smoothstep(-_FaceSmoothStrength, _FaceSmoothStrength, sdf + faceLambert - 1.0));
        //采样Ramp贴图
        #if defined(_UTR_RAMP)
            float3 ramp = SAMPLE_TEXTURE2D(_SkinRampMap, sampler_SkinRampMap, float2(0.5, 1 - lightAttenuation));
            diffuse = ramp;
        #elif defined(_UTR_DETAIL_RAMP)
        //仿原神渲染
        //映射halfLambert 0~0.5扩大，0.5以上直接1
        float halfLambert = smoothstep(0, 0.5, 1 - lightAttenuation);
        // 根据lightMapValue.a确定使用的Ramp带
        float rampY = _IsDay > 0 ? lightInfoMapValue.a * 0.45 + 0.55 : lightInfoMapValue.a * 0.45;
        float3 ramp = SAMPLE_TEXTURE2D(_DetailRampMap, sampler_DetailRampMap, float2(halfLambert, rampY));
        diffuse = ramp;
        #else
            diffuse = lerp(_ShadowTone, _LitTone, 1 - lightAttenuation);
        #endif

        return surface.color * diffuse;
    }
}

#endif
