//用来存放计算光照相关的方法
//HLSL编译保护机制
#ifndef UTR_LIGHTING_INCLUDE
#define UTR_LIGHTING_INCLUDE

//计算物体表面接收到的光能量
float3 IncomingLight(UTRSurface surface,Light light)
{
    //考虑了阴影带来的光源衰减
    return light.attenuation * light.color;
}

float3 GetUniversalDiffuse(UTRSurface surface, Light light)
{
    float3 diffuse = 0.0;
    float halfLambert = dot(surface.normal, light.direction) * 0.5 + 0.5;
    halfLambert *= light.attenuation;

    //通用
    //是否使用Ramp贴图
    #if defined(_UTR_RAMP)
    float3 ramp = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(0.5, saturate(halfLambert - _ShadowThreshold))).rgb;
    diffuse = ramp;
    #else
    //明暗二分，亮面暖色调，暗面冷色调
    //柔滑明暗边界
    float ramp = smoothstep(0, _ShadowSmoothStrength, halfLambert - _ShadowThreshold);
    diffuse = lerp(_ShadowTone, _LitTone, ramp);
    #endif
    
    return surface.color * diffuse;
}

float3 GetDiffuse(UTRSurface surface, Light light)
{
    //采样lightMap确定要使用的Ramp带
    float4 lightInfoMapValue = 0;
    #if defined(_UTR_DETAIL_RAMP)
        lightInfoMapValue = SAMPLE_TEXTURE2D(_LightInfoMap, sampler_LightInfoMap, surface.baseUV);
    #endif
    #if defined(_UTR_FACE)
        return GetFaceDiffuse(surface, light, lightInfoMapValue);
    #elif defined(_UTR_BODY_SKIN)
        return GetBodySkinDiffuse(surface, light, lightInfoMapValue);
    #elif defined(_UTR_CLOTH)
        return GetClothDiffuse(surface, light, lightInfoMapValue);
    #endif

    return GetUniversalDiffuse(surface, light);
}

float3 GetFresnel(UTRSurface surface)
{
    //计算菲涅尔反射强度,物体表面法线和观察方向越垂直，菲涅尔强度越大
    float fresnelStrength = surface.fresnelStrength * Pow4(1.0 - saturate(abs(dot(surface.normal,surface.viewDirection))));
    float rim = smoothstep(0, _FresnelSmoothStrength, fresnelStrength - _FresnelThreshold);
    return surface.color * rim;
}

//新增的GetLighting方法，传入surface和light，返回真正的光照计算结果，即物体表面最终反射出的RGB光能量
float3 GetLighting(UTRSurface surface, Light light)
{
    float3 diffuse = GetDiffuse(surface, light);
    float3 fresnel = GetFresnel(surface);
    return diffuse;
    return (diffuse + fresnel);
}

float3 GetLighting(UTRSurface surfaceWS)
{
    //计算片元的级联阴影信息
    ShadowData shadowData = GetShadowData(surfaceWS);
    //光照结果初始化为烘培好的gi光照结果
    float3 color = 0.0;
    //使用循环，累积所有有效方向光源的光照计算结果
    for(int i=0;i<GetDirectionalLightCount();i++)
    {
        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        color += GetLighting(surfaceWS, light);
    }
    return color;
}

#endif
