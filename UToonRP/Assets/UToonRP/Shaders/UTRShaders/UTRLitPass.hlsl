//统一管理Lit的Input，其用于所有Pass,诞生目的为加入GI用的Meta Pass相关
#ifndef UTR_LIT_PASS_INCLUDED
#define UTR_LIT_PASS_INCLUDED

#include "../../ShaderLibrary/UTRLibrary/UTRSurface.hlsl"
#include "../../ShaderLibrary/UTRLibrary/UTRShadows.hlsl"
#include "../../ShaderLibrary/UTRLibrary/UTRLight.hlsl"
#include "../../ShaderLibrary/UTRLibrary/UTRFace.hlsl"
#include "../../ShaderLibrary/UTRLibrary/UTRBodySkin.hlsl"
#include "../../ShaderLibrary/UTRLibrary/UTRCloth.hlsl"
#include "../../ShaderLibrary/UTRLibrary/UTRLighting.hlsl"

struct Attributes
{
    float3 positionOS:POSITION;
    float3 normalOS:NORMAL;
    float2 baseUV:TEXCOORD0;
};

struct Varyings
{
    float4 positionCS:SV_POSITION;
    float3 positionWS:VAR_POSITION;
    float3 normalWS:VAR_NORMAL;
    float2 baseUV:VAR_BASE_UV;
};

Varyings UTRLitPassVertex(Attributes input)
{
    Varyings output;
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.baseUV = TransformBaseUV(input.baseUV);

    return output;
}

float4 UTRLitPassFragment(Varyings input) : SV_TARGET
{
    InputConfig config = GetInputConfig(input.baseUV);
    float4 base = GetBase(config);

    UTRSurface surface;
    surface.position = input.positionWS;
    surface.normal = normalize(input.normalWS);
    surface.interpolatedNormal = surface.normal;
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.color = base.rgb;
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.baseUV = input.baseUV;
    surface.fresnelStrength = _FresnelStrength;
    surface.dither = InterleavedGradientNoise(input.positionCS.xy,0);
    float3 color = GetLighting(surface);
    return float4(color, 1);
}

#endif