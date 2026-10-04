// 试验用抛光地面面光高光；漫反射和阴影仍由原 URP Lit 光照贴图负责。
// 采用圆盘面积与 GGX 粗糙度的有限近似，不替代完整 LTC，也不处理光源遮挡。
Shader "SSNoir/City/AreaFloorHighlight"
{
    Properties
    {
        _Roughness ("Source roughness", Range(0.04,1)) = 0.3
        _Intensity ("Approximation calibration", Float) = 0.12
        _AreaPosition0 ("Area position 0", Vector) = (0,0,0,0)
        _AreaNormal0 ("Area normal 0", Vector) = (0,-1,0,0)
        _AreaRadiance0 ("Area radiance 0", Vector) = (0,0,0,0)
        _AreaPosition1 ("Area position 1", Vector) = (0,0,0,0)
        _AreaNormal1 ("Area normal 1", Vector) = (0,-1,0,0)
        _AreaRadiance1 ("Area radiance 1", Vector) = (0,0,0,0)
        _AreaPosition2 ("Area position 2", Vector) = (0,0,0,0)
        _AreaNormal2 ("Area normal 2", Vector) = (0,-1,0,0)
        _AreaRadiance2 ("Area radiance 2", Vector) = (0,0,0,0)
        _AreaPosition3 ("Area position 3", Vector) = (0,0,0,0)
        _AreaNormal3 ("Area normal 3", Vector) = (0,-1,0,0)
        _AreaRadiance3 ("Area radiance 3", Vector) = (0,0,0,0)
        _AreaPosition4 ("Area position 4", Vector) = (0,0,0,0)
        _AreaNormal4 ("Area normal 4", Vector) = (0,-1,0,0)
        _AreaRadiance4 ("Area radiance 4", Vector) = (0,0,0,0)
        _AreaPosition5 ("Area position 5", Vector) = (0,0,0,0)
        _AreaNormal5 ("Area normal 5", Vector) = (0,-1,0,0)
        _AreaRadiance5 ("Area radiance 5", Vector) = (0,0,0,0)
        _AreaPosition6 ("Area position 6", Vector) = (0,0,0,0)
        _AreaNormal6 ("Area normal 6", Vector) = (0,-1,0,0)
        _AreaRadiance6 ("Area radiance 6", Vector) = (0,0,0,0)
        _AreaPosition7 ("Area position 7", Vector) = (0,0,0,0)
        _AreaNormal7 ("Area normal 7", Vector) = (0,-1,0,0)
        _AreaRadiance7 ("Area radiance 7", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "AreaHighlight"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Roughness, _Intensity;
            float4 _AreaPosition0, _AreaNormal0, _AreaRadiance0;
            float4 _AreaPosition1, _AreaNormal1, _AreaRadiance1;
            float4 _AreaPosition2, _AreaNormal2, _AreaRadiance2;
            float4 _AreaPosition3, _AreaNormal3, _AreaRadiance3;
            float4 _AreaPosition4, _AreaNormal4, _AreaRadiance4;
            float4 _AreaPosition5, _AreaNormal5, _AreaRadiance5;
            float4 _AreaPosition6, _AreaNormal6, _AreaRadiance6;
            float4 _AreaPosition7, _AreaNormal7, _AreaRadiance7;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(pos.positionCS.z);
                return output;
            }
            float3 DiskHighlight(float3 P, float3 N, float3 V, float4 positionRadius, float4 planeNormal, float4 radiance)
            {
                if (positionRadius.w <= 0) return 0;
                float3 R = reflect(-V, N);
                float denom = dot(R, planeNormal.xyz);
                if (denom >= -0.0001) return 0;
                float t = dot(positionRadius.xyz - P, planeNormal.xyz) / denom;
                if (t <= 0) return 0;
                float radial = length(P + R * t - positionRadius.xyz);
                float radius = positionRadius.w;
                float blur = max(t * _Roughness * _Roughness, radius * 0.08);
                // 连续圆盘卷积近似：避免平顶亮斑和相邻亮斑之间的灰色凹口。
                float variance = blur * blur + radius * radius * 0.25;
                float coverage = exp(-0.5 * radial * radial / variance);
                coverage *= radius * radius / (radius * radius + 2 * blur * blur);
                float facing = saturate(-dot(normalize(positionRadius.xyz - P), planeNormal.xyz));
                float fresnel = 0.04 + 0.96 * pow(1 - saturate(dot(N,V)), 5);
                return radiance.rgb * coverage * fresnel * facing * _Intensity;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 color = 0;
                color += DiskHighlight(input.positionWS, N, V, _AreaPosition0, _AreaNormal0, _AreaRadiance0);
                color += DiskHighlight(input.positionWS, N, V, _AreaPosition1, _AreaNormal1, _AreaRadiance1);
                color += DiskHighlight(input.positionWS, N, V, _AreaPosition2, _AreaNormal2, _AreaRadiance2);
                color += DiskHighlight(input.positionWS, N, V, _AreaPosition3, _AreaNormal3, _AreaRadiance3);
                color += DiskHighlight(input.positionWS, N, V, _AreaPosition4, _AreaNormal4, _AreaRadiance4);
                color += DiskHighlight(input.positionWS, N, V, _AreaPosition5, _AreaNormal5, _AreaRadiance5);
                color += DiskHighlight(input.positionWS, N, V, _AreaPosition6, _AreaNormal6, _AreaRadiance6);
                color += DiskHighlight(input.positionWS, N, V, _AreaPosition7, _AreaNormal7, _AreaRadiance7);
                return half4(MixFogColor(color, 0, input.fog), 0);
            }
            ENDHLSL
        }
    }
}
