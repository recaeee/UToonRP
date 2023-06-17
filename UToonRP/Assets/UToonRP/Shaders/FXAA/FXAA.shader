Shader "UTR/Anti-Aliasing/FXAA"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        #include  "../../ShaderLibrary/Common.hlsl"
        #include  "../PostFxStackPasses.hlsl"
        
        #define FXAA_PC 1
        #define FXAA_HLSL_3 1
        #define FXAA_QUALITY__PRESET 12
        #define FXAA_GREEN_AS_LUMA 1

        #pragma target 3.0
        // #include "UnityCG.cginc"
        #include "FXAA3.cginc"


        float3 _QualitySettings;
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;

        inline float2 UnityStereoScreenSpaceUVAdjustInternal(float2 uv, float4 scaleAndOffset)
        {
            return uv.xy * scaleAndOffset.xy + scaleAndOffset.zw;
        }

        float4 FXAAPassFragment(Varyings input) : SV_TARGET
        {
            half4 color = FxaaPixelShader(UnityStereoScreenSpaceUVAdjustInternal(input.screenUV, float4(1, 1, 0, 0)),
                                          0,
                                          _MainTex, _MainTex, _MainTex, _MainTex_TexelSize.xy,
                                          0, 0, 0,
                                          _QualitySettings.x, _QualitySettings.y, _QualitySettings.z,
                                          0, 0, 0, 0);
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "FXAA"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FXAAPassFragment
            ENDHLSL
        }
    }
}