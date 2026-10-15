Shader "SSNoir/CityRain"
{
    Properties { _Snow ("Snow shape", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings o; o.position = TransformObjectToHClip(input.position.xyz);
                o.uv = input.uv; o.color = input.color; return o;
            }
            float _Snow;
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = input.color;
                // The same transparent weather pass accepts either a thin rain stroke
                // or a soft round flake. Uniform flag; no extra material or draw pass.
                if (_Snow > 0.5)
                {
                    float radius = length(input.uv * 2 - 1);
                    color.a *= 1 - smoothstep(0.20, 0.94, radius);
                }
                else
                {
                    color.a *= saturate(1 - abs(input.uv.x * 2 - 1));
                    color.a *= sin(input.uv.y * PI);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
