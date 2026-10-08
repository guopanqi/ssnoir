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
            sampler2D _MainTex, _FocusBackground;
            float4 _Focus, _FocusRadii, _BackgroundSize;
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
                    color *= tex2D(_MainTex, input.uv);
                    if (_Lit > 0.5)
                    {
                        float2 lightLocal = mul(_SceneToLight, float4(input.world, 0, 1)).xy;
                        float falloff = pow(saturate(1.0 - length(lightLocal) / max(_Lamp.z, 0.001)), 2.0);
                        color.rgb *= 0.38 + _LightColor.rgb * falloff * _Lamp.w * 1.8;
                    }
                }
                else
                {
                    float d = distance(input.world, _Focus.xy) / _Focus.z;
                    float edge = smoothstep(_FocusRadii.x, _FocusRadii.y, d);
                    float dark = d < _FocusRadii.y ? edge * .5 : lerp(.5, .86, saturate((d - _FocusRadii.y) / (.94 - _FocusRadii.y)));
                    float4 source = tex2D(_FocusBackground, input.uv);
                    float4 blurred = 0;
                    for (int y = -1; y <= 1; y++)
                        for (int x = -1; x <= 1; x++)
                            blurred += tex2D(_FocusBackground, input.uv + float2(x,y) * _BackgroundSize.xy) / 9;
                    color = lerp(source, blurred, edge * _Focus.w);
                    // Prototype iris mixes in sRGB; the project renders in Linear, so the same
                    // factor must mix in display space or mid-ramp reads a full stop too weak.
                    float3 mixed = lerp(pow(max(color.rgb, 0), 1.0 / 2.2), pow(max(_Tint.rgb, 0), 1.0 / 2.2), dark * _Focus.w);
                    color.rgb = pow(mixed, 2.2);
                    // Keep the lerped alpha: forcing opaque here would seal the canvas and
                    // the city could never show through, no matter the dim setting.
                }
                // Static sub-LSB dither: the wide dark ramp quantizes into visible rings on an
                // 8-bit target. This perturbs which display code each pixel lands on without
                // any visible speckle or shimmer; keep amplitude at the floor.
                float grainHash = frac(sin(dot(floor(input.vertex.xy), float2(12.9898, 78.233))) * 43758.5453);
                color.rgb += (grainHash - 0.5) * _Dither;
                return color;
            }
            ENDHLSL
        }
    }
}
