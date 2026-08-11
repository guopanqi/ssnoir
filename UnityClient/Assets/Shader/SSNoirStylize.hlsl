#ifndef SSNOIR_STYLIZE_INCLUDED
#define SSNOIR_STYLIZE_INCLUDED

// 统一涂装的核心运算。实时世界和过场视频两条路都调这一个函数——这正是整件事的目的：
// 只要两边过的是同一段代码、拿的是同一份参数，底下来源的色差、锐度差、压缩噪声就被同化成
// 同一种画面材质。任何一边偷偷走别的分支，缝就回来了。
//
// 所有运算都在显示值（sRGB 编码后）空间里做，不在线性空间。量化必须在感知空间进行，否则
// 档位会全部挤到高光去，暗部一档都分不到——而这套画面九成是暗部。两条调用路径各自负责把
// 输入转成显示值再进来。

// 抖动图案。属于「屏幕」而不属于「内容」——它按屏幕像素定位，不跟着物体或视频内容走。
// 世界和视频因此被同一张网罩住，这是统一感的来源。
static const float SSNOIR_BAYER4[16] =
{
     0.0,  8.0,  2.0, 10.0,
    12.0,  4.0, 14.0,  6.0,
     3.0, 11.0,  1.0,  9.0,
    15.0,  7.0, 13.0,  5.0
};

// 色阶曲线的四个锚点，全是显示值。A 是纯黑端，D 是纯白端，B/C 是中间两档，位置可调。
float4 _RampA;
float4 _RampB;
float4 _RampC;
float4 _RampD;
float  _RampBPos;
float  _RampCPos;

// IMGUI 那条路（过场视频）两头的传递曲线。当前经过真机校准的契约是输入、输出都为 1：
// VideoPlayer 的显示值直入，GUI Pass 原样写出。场景全屏 Pass 是否执行与这条视频链路无关，
// 不能因为场景 Intensity 为零就停用共享材质或把视频退回另一套绘制路径。
//
// 两个指数仍独立保留给以后重新校准视频来源；pow 保端点，只改变中间调。
// 1.0 = 原样，2.2 ≈ 一次较强的 Gamma 变换。
float _GuiInputGamma;
float _GuiOutputGamma;

float _InBlack;         // 输入黑点
float _InWhite;         // 输入白点
float _LumaGamma;       // 归一化之后的曲线，<1 提亮暗部

float _Levels;          // 量化档数
float _DitherScale;     // 一个抖动格子占几个屏幕像素
float _DitherStrength;  // 0 = 硬色带，1 = 全抖动
float _Intensity;       // 0 = 原样，1 = 全涂装

float SSNoirLuma(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

/// 亮度 → 那条 黑→夜蓝→蓝灰→白 的曲线。
///
/// 三段串起来靠 saturate 自己截断：低于某一段的起点时，那一段的插值系数是负数、被 saturate
/// 压成 0，于是不生效。不用写分支。
float3 SSNoirRamp(float l)
{
    float3 c = lerp(_RampA.rgb, _RampB.rgb, saturate(l / max(1e-4, _RampBPos)));
    c = lerp(c, _RampC.rgb, saturate((l - _RampBPos) / max(1e-4, _RampCPos - _RampBPos)));
    c = lerp(c, _RampD.rgb, saturate((l - _RampCPos) / max(1e-4, 1.0 - _RampCPos)));
    return c;
}

/// <param name="src">显示值空间的原色</param>
/// <param name="pixelPos">屏幕像素坐标（SV_Position.xy）</param>
float3 SSNoirStylize(float3 src, float2 pixelPos)
{
    // 第一味药：取亮度，丢掉色相。输入是暖是冷从这里开始就不影响输出了。
    float l = saturate(SSNoirLuma(src));

    // 黑白点。这一步不是为了好看，是统一化真正生效的地方。
    //
    // 实测过同一镜的两个来源：3D 渲染的亮度九成落在 0.14 以下、最高冲到 0.996，视频那边最高
    // 只有 0.830——亮端差了一大截，直接分档的话两边永远对不齐。把白点压到远低于两者上限的
    // 位置，两边的亮部就都顶到同一档，差异被强行吃掉。顺带也把档位从画面根本没用到的亮部
    // 区间收回来：不做这一步，六档里有四档是空的。
    l = saturate((l - _InBlack) / max(1e-4, _InWhite - _InBlack));
    l = pow(l, max(0.01, _LumaGamma));

    // 第二味药：量化 + 有序抖动。
    //
    // 格子大小按 720p 归一化，不是给死的像素数。三个原因：
    //
    // 1. 网点密度不能随窗口大小漂。玩家换个分辨率、或者 WebGL 的画布被缩放，画面的「质感」
    //    就跟着变——而这套涂装的整个卖点就是质感恒定。
    // 2. 两条路的渲染目标尺寸不一定相等（相机 RT 有 render scale，IMGUI 走的是屏幕）。各按
    //    自己的 _ScreenParams 归一化，算出来的格子在屏幕上才是同一个大小；直接用像素数的话，
    //    两边尺寸一差，视频和世界的网纹粗细就对不上。
    // 3. 基准取 720 而不是 1080，是对着视频的原生分辨率定的——生成服务的上限就是 720p。
    //    网格至少和视频自己的像素一样粗，视频放大后的那点糊就永远细不过网格、显不出来。
    //    网格必须是画面里最粗的东西，它才压得住底下的来源差异。
    #define SSNOIR_DITHER_REFERENCE_HEIGHT 720.0
    float scale = max(
        1.0, _DitherScale * max(1.0, _ScreenParams.y) / SSNOIR_DITHER_REFERENCE_HEIGHT);
    uint2 cell = (uint2)floor(pixelPos / scale);
    float bayer = SSNOIR_BAYER4[(cell.y & 3) * 4 + (cell.x & 3)] / 16.0;
    bayer = lerp(0.5, bayer, saturate(_DitherStrength));

    float steps = max(2.0, _Levels) - 1.0;
    float quantized = saturate(floor(l * steps + bayer) / steps);

    return SSNoirRamp(quantized);
}

#endif
