#ifndef UTR_INPUT_INCLUDED
#define UTR_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
TEXTURE2D(_RampMap);SAMPLER(sampler_RampMap);
TEXTURE2D(_DetailRampMap);SAMPLER(sampler_DetailRampMap);
TEXTURE2D(_LightInfoMap);SAMPLER(sampler_LightInfoMap);
TEXTURE2D(_SkinRampMap);SAMPLER(sampler_SkinRampMap);
TEXTURE2D(_FaceSDFMap);SAMPLER(sampler_FaceSDFMap);
TEXTURE2D(_BodySkinRampMap);SAMPLER(sampler_BodySkinRampMap);
TEXTURE2D(_ClothRampMap);SAMPLER(sampler_ClothRampMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float _OutlineWidth;
    float4 _OutlineColor;

    float4 _LitTone;
    float4 _ShadowTone;
    float _ShadowThreshold;
    float _ShadowSmoothStrength;
    float _IsDay;
    float _UseSolidShadow;

    float _FresnelStrength;
    float _FresnelThreshold;
    float _FresnelSmoothStrength;

    float4 _RampMap_ST;
    float4 _FaceSDFMap_ST;
    float _FaceSmoothStrength;
CBUFFER_END

struct InputConfig
{
    float2 baseUV;
};

InputConfig GetInputConfig(float2 baseUV)
{
    InputConfig c;
    c.baseUV = baseUV;
    return c;
}

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}


float4 GetBase(InputConfig c)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
    return map;
}


#endif