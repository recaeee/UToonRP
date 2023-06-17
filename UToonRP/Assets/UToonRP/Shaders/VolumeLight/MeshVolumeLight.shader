Shader "UTR/Mesh Volume Light"
{
    Properties
    {
        _SampleStepCount("Sample Step Count(采样次数)", Float) = 10
        _Absorption("Absorption(介质吸光率)", Range(0.0001, 1.0)) = 0.15
        [Toggle(_USE_SHADOW)]_UseShadow("Use Shadow(是否考虑阴影)", Float) = 1
    }
    SubShader
    {
        HLSLINCLUDE
        #include  "../../ShaderLibrary/Common.hlsl"
        #include "../../ShaderLibrary/UTRLibrary/PPUtils.hlsl"

        #pragma multi_compile _ _USE_SHADOW

        #define PLANES_COUNT_MAX 16
        #define LIGHT_INDEX _SpotVolumeLightInfo.x
        #define PLANES_COUNT _SpotVolumeLightInfo.y

        struct Attributes
        {
            float4 positionOS : POSITION;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            // float2 screenUV : VAR_SCREEN_UV;

            float3 positionWS : TEXCOORD0;
            float4 positionSS : TEXCOORD1;
        };

        Varyings DefaultPassVertex(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            // output.positionSS = ComputeScreenPos(float4(input.positionOS.x, input.positionOS.y * _ProjectionParams.x, 1, 1));
            output.positionSS = ComputeScreenPos(output.positionCS);
            return output;
        }

        float _SampleStepCount;
        float _UseShadow;
        float _Absorption;
        float4x4 _CameraInvVP;

        float4 _SpotVolumeLightInfo;
        float4 _SpotBoundaryPlanes[PLANES_COUNT_MAX];
        
        TEXTURE2D(_PostFXSource);
        TEXTURE2D(_CameraDepthTex);
        TEXTURE2D_SHADOW(_OtherShadowAtlas);
        SAMPLER(sampler_linear_clamp);
        #define SHADOW_SAMPLER sampler_linear_clamp_compare
        SAMPLER_CMP(SHADOW_SAMPLER);

        //-----------------------------------------------------------------
        //光源相关
        #define MAX_OTHER_LIGHT_COUNT 64
        #define MAX_SHADOWED_OTHER_LIGHT_COUNT 16

        //用CBuffer包裹构造方向光源的两个属性，cpu会每帧传递（修改）这两个属性到GPU的常量缓冲区，对于一次渲染过程这两个值恒定
        CBUFFER_START(_CustomLight)
        int _OtherLightCount;
        float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
        float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
        float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
        float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
        float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
        float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
        //阴影相关
        float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
        CBUFFER_END

        struct SpotLight
        {
            float3 color;
            float3 direction;
            float3 position;
            bool isSpot;
            int lightIndex;
            //使用哪个Shadow Tile
            int tileIndex;
            //聚光灯内外角
            float4 spotAngles;
        };

        bool IsSpotLight(int lightIndex)
        {
            return _OtherLightDirections[lightIndex].w == 1.0;
        }

        SpotLight GetSpotLight(int lightIndex)
        {
            SpotLight light;
            light.color = _OtherLightColors[lightIndex];
            light.direction = _OtherLightDirections[lightIndex];
            light.position = _OtherLightPositions[lightIndex];
            light.isSpot = _OtherLightDirections[lightIndex].w == 1.0;
            light.lightIndex = lightIndex;
            light.tileIndex = _OtherLightShadowData[lightIndex].y;
            light.spotAngles = _OtherLightSpotAngles[lightIndex];


            return light;
        }

        //光源相关
        //-----------------------------------------------------------------

        //参考https://zhuanlan.zhihu.com/p/21425792
        //假定光强为1，求从start位置往rd方向走d距离的光能量积分
        float InScatter(float3 startPos, float3 dir, float3 lightPos, float distance)
        {
            float3 q = startPos - lightPos;
            float b = dot(dir, q);
            float c = dot(q, q);
            float iv = 1.0f / sqrt(c - b * b);
            float l = iv * (atan(distance + b * iv) - atan(b * iv));

            return l;
        }

        //返回source颜色
        float4 GetSource(float2 screenUV)
        {
            //直接采样0级mipmap，其不启用mipmap
            return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
        }

        float4 GetVolumeLightBySpotLight(float2 screenUV, float4 positionWS, SpotLight spotLight)
        {
            float4 volumeLight = 0.0;
            //计算从摄像机位置到屏幕像素的方向
            float3 dir = normalize(positionWS - _WorldSpaceCameraPos);
            //获取片元深度
            float depth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTex, sampler_linear_clamp, screenUV, 0).r;
            // return depth;
            //计算步进的距离
            float depthWS = DepthToWorldDistance(screenUV, depth);
            // return depthWS * 0.001;
            //求积分
            volumeLight += InScatter(positionWS, dir, spotLight.position, depthWS);
            return volumeLight;
        }

        float ShadowAt(float3 worldPos, SpotLight spotLight)
        {
            //计算Shadow Tile Space Position
            float tileIndex = spotLight.tileIndex;
            float4 positionSTS = mul(_OtherShadowMatrices[tileIndex], float4(worldPos, 1.0));
            positionSTS /= positionSTS.w;
            //防止采样越界
            float4 tileData = _OtherShadowTiles[tileIndex];
            float3 bounds = tileData.xyz;
            positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
            return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas,SHADOW_SAMPLER,positionSTS);
        }
        
        //光在介质中传播，会被戒指吸收一部分
        float BeerLambertLaw(float dis, float absorption)
        {
            return exp(-dis * absorption);
        }

        float GetSpotLightAttenuationOnSurface(float3 positioWS, SpotLight spotLight)
        {
            float index = spotLight.lightIndex;
            //考虑聚光灯方向造成的衰减
            float4 spotAngles = _OtherLightSpotAngles[index];
            float3 spotDirection = _OtherLightDirections[index].xyz;
            float3 direction = normalize(positioWS - spotLight.position);
            float spotAttenuation = Square(saturate(dot(spotDirection, direction) *
                spotAngles.x + spotAngles.y));
            return spotAttenuation;
        }

        //transmittance为透射率
        float3 Scattering(float3 dir, float near, float far, SpotLight spotLight)
        {
            // float3 pos = _WorldSpaceCameraPos + dir * far;
            // return GetSpotLightAttenuationOnSurface(pos, spotLight);
            float totalLight = 0;
            //计算采样步长
            float stepSize = (far - near) / _SampleStepCount;
            
            
            for (int i = 1; i < _SampleStepCount; i++)
            {
                float f = stepSize;
                //计算世界空间下的采样点
                float3 pos = _WorldSpaceCameraPos + dir * (near + stepSize * i);
                //考虑光源到采样点的衰减
                float lightDistance = distance(spotLight.position, pos);
                float scat1 = BeerLambertLaw(lightDistance, _Absorption);
                f *= scat1;
                //考虑采样点到摄像机的衰减
                float cameraDistance = distance(_WorldSpaceCameraPos, pos);
                float scat2 = BeerLambertLaw(cameraDistance, _Absorption);
                f *= scat2;

                //考虑阴影
                #if defined(_USE_SHADOW)
                float atten = ShadowAt(pos, spotLight);
                f *= atten;
                // if(atten == 0)
                // {
                //     return float3(1,0,0);
                // }
                #endif

                //考虑聚光灯的圆锥体
                float3 pos2LightDir = normalize(spotLight.position - pos);
                float spotAttenuation = Square(saturate(dot(spotLight.direction, pos2LightDir) *
                    spotLight.spotAngles.x + spotLight.spotAngles.y));
                f *= spotAttenuation;
                
                totalLight += f;
            }
            
            return totalLight * spotLight.color;
        }

        float IntersectPlane(float4 plane, float3 origin, float3 dir, out float intersect)
        {
            float d = dot(dir, plane.xyz);
            intersect = d;
            return -dot(float4(origin.xyz, 1), plane) / d;
        }

        float2 GetBoundary(float3 dir)
        {
            //先获取摄像机的近远平面作为边界
            float maxNear = _ProjectionParams.y;
            float minFar = _ProjectionParams.z;
            //求视线和聚光灯锥体的交点
            float intersected = 0;
            for(int i = 0; i < PLANES_COUNT; i++)
            {
                float t = IntersectPlane(_SpotBoundaryPlanes[i], _WorldSpaceCameraPos, dir, intersected);
                if(intersected < 0)
                {
                    maxNear = max(maxNear, t);
                }
                if(intersected > 0)
                {
                    minFar = min(minFar, t);
                }
            }
            return float2(maxNear, minFar);
        }
        

        float4 MeshVolumeLightPassFragment(Varyings input) : SV_TARGET
        {
            float4 positionSS = input.positionSS;
            positionSS /= positionSS.w;
            //求屏幕深度
            float2 screenPos = positionSS.xy;
            float screenDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTex, sampler_linear_clamp, screenPos, 0).r;
            //求世界空间深度
            float depthWS = DepthToWorldDistance(screenPos, screenDepth);
            //求视线方向
            float3 dir = normalize(input.positionWS - _WorldSpaceCameraPos);
            //求Raymarching的近远距离
            float2 nearFar = GetBoundary(dir);
            float near = nearFar.x, far = nearFar.y;

            //手动深度剔除
            //如果摄像机和“圆锥体”的近交点深度 大于 片元摄像机深度，意味着近交点在实际物体前面，则Clip掉这个像素
            float3 nearWorldPos = _WorldSpaceCameraPos + dir * near;
            float4 p = TransformWorldToHClip(nearWorldPos);
            p /= p.w;
            //p.z - screenDepth而不是screenDepth - p.z的原因是裁剪空间z轴指向摄像机正后方，因此取反
            clip(p.z - screenDepth);
            
            far = min(far, depthWS);

            SpotLight spotLight = GetSpotLight(LIGHT_INDEX);
            float3 color = Scattering(dir,near,far,spotLight);
            
            return float4(color,1);
        }
        ENDHLSL

        Pass
        {
            Name "Mesh Volume Light"

            ZTest Less
            ZWrite Off
            Cull Front
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment MeshVolumeLightPassFragment
            ENDHLSL
        }
    }
}