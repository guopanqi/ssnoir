Shader "Noir/geminiRiverFlow"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseColor ("Base Color", Color) = (0.005, 0.01, 0.02, 1)
        _LineColor ("Wave Line Color", Color) = (0.7, 0.85, 1.0, 1)
        _LineGlow ("Glow Intensity", Range(1, 5)) = 2.0

        [Header(Flow Grid)]
        _FlowSpeed ("Flow Speed", Range(-5, 5)) = 0.8
        _GridScaleX ("Grid Scale X", Float) = 8.0
        _GridScaleY ("Grid Scale Y", Float) = 30.0

        [Header(Wave Distortion)]
        _DistortionFreq ("Distortion Frequency", Range(0.1, 5)) = 1.2
        _DistortionAmp ("Distortion Amplitude", Range(0, 5)) = 1.5

        [Header(Wave Lines)]
        _WaveDensity ("Wave Density", Range(1, 20)) = 5.0
        _LineThickness ("Line Thickness", Range(0.01, 0.2)) = 0.05
        _WaveBreakup ("Wave Line Breakup", Range(0, 1)) = 0.5

        [Header(Shimmering Glints)]
        _GlintSpeed ("Glint Speed", Range(0.1, 5)) = 1.5
        _GlintDensity ("Glint Density", Float) = 15.0
        _GlintThreshold ("Glint Threshold", Range(0.7, 0.99)) = 0.92

        [Header(Shoreline Glow)]
        _ShoreGlowRange ("Shore Glow Range", Range(0.01, 2.0)) = 0.5
        _ShoreGlowPower ("Shore Glow Power", Range(0.5, 4.0)) = 1.5
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldPos     : TEXCOORD1;
                float4 screenPos    : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                float _LineGlow;
                float _FlowSpeed;
                float _GridScaleX;
                float _GridScaleY;
                float _DistortionFreq;
                float _DistortionAmp;
                float _WaveDensity;
                float _LineThickness;
                float _WaveBreakup;
                float _GlintSpeed;
                float _GlintDensity;
                float _GlintThreshold;
                float _ShoreGlowRange;
                float _ShoreGlowPower;
            CBUFFER_END

            // 2D Hash
            float hash2d(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // 2D Value Noise
            float value_noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash2d(i);
                float b = hash2d(i + float2(1.0, 0.0));
                float c = hash2d(i + float2(0.0, 1.0));
                float d = hash2d(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.worldPos = vertexInput.positionWS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;

                // 1. Grid UV calculations
                float2 p = float2(input.uv.x * _GridScaleX, input.uv.y * _GridScaleY);
                float flowY = p.y - time * _FlowSpeed;

                // 2. Wave distortion noise
                float2 distUV = float2(p.x * _DistortionFreq, flowY * 0.2);
                float distNoise = value_noise(distUV) * _DistortionAmp;

                // 3. Wavy lines
                float sineWave = sin(flowY * _WaveDensity + p.x * 0.5 + distNoise);
                float normalizedWave = sineWave * 0.5 + 0.5;
                float mainLines = smoothstep(1.0 - _LineThickness, 1.0, normalizedWave);

                // Wave line breakup (discontinuity)
                float breakupNoise = value_noise(float2(p.x * 2.0, flowY * 0.5));
                float breakupMask = smoothstep(_WaveBreakup, _WaveBreakup + 0.1, breakupNoise);
                mainLines *= breakupMask;

                // 4. Shimmering Glints (Interference Pattern)
                float2 glintUV1 = p * _GlintDensity * 0.1 + float2(time * _GlintSpeed * 0.5, -time * _GlintSpeed * 0.2);
                float2 glintUV2 = p * _GlintDensity * 0.1 + float2(-time * _GlintSpeed * 0.3, time * _GlintSpeed * 0.6);
                
                float glint1 = value_noise(glintUV1);
                float glint2 = value_noise(glintUV2);
                float combinedGlint = glint1 * glint2;
                float glints = step(_GlintThreshold, combinedGlint);

                // 5. Shoreline Glow using Camera Depth Texture
                float depthGlow = 0.0;
                /*
                #if defined(_SCREEN_SPACE_OCCLUSION) || 1
                    // Retrieve screen coordinates
                    float4 screenPos = input.screenPos;
                    float2 screenUV = screenPos.xy / (screenPos.w + 0.00001);
                    
                    // Sample raw depth from depth texture
                    float rawDepth = SampleSceneDepth(screenUV);
                    
                    // Convert raw depth to linear eye depth (distance from camera in world units)
                    float sceneLinearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float surfaceLinearDepth = screenPos.w; // Perspective coordinate w holds the vertex depth in view space
                    
                    // Difference between scene geometry and water plane
                    float depthDiff = sceneLinearDepth - surfaceLinearDepth;
                    
                    if (depthDiff > 0.0 && depthDiff < _ShoreGlowRange)
                    {
                        depthGlow = 1.0 - saturate(depthDiff / _ShoreGlowRange);
                        depthGlow = pow(depthGlow, _ShoreGlowPower);
                    }
                #endif
                */

                // 6. Final Color Assembly
                float riverMask = saturate(mainLines + glints + depthGlow);
                half3 col = lerp(_BaseColor.rgb, _LineColor.rgb * _LineGlow, riverMask);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
