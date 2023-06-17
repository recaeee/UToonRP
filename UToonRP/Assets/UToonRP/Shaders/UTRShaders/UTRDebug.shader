Shader "UTR/UTR Debug"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white"{}
        //剔除模式
        [Enum(Off,0,Front,1,Back,2)] _Cull("Cull",Float) = 0
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../../ShaderLibrary/Common.hlsl"
        #include "UTRInput.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "UTR Stage Emission"
            Tags
            {
                "LightMode" = "UTRMainLit"
            }
            
            Blend Off
            Zwrite On
            Cull [_Cull]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UTRReflectionPassVertex
            #pragma fragment UTRReflectionPassFragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

            struct Attributes
            {
                float3 positionOS:POSITION;
                float2 baseUV:TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:VAR_POSITION;
                float2 baseUV:VAR_BASE_UV;
                float4 positionSS:TEXCOORD0;
            };

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            float4 ComputeScreenPos(float4 positionCS)
            {
                float4 o = positionCS * 0.5f;
                o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
                o.zw = positionCS.zw;
                return o;
            }

            Varyings UTRReflectionPassVertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.baseUV = input.baseUV;;
                output.positionSS = ComputeScreenPos(output.positionCS);
                return output;
            }

            float4 UTRReflectionPassFragment(Varyings input) : SV_TARGET
            {
                // float4 col = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, input.positionSS.xy/input.positionSS.w);
                float4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
                float3 col1 = col.a;
                return float4(input.baseUV.xy,0.0, 1.0f);
            }
            
            ENDHLSL
        }
    }
}