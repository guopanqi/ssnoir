/**
 * 机位。全部是"城市视角"——没有一个是站在街上看的。
 *
 * 构图上只做两件事：
 *   1 让河面或湿街在前景占据大片黑，城市悬在远处（黑色电影的"水在前、城在后"）。
 *   2 让冷键落在城的另一侧，于是城市是一块逆光的黑，只有边和雾是亮的。
 *
 * 字段：pos/target 世界坐标（米），fov 垂直视角（度），
 *       rain/wet 雨量与湿度，density 雾浓度倍率，exposure 曝光，line 描线强度。
 */
export const SHOTS = [
  {
    id: '01-world', name: '世界机位',
    note: '游戏自带的城市视角（Camera_世界）。保留原构图，用来和现行画面直接对照',
    pos: [-1040, 960, 110], target: [-120, 20, -60], fov: 27,
    rain: 0.0, wet: 0.95, density: 0.55, exposure: 1.05, line: 0.55,
  },
  {
    id: '02-across', name: '隔河长焦',
    note: '东岸长焦压缩：河面斜切过前景，市中心在雾里，楼只剩窗',
    pos: [180, 62, 170], target: [-235, 55, -80], fov: 20,
    rain: 0.0, wet: 1.25, density: 1.10, exposure: 1.18, line: 1.0,
  },
  {
    id: '03-river', name: '河上北望',
    note: '贴着水面往北看：河是黑的镜子，城市是一条发光的岸',
    pos: [-40, 17, 430], target: [-70, 46, -300], fov: 28,
    rain: 0.22, wet: 1.45, density: 1.05, exposure: 1.15, line: 0.9,
  },
  {
    id: '04-port', name: '码头低角',
    note: '低机位沿旧港岸线看过去：仓库是剪影，只有一排钠灯在雾里',
    pos: [-5, 15, 62], target: [-74, 21, -54], fov: 40,
    rain: 0.0, wet: 1.10, density: 1.05, exposure: 1.15, line: 1.15,
  },
  {
    id: '05-rain', name: '雨夜',
    note: '雨最重的一格：雾更浓、地面更湿、反射拖得更长',
    pos: [252, 88, 196], target: [-236, 52, -84], fov: 24,
    rain: 1.0, wet: 1.60, density: 1.45, exposure: 1.32, line: 0.85,
  },
  {
    id: '06-silhouette', name: '逆光剪影',
    note: '极端逆光：城只剩轮廓，月亮把雾烧出一条亮带',
    pos: [-628, 68, 176], target: [-152, 88, -100], fov: 26,
    rain: 0.0, wet: 0.85, density: 1.40, exposure: 1.15, line: 1.35,
  },
  {
    id: '07-plan', name: '高处俯瞰',
    note: '高位斜俯：城市变成一块被光刻过的电路板，看清密度与肌理',
    pos: [-140, 640, 640], target: [-160, 0, -80], fov: 30,
    rain: 0.0, wet: 0.95, density: 0.60, exposure: 1.05, line: 0.60,
  },
];
