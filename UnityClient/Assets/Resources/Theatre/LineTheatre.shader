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
            int _SpotCount;
            float4 _Spots[8], _SpotColors[8];
            // Scene-space beam; soft sides, narrower at the source, constant geometry on every platform.
            float beam(float2 world, float4 spot)
            {
                float depth = (world.y - (spot.y - spot.w)) / spot.w;
                float halfWidth = spot.z * .5 * lerp(.12, 1, saturate(depth));
                float side = 1 - smoothstep(.55, 1, abs(world.x - spot.x) / halfWidth);
                return side * smoothstep(0, .12, depth) * (1 - smoothstep(1, 1.06, depth));
            }
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
                    float3 illumination = 0;
                    for (int i = 0; i < _SpotCount; i++)
                        illumination += _SpotColors[i].rgb * _SpotColors[i].a * beam(input.world, _Spots[i]);
                    // Adds light to the actual portrait pixels, including their faint alpha edges.
                    color.rgb += image.rgb * _Tint.rgb * illumination * 1.4;
                    color.a = saturate(color.a * (1 + max(illumination.r, max(illumination.g, illumination.b)) * .65));
                }
                else if (_Mode < 4.5)
                {
                    // Fixed scene-space ellipse: its radius does not inflate as the center moves.
                    float d = length((input.world - _Focus.xy) / float2(1, .82));
                    float edge = smoothstep(_FocusRadii.x, _FocusRadii.y, d);
                    color.a = edge * _Focus.z * _Focus.w;
                    float noise = frac(sin(dot(floor(input.vertex.xy), float2(12.9898,78.233))) * 43758.5453) - .5;
                    color.a = saturate(color.a + noise * _Dither * edge);
                }
                else
                {
                    float depth = 1 - input.uv.y;
                    float side = 1 - smoothstep(.55, 1, abs(input.uv.x - .5) * 2 / lerp(.12, 1, depth));
                    color.a *= .085 * side * smoothstep(0, .12, depth);
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
