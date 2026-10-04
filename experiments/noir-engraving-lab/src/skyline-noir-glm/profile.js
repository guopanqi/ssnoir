// SKYLINE NOIR (by GLM) — 全局视觉参数总谱。
// 本实验只回答一个问题：城市视角下，"剪影组织 + 少量叙事光"能否建立可读的 noir 海报画面。
// 所有可调数字集中在这里；模块里不许出现第二个.magic number 的家。

export const PROFILE = {
  seed: 20261004,

  sky: {
    topColor: 0x03060c,
    midColor: 0x0a141e,
    horizonColor: 0x2b3a47,
    smogBandColor: 0x4a3a20, // 贴地平线的暖雾带，城市自己的呼吸
    smogBandHeight: 0.14,
    moonDir: [-0.88, 0.29, -0.41], // 从场景指向月亮；方位角专为 04 剪影机位：月亮贴着主角塔冠，其余机位月亮出画
    moonColor: 0xdfe9ef,
    moonAngularRadius: 1.3, // 度
    moonHaloExponent: 320,
    moonHaloStrength: 0.3,
    starDensity: 0.22,
  },

  fog: {
    color: 0x22303d,
    density: 0.0019,
  },

  lighting: {
    moonLightColor: 0xb9c9da,
    moonLightIntensity: 0.95,
    hemiSky: 0x1d2733,
    hemiGround: 0x0a0c10,
    hemiIntensity: 0.62,
    lampColor: 0xffa04a,
    lampGlowSize: 8,
    lampPoolRadius: 13,
    lampPoolOpacity: 0.5,
    pointLightIntensity: 140,
    pointLightDistance: 90,
  },

  city: {
    grid: 48, // 街区间距
    blockHalf: 20, // 街块半宽（剩余 8m 是街沟）
    downtown: [30, -50],
    downtownRadius: 125,
    midRadius: 235,
    districtTint: {
      downtown: 0x1a212b,
      mid: 0x19212b,
      outer: 0x161b22,
      oldtown: 0x241d15, // 暖调深砖色
      industry: 0x1a1e26,
      far: 0x0d1016,
    },
    farBandCount: 42,
    farBandRadius: [620, 840],
    viaduct: { axis: 'x', at: 110, halfLength: 270, deckY: 10.5 },
    windowSpacing: [3.3, 3.6], // 列 / 行
    windowSize: [1.5, 2.0],
    litRatio: { downtown: 0.26, mid: 0.38, outer: 0.28, oldtown: 0.4, industry: 0.12 },
    windowBrightness: { downtown: 1.0, mid: 0.95, outer: 0.9, oldtown: 0.78, industry: 0.7 },
    windowTiers: [
      { color: [1.0, 0.63, 0.29], weight: 0.66 }, // 钠暖
      { color: [1.0, 0.8, 0.52], weight: 0.2 }, // 白炽
      { color: [0.6, 0.77, 0.88], weight: 0.1 }, // 冷青
      { color: [1.0, 0.93, 0.78], weight: 0.04 }, // 亮芯
    ],
    flickerWindowCount: 26,
  },

  ink: {
    color: 0x04060a,
    heroThickness: 0.0052,
    clockThickness: 0.0038,
    churchThickness: 0.003,
    gasometerThickness: 0.0026,
  },

  beacon: {
    color: 0xffedc4,
    intensity: 1.2,
    length: 130,
    radius: 16,
    elevation: 0.3,
    speed: 0.2,
  },

  smoke: {
    color: 0x04070b,
    opacity: 0.5,
  },

  post: {
    exposure: 1.04,
    crush: 0.004,
    shadowTint: [0.9, 0.99, 1.14], // 冷影子
    highTint: [1.08, 1.01, 0.9], // 暖高光
    vignette: 0.42,
    grain: 0.07,
    bloom: {
      strength: 0.5,
      threshold: 0.5,
      tintCore: [0.95, 1.0, 1.05], // 紧芯偏冷
      tintMid: [1.0, 0.92, 0.78],
      tintWide: [1.05, 0.82, 0.55], // 大晕偏钠暖
    },
  },

  shots: null, // 由 shots.js 提供，这里只留位
};
