Shader "Hidden/SSNoir/PerceptualCrossfade"
{
    Properties
    {
        _MainTex ("Outgoing", 2D) = "black" {}
        _IncomingTex ("Incoming", 2D) = "black" {}
        _Progress ("Progress", Range(0, 1)) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _IncomingTex;
            float _Progress;

            float3 LinearToSrgb(float3 value)
            {
                value = max(value, 0.0);
                float3 low = value * 12.92;
                float3 high = 1.055 * pow(value, 1.0 / 2.4) - 0.055;
                return lerp(low, high, step(0.0031308, value));
            }

            float3 SrgbToLinear(float3 value)
            {
                value = max(value, 0.0);
                float3 low = value / 12.92;
                float3 high = pow((value + 0.055) / 1.055, 2.4);
                return lerp(low, high, step(0.04045, value));
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float3 outgoing = tex2D(_MainTex, input.uv).rgb;
                float3 incoming = tex2D(_IncomingTex, input.uv).rgb;

                #if defined(UNITY_COLORSPACE_GAMMA)
                    float3 color = lerp(outgoing, incoming, _Progress);
                #else
                    // Unity 在线性工程中采样 sRGB RenderTexture 时会先解码为线性值。
                    // 这里转回显示值做 dissolve，再转成线性值交给目标 RT 编码。
                    float3 outgoingSrgb = LinearToSrgb(outgoing);
                    float3 incomingSrgb = LinearToSrgb(incoming);
                    float3 color = SrgbToLinear(
                        lerp(outgoingSrgb, incomingSrgb, _Progress));
                #endif

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
