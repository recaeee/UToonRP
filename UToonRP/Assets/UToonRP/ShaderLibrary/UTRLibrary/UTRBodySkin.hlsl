#ifndef UTR_BODY_SKIN_INCLUDE
#define UTR_BODY_SKIN_INCLUDE

//身体皮肤Diffuse部分
float3 GetBodySkinDiffuse(UTRSurface surface, Light light, float4 lightInfoMapValue)
{
    float3 diffuse = 0.0;
    float halfLambert = dot(surface.normal, light.direction) * 0.5 + 0.5;
    halfLambert *= light.attenuation;
    //通用
    //是否使用Ramp贴图
    #if defined(_UTR_RAMP)
    float3 ramp = SAMPLE_TEXTURE2D(_BodySkinRampMap, sampler_BodySkinRampMap, float2(0.5, saturate(halfLambert - _ShadowThreshold))).rgb;
    diffuse = ramp;
    #elif defined(_UTR_DETAIL_RAMP)
    //仿原神渲染
    //映射halfLambert 0~0.5扩大，0.5以上直接1
    halfLambert = smoothstep(0, 0.5, halfLambert);
    //考虑固定阴影
    if(_UseSolidShadow)
    {
        halfLambert = halfLambert * smoothstep(0.1, 0.5, lightInfoMapValue.g);
    }
    // 根据lightMapValue.a确定使用的Ramp带
    float rampY = _IsDay > 0 ? lightInfoMapValue.a * 0.45 + 0.55 : lightInfoMapValue.a * 0.45;
    float3 ramp = SAMPLE_TEXTURE2D(_DetailRampMap, sampler_DetailRampMap, float2(halfLambert, rampY));
    diffuse = ramp;
    #else
    //明暗二分，亮面暖色调，暗面冷色调
    //柔滑明暗边界
    float ramp = smoothstep(0, _ShadowSmoothStrength, halfLambert - _ShadowThreshold);
    diffuse = lerp(_ShadowTone, _LitTone, ramp);
    #endif
    return surface.color * diffuse;
}

#endif
