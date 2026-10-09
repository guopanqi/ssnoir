// 灯岸 LANTERN — by Spark · 全局视觉总谱
//
// 一句话论点：城市是一排剪影，一条灯河，一个焦点。
//   - 前景（河/湿街）是黑的镜子，只拉出垂直光带；
//   - 中景（整城体块）是实体黑，只有朝向对的碎面和冷银边浮出来；
//   - 焦点（剧院檐口一带）是全城唯一的暖+红，眼睛落点只此一处。
//
// 和黑水 / 剪影夜城的刻意区别（独立设计，不沿袭）：
//   黑水 = 逐像素体积雾 raymarch + 半分辨率镜像反射 RT + 屏幕空间逆光线；
//   剪影夜城 = 程序化城市 + inverted-hull 墨线壳 + 官方 UnrealBloom 直出海报；
//   灯岸 = 真实城市几何 + 前向解析光（无 raymarch、无反射 RT、无深度预 pass）
//          + 湿带只做纵向解析拉丝 + 轮廓只做法线 fresnel 银边。
// 本文件是唯一允许出现 magic number 的地方。

export const PROFILE = {
  seed: 20261004,

  city: {
    dataBase: './heishui/', // 冻结快照（与黑水共用同一份烘焙，不碰 CityBox 流水线）
    mirrorY: 0.62,
    near: 2,
    far: 9000,
  },

  // 九片区的黑度：越暗 = 越旧越穷。全城唯一的"材质差异"，其余交给光。
  districtTint: {
    oldport: [0.0140, 0.0168, 0.0224],
    village: [0.0121, 0.0140, 0.0187],
    downtown: [0.0224, 0.0261, 0.0355],
    civic: [0.0205, 0.0233, 0.0299],
    enclave: [0.0149, 0.0177, 0.0224],
    oldtown: [0.0168, 0.0196, 0.0261],
    newport: [0.0187, 0.0215, 0.0280],
    newgrid: [0.0196, 0.0224, 0.0299],
    outskirt: [0.0131, 0.0149, 0.0196],
  },
  districtOrder: ['civic', 'downtown', 'enclave', 'newgrid', 'newport', 'oldport', 'oldtown', 'outskirt', 'village'],
  flatTint: {
    struct: [0.020, 0.024, 0.034], // 桥/驳岸/广场：主角级，略提半档与填充楼拉开层级
    shell: [0.024, 0.028, 0.040], // 地点外壳：主角级最亮
    ground: [0.016, 0.019, 0.028], // 城市地面/街区/干道
    land: [0.010, 0.012, 0.018], // 郊野：最黑，压住画面边缘
    water: [0.008, 0.012, 0.022], // 河面本体黑，亮全靠拉丝
  },

  // 冷键：城东偏北的月亮，低角度。相机在西侧时城市是逆光的黑。
  moon: {
    toLight: [0.68, 0.32, -0.55],
    color: [0.46, 0.66, 1.0],
    intensity: 1.0,
    terminator: [0.30, 0.70], // 硬终止线：屋顶几乎不亮，正对月亮的立面才亮
    specPower: 160.0,
    specGain: 1.2,
  },

  // 暖池：8 盏，每盏都有动机。three 空间 (x, y=高, z=-blenderY)。
  // r = 表面照度半径（米），i = 强度。灯只洗街面往上十几米（uHeightFall）。
  lamps: [
    { p: [-10, 10, -170], r: 50, i: 1.30, c: 'sodium', note: '码头岸线·北' },
    { p: [-20, 10, -60], r: 52, i: 1.35, c: 'sodium', note: '码头岸线·中' },
    { p: [-35, 10, 60], r: 48, i: 1.15, c: 'sodium', note: '码头岸线·南' },
    { p: [-238, 8, -100], r: 58, i: 1.45, c: 'lamp', note: '中心广场' },
    { p: [-252, 12, 30], r: 44, i: 1.25, c: 'lamp', note: '剧院街口（焦点暖池）' },
    { p: [21, 9, -30], r: 60, i: 1.20, c: 'sodium', note: '北桥' },
    { p: [132, 10, 96], r: 58, i: 0.90, c: 'lamp', note: '东岸滨江大道' },
    { p: [-206, 10, -20], r: 46, i: 0.95, c: 'sodium', note: '老城酒馆一带' },
  ],
  lampColors: {
    sodium: [1.0, 0.545, 0.18],
    lamp: [1.0, 0.76, 0.40],
    ember: [0.62, 0.24, 0.11],
  },
  heightFall: 0.062, // exp(-max(y-灯高,0)*k)：灯不染高楼，结构命门
  diffuseFloor: 0.22, // 漫反射下限耳语：黑块不成剪纸即可

  // 叙事色：全城只允许两点。焦点红在剧院檐口，远端绿在东岸。
  accents: [
    { p: [-258, 26, 28], c: [1.0, 0.10, 0.07], r: 26, i: 2.2, note: '剧院檐口霓虹（唯一焦点）' },
    { p: [120, 8, 90], c: [0.20, 1.0, 0.42], r: 12, i: 0.9, note: '东岸远端绿灯' },
  ],

  // 探照灯：2 道。只做加法锥体几何 + 雾中亮斑，不做体积 raymarch。
  spots: [
    { p: [26, 42, -150], aim: [-120, 0, -40], angle: 0.20, i: 2.4, sweep: 0.012, note: '码头上空缓扫' },
    { p: [-262, 95, 58], aim: [-150, 200, -40], angle: 0.17, i: 1.8, sweep: 0.030, note: '广场塔顶快扫' },
  ],

  // 轮廓银边（前向 fresnel，不读深度/法线贴图，不开预 pass）。
  rim: {
    color: [0.55, 0.68, 0.86],
    gain: 0.85,
    power: 3.0,
    base: 0.10, // 黑贴黑时的极弱底线，保住"被画出来"的身份
  },

  // 空气：前向解析高度雾（无 raymarch）。雾只染色，不发光墙。
  fog: {
    color: [0.012, 0.020, 0.038],
    density: 0.00095,
    heightScale: 42,
    glowPos: [-200, 10, -40], // 市中心方向的雾暖：只暖雾，不暖楼
    glowColor: [0.10, 0.055, 0.022],
    glowRadius: 260,
    glowGain: 0.3,
  },

  // 湿带：纵向解析拉丝（无反射 RT）。只有贴地与水面有。
  wet: {
    ground: 1.0,
    water: 1.6,
    streakNarrow: 0.35, // 水平瓣收窄：光带只沿纵向拉长
    rainRipple: 0.35, // 世界空间涟漪对 UV 的推开量（静态干夜，只取细碎）
  },

  // 窗光：合并面片，tier 定色温，hash 定明灭，量化时间定闪烁。
  windows: {
    tiers: [
      { color: [1.0, 0.93, 0.78], power: 1.0 },
      { color: [0.62, 0.60, 0.62], power: 0.55 },
      { color: [0.16, 0.24, 0.52], power: 0.32 },
    ],
    lift: 1.1,
    flickerFrac: 0.45,
    flickerRate: 0.25, // 量化台阶：floor(time*rate)，不量化就是噪点
    minOn: 0.14,
  },

  sky: {
    void: [0.004, 0.006, 0.010],
    deep: [0.012, 0.020, 0.038],
    horizon: [0.075, 0.105, 0.150],
    smog: [0.135, 0.098, 0.052], // 贴地平线的一线暖：城市自己的呼吸
    moonDisc: [0.85, 0.92, 1.0],
    moonSize: 0.99955, // dot 阈值：小而硬的月亮
    haloGain: 0.35,
    stars: 0.35,
  },

  lampPoints: {
    size: 5.0, // 灯头光斑世界尺寸（米），做"灯河"的颗粒
    gain: 1.6,
    twinkle: 0.25,
  },

  cones: {
    color: [0.62, 0.70, 0.80],
    opacity: 0.16,
  },

  post: {
    exposure: 1.0,
    levels: 12,
    dither: 0.6,
    contrastGain: 1.22,
    crush: 0.030,
    shadowTint: [0.90, 0.99, 1.14],
    highTint: [1.08, 1.01, 0.90],
    desat: 0.55,
    grain: 0.035,
    vignette: 0.60,
    aberration: 0.0008,
    bloom: { strength: 0.35, radius: 0.4, threshold: 0.8 },
  },
};
