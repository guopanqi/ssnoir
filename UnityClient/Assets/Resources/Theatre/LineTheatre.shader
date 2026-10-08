Shader "SSNoir/LineTheatre"
{
    Properties { _MainTex ("Image", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Focus, _FocusRadii;
            float4 _Tint, _LightColor;
            float4x4 _SceneTransform, _SceneToClip, _SceneToLight;
            float4 _Lamp; // unused x/y, local radius, inherited intensity
            float _Mode, _Reveal, _Lit, _Dither;
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.world = mul(_SceneTransform, input.vertex).xy;
                output.vertex = mul(_SceneToClip, float4(output.world, 0, 1));
                output.uv = input.uv;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float4 color = _Tint;
                if (_Mode < 0.5) clip(_Reveal - input.uv.x - 0.000001);
                else if (_Mode < 1.5) { }
                else if (_Mode < 2.5)
                {
                    float d = length((input.uv - 0.5) * 2.0);
                    color.a *= pow(saturate(1.0 - d), 2.0);
                }
                else if (_Mode < 3.5)
                {
                    float4 image = tex2D(_MainTex, input.uv);
                    #ifndef UNITY_COLORSPACE_GAMMA
                    image.rgb = LinearToGammaSpace(image.rgb);
                    #endif
                    color *= image;
                    if (_Lit > 0.5)
                    {
                        float2 lightLocal = mul(_SceneToLight, float4(input.world, 0, 1)).xy;
                        float falloff = pow(saturate(1.0 - length(lightLocal) / max(_Lamp.z, 0.001)), 2.0);
                        color.rgb *= 0.38 + _LightColor.rgb * falloff * _Lamp.w * 1.8;
                    }
                }
                else
                {
                    // Fixed scene-space ellipse: its radius does not inflate as the center moves.
                    float d = length((input.world - _Focus.xy) / float2(1, .82));
                    float edge = smoothstep(_FocusRadii.x, _FocusRadii.y, d);
                    color.a = edge * _Focus.z * _Focus.w;
                    float noise = frac(sin(dot(floor(input.vertex.xy), float2(12.9898,78.233))) * 43758.5453) - .5;
                    color.a = saturate(color.a + noise * _Dither * edge);
                }
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment resolve
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 resolve(v2f_img input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                color.rgb = color.a > .00001 ? color.rgb / color.a : 0;
                #ifndef UNITY_COLORSPACE_GAMMA
                color.rgb = GammaToLinearSpace(color.rgb);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}
