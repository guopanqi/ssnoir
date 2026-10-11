Shader "SSNoir/CityRain"
{
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
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = input.color;
                color.a *= saturate(1 - abs(input.uv.x * 2 - 1));
                color.a *= sin(input.uv.y * PI);
                return color;
            }
            ENDHLSL
        }
    }
}
