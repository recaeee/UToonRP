//统一管理Lit的Input，其用于所有Pass,诞生目的为加入GI用的Meta Pass相关
#ifndef UTR_OUTLINE_PASS_INCLUDED
#define UTR_OUTLINE_PASS_INCLUDED

float4x4 unity_CameraInvProjection;
// float4 _ProjectionParams;

struct Attributes
{
    float3 positionOS:POSITION;
    float3 normalOS:NORMAL;
    float2 baseUV:TEXCOORD0;
};

struct Varings
{
    float4 positionCS:SV_POSITION;
    float3 positionWS:VAR_POSITION;
    float3 normalWS:VAR_NORMAL;
    float2 baseUV:VAR_BASE_UV;
};

Varings UTROutlinePassVertex(Attributes input)
{
    Varings output;
    //顶点沿着法线方向外扩
    // float3 outlinePositionOS = input.positionOS + input.normalOS * _OutlineWidth;
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.baseUV = TransformBaseUV(input.baseUV);
    
    //将近裁面右上角的顶点变换到观察空间
    float4 nearUpperRight = mul(unity_CameraInvProjection, float4(1, 1, UNITY_NEAR_CLIP_VALUE, _ProjectionParams.y));
    //计算屏幕宽高比
    float aspect = abs(nearUpperRight.y / nearUpperRight.x);
    //裁剪空间法线方向
    float3 normalCS = TransformWorldToHClipDir(output.normalWS);
    //NDC空间法线
    float3 normalNDC = normalize(normalCS) * output.positionCS.w;
    normalNDC.x *= aspect;
    output.positionCS.xy += 0.01 * _OutlineWidth * normalNDC.xy;

    return output;
}

float4 UTROutlinePassFragment(Varings input) : SV_TARGET
{
    return _OutlineColor;
}

#endif