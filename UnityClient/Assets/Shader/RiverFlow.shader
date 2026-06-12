Shader "Noir/River Flow"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor ("Base Color", Color) = (0.008, 0.015, 0.035, 1)
        _LineColor ("Wave Line Color", Color) = (0.82, 0.86, 0.90, 1)
        _Brightness ("Line Brightness", Range(0, 2)) = 0.85

        [Header(Layout)]
        _WidthScale ("Width Scale", Range(0.5, 20)) = 5
        _LengthScale ("Length Scale", Range(1, 80)) = 24
        _SwapUV ("Swap UV Axis", Range(0, 1)) = 0

        [Header(Main Waves)]
        _FlowSpeed ("Main Flow Speed", Range(-5, 5)) = 0.75
        _WaveDensity ("Main Wave Density", Range(0.2, 10)) = 2.2
        _Distortion ("Wave Distortion", Range(0, 8)) = 2.2
        _LineThickness ("Main Line Thickness", Range(0.005, 0.3)) = 0.065
        _Breakup ("Main Line Breakup", Range(0, 1)) = 0.58

        [Header(Secondary Waves)]
        _SecondarySpeed ("Secondary Flow Speed", Range(-5, 5)) = 0.42
        _SecondaryDensity ("Secondary Wave Density", Range(0.2, 10)) = 3.6
        _SecondaryThickness ("Secondary Line Thickness", Range(0.005, 0.3)) = 0.045
        _SecondaryStrength ("Secondary Line Strength", Range(0, 1)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "RiverFlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;

                float _Brightness;

                float _WidthScale;
                float _LengthScale;
                float _SwapUV;

                float _FlowSpeed;
                float _WaveDensity;
                float _Distortion;
                float _LineThickness;
                float _Breakup;

                float _SecondarySpeed;
                float _SecondaryDensity;
                float _SecondaryThickness;
                float _SecondaryStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);

                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(
                    lerp(a, b, local.x),
                    lerp(c, d, local.x),
                    local.y
                );
            }

            float FBM(float2 p)
            {
                float result = 0.0;
                float amplitude = 0.5;

                result += ValueNoise(p) * amplitude;
                p = p * 2.03 + 17.1;
                amplitude *= 0.5;

                result += ValueNoise(p) * amplitude;
                p = p * 2.01 + 11.7;
                amplitude *= 0.5;

                result += ValueNoise(p) * amplitude;
                p = p * 2.07 + 23.4;
                amplitude *= 0.5;

                result += ValueNoise(p) * amplitude;

                return result;
            }

            float NarrowBrightLine(float value, float thickness)
            {
                float edge = 1.0 - thickness;
                float aa = max(fwidth(value) * 1.5, 0.0001);
                return smoothstep(edge - aa, edge + aa, value);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = lerp(input.uv, input.uv.yx, saturate(_SwapUV));

                // _Time.y is elapsed time in seconds.
                float time = _Time.y;

                // The river is expected to run in the UV Y direction.
                float2 p = float2(
                    uv.x * _WidthScale,
                    uv.y * _LengthScale
                );

                // Main wave layer.
                float mainY = p.y - time * _FlowSpeed;

                float mainNoise = FBM(
                    float2(p.x * 0.72, mainY * 0.14) +
                    float2(time * 0.025, 0.0)
                );

                float mainWave = 0.5 + 0.5 * sin(
                    mainY * _WaveDensity +
                    p.x * 0.65 +
                    mainNoise * _Distortion
                );

                float mainBreakup = smoothstep(
                    _Breakup,
                    1.0,
                    FBM(
                        float2(p.x * 1.18, mainY * 0.11) +
                        float2(time * 0.055, -time * 0.025)
                    )
                );

                float mainLines =
                    NarrowBrightLine(mainWave, _LineThickness) *
                    mainBreakup;

                // Secondary wave layer: thinner, weaker and slightly slower.
                float secondaryY = p.y - time * _SecondarySpeed;

                float secondaryNoise = FBM(
                    float2(p.x * 1.24, secondaryY * 0.19) +
                    float2(-time * 0.035, time * 0.018)
                );

                float secondaryWave = 0.5 + 0.5 * sin(
                    secondaryY * _SecondaryDensity -
                    p.x * 0.92 +
                    secondaryNoise * (_Distortion * 0.72)
                );

                float secondaryBreakup = smoothstep(
                    min(_Breakup + 0.08, 0.98),
                    1.0,
                    FBM(
                        float2(p.x * 1.55, secondaryY * 0.16) +
                        float2(-time * 0.04, time * 0.03)
                    )
                );

                float secondaryLines =
                    NarrowBrightLine(secondaryWave, _SecondaryThickness) *
                    secondaryBreakup *
                    _SecondaryStrength;

                // A few faint, short-lived highlights prevent the water
                // from looking like a mechanically scrolling texture.
                float glintNoise = FBM(
                    float2(p.x * 2.1, mainY * 0.23) +
                    float2(time * 0.11, -time * 0.06)
                );

                float glints =
                    smoothstep(0.80, 0.97, glintNoise) *
                    smoothstep(
                        0.90,
                        1.0,
                        0.5 + 0.5 * sin(mainY * 5.4 + p.x * 2.7)
                    ) *
                    0.22;

                float waveMask = saturate(
                    mainLines +
                    secondaryLines +
                    glints
                );

                half3 finalColor = lerp(
                    _BaseColor.rgb,
                    _LineColor.rgb,
                    saturate(waveMask * _Brightness)
                );

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
