// 灯岸 LANTERN — by Spark · 四个固定判断机位（three 空间，Y-up，米）
// 构图只做两件事：
//   1 让河面/湿街在前景占大片黑，城市悬在远处（水在前、城在后）；
//   2 冷键落在城的另一侧，城市是逆光的黑，只有边、灯河与焦点是亮的。

export const SHOTS = [
  {
    id: '01-poster', name: '海报全景',
    note: '西侧高位斜俯：河在前景，市中心悬在雾里，剧院暖点是唯一焦点',
    pos: [-700, 120, 380], target: [-140, 20, -60], fov: 30,
  },
  {
    id: '02-river', name: '河上北望',
    note: '贴水面往北：河是黑镜子，城市是一条发光的岸',
    pos: [80, 13, 430], target: [-130, 45, -220], fov: 33,
  },
  {
    id: '03-dock', name: '码头灯河',
    note: '沿岸线看过去：仓库剪影 + 一串钠灯在雾里排成河',
    pos: [60, 16, 130], target: [-190, 22, -40], fov: 44,
  },
  {
    id: '04-focus', name: '焦点',
    note: '收束到剧院街口：暖池 + 檐口红霓虹，其余沉黑',
    pos: [-430, 55, 170], target: [-235, 30, -20], fov: 24,
  },
];

export function applyShot(camera, controls, index) {
  const shot = SHOTS[((index % SHOTS.length) + SHOTS.length) % SHOTS.length];
  camera.position.set(...shot.pos);
  camera.fov = shot.fov;
  camera.updateProjectionMatrix();
  if (controls) {
    controls.target.set(...shot.target);
    controls.update();
  } else {
    camera.lookAt(...shot.target);
  }
  return shot;
}
