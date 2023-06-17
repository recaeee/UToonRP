//后处理计算工具
#ifndef PP_UTILS
#define PP_UTILS

#include "../../ShaderLibrary/UnityInput.hlsl"

float4 _ZBufferParams;
float4 _DepthParams;

float4 ComputeScreenPos(float4 positionCS)
{
    float4 o = positionCS * 0.5f;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o;
}

float LinearEyeDepth( float z )
{
    return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
}

float DepthToWorldDistance(float2 screenCoord, float depthValue)
{
    float2 p = (screenCoord.xy * 2 - 1) * _DepthParams.xy;
    float3 ray = float3(p.xy, 1);
    return LinearEyeDepth(depthValue) * length(ray);
}

float Distance(float3 posA, float3 posB)
{
    return pow(dot(posA - posB, posA - posB), 0.5);
}

#endif