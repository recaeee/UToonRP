Shader "UTR/UTR-Universal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}

        [Header(Outline)]
        _OutlineWidth("Outline Width", Range(0.01, 1)) = 0.25
        _OutlineColor("Outline Color", Color) = (0.5,0.5,0.5,1)

        [Header(Lit)]
        //亮面色调
        _LitTone("Lit Tone", Color) = (1,1,1,1)
        //暗面色调
        _ShadowTone("Shadow Tone", Color) = (0.7,0.7,0.8,1)
        //二分阈值
        _ShadowThreshold("Shadow Threshold", Range(0, 1)) = 0.5
        //明暗过渡强度
        _ShadowSmoothStrength("Shadow Smooth Strength", Range(0, 0.05)) = 0.025
        //是否接收阴影
        [Toggle(_RECEIVE_SHADOWS)]_ReceiveShadows("Receive Shadows", Float) = 1
        //白天黑夜
        [Enum(Day,1,Night,0)]_IsDay("Is Day", Float) = 1
        //考虑固定阴影
        [Enum(Off,0,On,1)]_UseSolidShadow("Use Solid Shadow", Float) = 1

        [Header(_UTR_RAMP)]
        //是否使用Ramp贴图
        [Toggle(_UTR_RAMP)]_UTRRamp("UTR Ramp", Float) = 1
        //通用Ramp贴图
        [NoScaleOffset]_RampMap("Ramp Map", 2D) = "white" {}
        //是否使用精细Ramp贴图
        [Toggle(_UTR_DETAIL_RAMP)]_UTRDetailRamp("UTR Detail Ramp", Float) = 0
        //精细Ramp贴图
        [NoScaleOffset]_DetailRampMap("Detail Ramp Map", 2D) = "white" {}
        //光照信息贴图
        [NoScaleOffset]_LightInfoMap("Light Info Map", 2D) = "white" {}

        [Header(Fresnel)]
        //菲涅尔系数
        _FresnelStrength("Fresnel Strength", Range(0, 1)) = 0.2
        //菲涅尔阈值
        _FresnelThreshold("Fresnel Threshold", Range(0, 0.1)) = 0.1
        //菲涅尔过渡
        _FresnelSmoothStrength("Fresnel Smooth Strength", Range(0, 0.05)) = 0.025

        [Header(Face)]
        [Toggle(_UTR_FACE)]_UTRFace("UTR Face", Float) = 0
        //皮肤Ramp贴图
        [NoScaleOffset]_SkinRampMap("Skin Ramp Map", 2D) = "white" {}
        //脸部SDF贴图
        _FaceSDFMap("Face SDF Map", 2D) = "white" {}
        //脸部明暗过渡
        _FaceSmoothStrength("Face Smooth Strength", Range(0, 0.05)) = 0.025

        [Header(BodySkin)]
        [Toggle(_UTR_BODY_SKIN)]_URTBodySkin("UTR Body Skin", Float) = 0
        //身体皮肤Ramp贴图
        [NoScaleOffset]_BodySkinRampMap("Body Skin Ramp Map", 2D) = "white" {}

        [Header(Cloth)]
        [Toggle(_UTR_CLOTH)]_UTRCloth("UTR Cloth", Float) = 0
        [NoScaleOffset]_ClothRampMap("Cloth Ramp Map", 2D) = "white" {}
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../../ShaderLibrary/Common.hlsl"
        #include "UTRInput.hlsl"
        ENDHLSL


        //主光照Pass
        Pass
        {
            Name "UTR Main Lit"

            Tags
            {
                "LightMode" = "UTRMainLit"
            }

            Blend Off
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _UTR_FACE
            #pragma shader_feature _UTR_BODY_SKIN
            #pragma shader_feature _UTR_CLOTH
            #pragma shader_feature _UTR_RAMP
            #pragma shader_feature _UTR_DETAIL_RAMP

            #pragma shader_feature _CLIPPING
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            
            #pragma vertex UTRLitPassVertex
            #pragma fragment UTRLitPassFragment
            #include "UTRLitPass.hlsl"
            ENDHLSL

        }

        //描边Pass
        Pass
        {
            Name "UTR Outline"

            Tags
            {
                "LightMode" = "UTROutline"
            }

            //正面剔除
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UTROutlinePassVertex
            #pragma fragment UTROutlinePassFragment
            #include "UTROutlinePass.hlsl"
            ENDHLSL
        }

        //渲染阴影贴图Pass
        Pass
        {
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
            ColorMask 0
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _ _SHADOWS_CLIP_SHADOWS_DITHER
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex UTRShadowCasterPassVertex
            #pragma fragment UTRShadowCasterPassFragment
            #include "UTRShadowCasterPass.hlsl"

            ENDHLSL
        }
    }
}