Shader "UTR/UTR Stage Emission"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        //自发光颜色，使用HDR颜色
        [HDR]_EmissionColor("Emission",Color) = (0.0,0.0,0.0,0.0)
        //底色
        [HDR]_BackgroundColor("Background Color",Color) = (0.0,0.0,0.0,0.0)
        //跳跃最大高度
        _MaxHeight("Max Height",float) = 1
        //最低自发光强度
        _MinEmissionIntensity("Min Emission Intensity", Range(0,1)) = 0.5
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
            Cull Off
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UTRStageEmissionPassVertex
            #pragma fragment UTRStageEmissionPassFragment

            CBUFFER_START(UnityPerMaterial)
            float4 _EmissionColor;
            float4 _BackgroundColor;
            float _MaxHeight;
            float _MinEmissionIntensity;
            CBUFFER_END

            //音量控制的自发光强度
            float _MusicEmissionIntensity;
            //时间
            float4 _Time;

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
            };

            Varyings UTRStageEmissionPassVertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.baseUV = TransformBaseUV(input.baseUV);
                return output;
            }

            float4 UTRStageEmissionPassFragment(Varyings input) : SV_TARGET
            {
                //亮度
                float intensity = _MinEmissionIntensity + (1 - _MinEmissionIntensity) * _MusicEmissionIntensity;
                //横向
                float horizontal = sin(5 * abs(input.positionWS.z) - _Time.y) * 0.5 + 0.5;
                //纵向
                float vertical = step(input.positionWS.y, _MaxHeight * _MusicEmissionIntensity + 0.1 * horizontal);
                
                // return float4(input.positionWS.y,0,0,1);
                return float4(_EmissionColor.rgb * intensity * vertical, 1.0);
            }
            
            ENDHLSL
        }
    }
}