// 逆光 BACKLIGHT · 固定机位
// 判断画风只看这五个镜头；自由环视用于检查，不用于结论。
// 坐标为 three (x, y, z)；城市范围 x[-660,470] z[-492,649]，主角塔在 (-110, 0, -53)。
export const SHOTS = [
  {
    id: '01-across',
    label: '隔河对望',
    note: '隔着河看市中心：发光地平线 + 塔的剪影是否第一眼成立',
    pos: [176, 14, 58],
    target: [-112, 58, -55],
    fov: 30,
    exposure: 1.05,
  },
  {
    id: '02-hero',
    label: '主角塔',
    note: '仰射灯下的立面：壁柱、收分、竖窗带、冠部是否读得出来',
    pos: [152, 34, 96],
    target: [-110, 70, -55],
    fov: 34,
    exposure: 1.05,
  },
  {
    id: '03-plan',
    label: '高位斜俯',
    note: '长影的方向与长度、街区明暗节奏、河的黑',
    pos: [300, 400, 330],
    target: [-150, 0, -70],
    fov: 34,
    exposure: 1.05,
  },
  {
    id: '04-boulevard',
    label: '干道纵深',
    note: '沿干道的纵深：雾的分档、钠灯珠的疏密、远近层级',
    pos: [-110, 14, -360],
    target: [-110, 60, -55],
    fov: 38,
    exposure: 1.10,
  },
  {
    id: '05-silhouette',
    label: '远景剪影',
    note: '天际线整体：塔与最高 81.5 m 填充楼的比例、god rays 是否在帮忙',
    pos: [452, 44, -232],
    target: [-200, 54, -90],
    fov: 22,
    exposure: 0.92,
  },
];
