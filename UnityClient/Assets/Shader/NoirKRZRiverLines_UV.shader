Shader "Noir/KRZ River Lines UV"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor ("Water Base Color", Color) = (0.006, 0.010, 0.020, 1)
        [HDR] _LineColor ("Wave Line Color", Color) = (0.58, 0.64, 0.72, 1)
        _LineStrength ("Overall Line Strength", Range(0, 2)) = 0.78

        [Header(River UV Coordinates)]
        _LengthScale ("Pattern Scale Along River", Range(0.1, 60)) = 14
        _WidthScale ("Pattern Scale Across River", Range(0.1, 30)) = 4
        _LineAngle ("Small Line Angle Offset", Range(-30, 30)) = 0

        [Header(Main Lines)]
        _LineSpacing ("Line Spacing", Range(0.15, 5)) = 1.10
        _LineWidth ("Line Width", Range(0.002, 0.25)) = 0.035
        _Amplitude ("Sway Amplitude", Range(0, 2)) = 0.22
        _Frequency ("Sway Frequency", Range(0.02, 3)) = 0.42
        _SecondaryAmplitude ("Secondary Sway Amplitude", Range(0, 1)) = 0.09
        _SecondaryFrequency ("Secondary Sway Frequency", Range(0.02, 3)) = 0.18
        _DriftSpeed ("Drift Speed", Range(-3, 3)) = 0.30
        _SwaySpeed ("Secondary Sway Speed", Range(-3, 3)) = 0.16
        _LineJitter ("Line Position Jitter", Range(0, 0.45)) = 0.12

        [Header(Intermittent Visibility)]
        _SegmentLength ("Visible Segment Length", Range(0.5, 30)) = 7
        _SegmentCutoff ("Sparse Line Cutoff", Range(0, 1)) = 0.48
        _SegmentSoftness ("Segment Fade Softness", Range(0.01, 0.45)) = 0.17
        _FadeSpeed ("Appear Fade Speed", Range(0, 3)) = 0.34
        _MinimumVisibility ("Minimum Line Visibility", Range(0, 1)) = 0.04

        [Header(Crossing Lines)]
        _CrossingStrength ("Crossing Line Strength", Range(0, 1)) = 0.30
        _CrossingAngle ("Crossing Line Angle Offset", Range(-45, 45)) = 11
        _CrossingSpacing ("Crossing Line Spacing", Range(0.15, 6)) = 1.65
        _CrossingWidth ("Crossing Line Width", Range(0.002, 0.25)) = 0.025
        _CrossingAmplitude ("Crossing Sway Amplitude", Range(0, 2)) = 0.18
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
            Name "KRZRiverLines"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM

            #pragma target 3.0
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
                float _LineStrength;

                float _LengthScale;
                float _WidthScale;
                float _LineAngle;

                float _LineSpacing;
                float _LineWidth;
                float _Amplitude;
                float _Frequency;
                float _SecondaryAmplitude;
                float _SecondaryFrequency;
                float _DriftSpeed;
                float _SwaySpeed;
                float _LineJitter;

                float _SegmentLength;
                float _SegmentCutoff;
                float _SegmentSoftness;
                float _FadeSpeed;
                float _MinimumVisibility;

                float _CrossingStrength;
                float _CrossingAngle;
                float _CrossingSpacing;
                float _CrossingWidth;
                float _CrossingAmplitude;
            CBUFFER_END

            #define TWO_PI 6.28318530718
            #define DEG_TO_RAD 0.01745329252

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            float Hash11(float value)
            {
                return frac(sin(value * 127.1 + 311.7) * 43758.5453123);
            }

            float2 Rotate2D(float2 position, float radians)
            {
                float sineValue;
                float cosineValue;
                sincos(radians, sineValue, cosineValue);

                return float2(
                    cosineValue * position.x - sineValue * position.y,
                    sineValue * position.x + cosineValue * position.y
                );
            }

            float DrawSingleLine(
                float2 coordinates,
                float lineIndex,
                float spacing,
                float width,
                float amplitude,
                float frequency,
                float driftSpeed,
                float secondaryAmplitude,
                float secondaryFrequency,
                float swaySpeed,
                float time,
                float familySeed
            )
            {
                float phaseA = Hash11(lineIndex * 2.71 + familySeed + 1.0) * TWO_PI;
                float phaseB = Hash11(lineIndex * 3.93 + familySeed + 7.0) * TWO_PI;
                float phaseC = Hash11(lineIndex * 5.17 + familySeed + 13.0) * TWO_PI;
                float phaseD = Hash11(lineIndex * 7.31 + familySeed + 19.0) * TWO_PI;

                float randomSpeed = lerp(
                    0.72,
                    1.30,
                    Hash11(lineIndex * 4.13 + familySeed + 23.0)
                );

                float randomAmplitude = lerp(
                    0.62,
                    1.22,
                    Hash11(lineIndex * 6.47 + familySeed + 29.0)
                );

                float randomOffset =
                    (Hash11(lineIndex * 8.59 + familySeed + 31.0) - 0.5) *
                    spacing *
                    _LineJitter;

                float basePosition = lineIndex * spacing + randomOffset;

                // The line is primarily a long, continuous curve.
                // Two slow sine waves make it sway without turning into noise.
                float curve =
                    basePosition +
                    sin(
                        coordinates.x * frequency +
                        time * driftSpeed * randomSpeed +
                        phaseA
                    ) *
                    amplitude *
                    randomAmplitude +
                    sin(
                        coordinates.x * secondaryFrequency -
                        time * swaySpeed * randomSpeed +
                        phaseB
                    ) *
                    secondaryAmplitude;

                float distanceToLine = abs(coordinates.y - curve);

                // Screen-space antialiasing keeps narrow lines stable when
                // the camera is far away or viewed at an angle.
                float antialiasing = max(fwidth(distanceToLine) * 1.35, 0.0001);
                float lineMask =
                    1.0 -
                    smoothstep(width, width + antialiasing, distanceToLine);

                // Long patches fade in and out. This leaves large black areas
                // instead of covering the river with a continuous pattern.
                float safeSegmentLength = max(_SegmentLength, 0.001);

                float segmentA =
                    0.5 +
                    0.5 *
                    sin(
                        coordinates.x / safeSegmentLength +
                        time * _FadeSpeed * 0.72 +
                        phaseC
                    );

                float segmentB =
                    0.5 +
                    0.5 *
                    sin(
                        coordinates.x / (safeSegmentLength * 1.83) -
                        time * _FadeSpeed * 0.37 +
                        phaseD
                    );

                float segmentSignal = segmentA * 0.72 + segmentB * 0.28;

                float visibleSegment = smoothstep(
                    _SegmentCutoff - _SegmentSoftness,
                    _SegmentCutoff + _SegmentSoftness,
                    segmentSignal
                );

                float linePulseSignal =
                    0.5 +
                    0.5 *
                    sin(
                        time *
                        _FadeSpeed *
                        lerp(
                            0.32,
                            0.76,
                            Hash11(lineIndex * 9.71 + familySeed + 37.0)
                        ) +
                        phaseD
                    );

                float linePulse = lerp(
                    _MinimumVisibility,
                    1.0,
                    smoothstep(0.18, 0.82, linePulseSignal)
                );

                float brightness = lerp(
                    0.52,
                    1.0,
                    Hash11(lineIndex * 11.23 + familySeed + 41.0)
                );

                return lineMask * visibleSegment * linePulse * brightness;
            }

            float DrawLineFamily(
                float2 coordinates,
                float spacing,
                float width,
                float amplitude,
                float frequency,
                float driftSpeed,
                float secondaryAmplitude,
                float secondaryFrequency,
                float swaySpeed,
                float time,
                float familySeed
            )
            {
                float safeSpacing = max(spacing, 0.001);
                float nearestLineIndex = floor(coordinates.y / safeSpacing);

                float result = 0.0;

                // Evaluate nearby lines because a swaying curve can cross the
                // nominal boundary of its own spacing cell.
                [unroll]
                for (int offset = -2; offset <= 2; offset++)
                {
                    result = max(
                        result,
                        DrawSingleLine(
                            coordinates,
                            nearestLineIndex + offset,
                            safeSpacing,
                            width,
                            amplitude,
                            frequency,
                            driftSpeed,
                            secondaryAmplitude,
                            secondaryFrequency,
                            swaySpeed,
                            time,
                            familySeed
                        )
                    );
                }

                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y;

                // UV convention:
                // uv.y runs downstream along the river.
                // uv.x runs across the river from one bank to the other.
                //
                // The mesh can curve freely in world space. As long as its UV
                // island is unwrapped as a straight strip, the wave lines will
                // follow the bends of the river.
                float2 riverCoordinates = float2(
                    input.uv.y * _LengthScale,
                    (input.uv.x - 0.5) * _WidthScale
                );

                float mainAngle = _LineAngle * DEG_TO_RAD;
                float2 mainCoordinates = Rotate2D(riverCoordinates, mainAngle);

                float mainLines = DrawLineFamily(
                    mainCoordinates,
                    _LineSpacing,
                    _LineWidth,
                    _Amplitude,
                    _Frequency,
                    _DriftSpeed,
                    _SecondaryAmplitude,
                    _SecondaryFrequency,
                    _SwaySpeed,
                    time,
                    0.0
                );

                // A weaker second family intersects the main curves at a
                // slight angle, creating the occasional crossed-line effect.
                float crossingAngle =
                    (_LineAngle + _CrossingAngle) *
                    DEG_TO_RAD;

                float2 crossingCoordinates =
                    Rotate2D(riverCoordinates, crossingAngle);

                float crossingLines = DrawLineFamily(
                    crossingCoordinates,
                    _CrossingSpacing,
                    _CrossingWidth,
                    _CrossingAmplitude,
                    _Frequency * 0.83,
                    -_DriftSpeed * 0.68,
                    _SecondaryAmplitude * 0.75,
                    _SecondaryFrequency * 1.19,
                    -_SwaySpeed * 0.74,
                    time,
                    83.0
                );

                float waveMask = saturate(
                    mainLines +
                    crossingLines * _CrossingStrength
                );

                half3 finalColor = lerp(
                    _BaseColor.rgb,
                    _LineColor.rgb,
                    saturate(waveMask * _LineStrength)
                );

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
