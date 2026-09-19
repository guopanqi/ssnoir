// 全屏统一涂装。两个 pass 干的是同一件事，区别只在「输入的颜色处于哪个空间」：
//
//   Pass 0（Blit）  给实时世界用，由 SSNoirStylizeFeature 在后处理之后插进管线。
//                   URP 的相机颜色缓冲装的是线性值，所以进出各转一次编码。
//
//   Pass 1（GUI）   给过场视频用，由 CutscenePlayer 在 OnGUI 里 Graphics.DrawTexture 调用。
//                   这条路上全程是显示值（视频解出来就是显示值，IMGUI 也照显示值往屏幕上
//                   画，中间不该有任何一次编码——见 CutscenePlayer 里那段注释），所以不转。
//
// 两条路最终写到屏幕上的都是 SSNoirStylize(显示值)，完全一致。这是设计要求，不是巧合：
// 哪一边多转或少转一次编码，视频和世界的明暗就对不上，整套统一化就白做了。
Shader "SSNoir/Stylize"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            Name "SSNoirStylizeBlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "SSNoirStylize.hlsl"

            TEXTURE2D(_CrossfadeTexture);
            SAMPLER(sampler_CrossfadeTexture);
            float _CrossfadeAlpha;
            // 世界负片：对白舞台进入负片时由 SSNoirStylizeMaterial.WorldInvert 推进来。
            // 放在最后，涂装、冻帧全部合成完再翻——翻的是"屏幕上那幅画"，不是某一层。
            float _WorldInvert;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 src = SAMPLE_TEXTURE2D_X(
                    _BlitTexture, sampler_LinearClamp, input.texcoord);

                float3 display = LinearToSRGB(saturate(src.rgb));
                float3 styled = SSNoirStylize(display, input.positionCS.xy);
                float3 live = lerp(display, styled, saturate(_Intensity));

                // 减少动画的冻帧也在显示值空间混合。若交给目标缓冲做普通 alpha blend，
                // Editor 与 WebGL 会因 sRGB 写入状态不同得到两种亮度；白描错位叠加时尤其明显。
                float3 frozenLinear = SAMPLE_TEXTURE2D(
                    _CrossfadeTexture, sampler_CrossfadeTexture, input.texcoord).rgb;
                float3 frozenDisplay = LinearToSRGB(saturate(frozenLinear));
                float3 composed = lerp(live, frozenDisplay, saturate(_CrossfadeAlpha));
                composed = lerp(composed, 1.0 - composed, saturate(_WorldInvert));

                return float4(SRGBToLinear(composed), src.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SSNoirStylizeGUI"

            HLSLPROGRAM
            #pragma vertex VertGUI
            #pragma fragment FragGUI

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // LinearToSRGB／SRGBToLinear 在这里。URP 的 Core.hlsl 不引它，Pass 0 是靠
            // Blit.hlsl 顺带引进来的，这条路没有，得自己引。
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "SSNoirStylize.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct AttributesGUI
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct VaryingsGUI
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            VaryingsGUI VertGUI(AttributesGUI input)
            {
                VaryingsGUI output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 FragGUI(VaryingsGUI input) : SV_Target
            {
                float3 raw = saturate(
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb);

                // 和 Pass 0 保持同一个约定：涂装一律在显示值空间里算。两头各自用可调指数
                // 对齐，理由见 SSNoirStylize.hlsl 里那两个 uniform 的注释。
                //
                // 垫一个极小值再 pow：pow 在 0 上各家硬件的行为不一致，垫完最暗端的误差是
                // 万分之几，看不出来。
                float3 display = pow(max(raw, 1e-5), 1.0 / max(0.1, _GuiInputGamma));
                float3 styled = SSNoirStylize(display, input.positionCS.xy);
                float3 result = lerp(display, styled, saturate(_Intensity));

                return float4(pow(max(result, 1e-5), max(0.1, _GuiOutputGamma)), 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
