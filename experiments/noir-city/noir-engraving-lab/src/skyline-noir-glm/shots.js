// 四个固定机位：本实验的判断只以这些镜头为准。
// 1 海报全景：抬高俯瞰，屋顶海 -> 市中心 -> 远景带三层
// 2 大道：站在南北大道里，灯池导视 + 高架横切 + 主角塔收束
// 3 屋顶前景：钟塔居中偏左，主角塔偏右，屋顶件压底
// 4 逆光剪影：月亮贴着主角塔冠，烟囱与老城剪影

export const SHOTS = [
  {
    id: '01-poster-vista',
    name: '01 · 海报全景',
    pos: [-45, 58, 335],
    target: [5, 62, -70],
    fov: 36,
  },
  {
    id: '02-main-street',
    name: '02 · 大道',
    pos: [0, 9, 172],
    target: [0, 44, -70],
    fov: 50,
  },
  {
    id: '03-viaduct',
    name: '03 · 高架',
    pos: [170, 13.5, 110],
    target: [-150, 28, 60],
    fov: 46,
  },
  {
    id: '04-silhouette',
    name: '04 · 逆光剪影',
    pos: [269, 31, 79],
    target: [-30, 128, -60],
    fov: 42,
  },
];

export function applyShot(camera, controls, index) {
  const shot = SHOTS[((index % SHOTS.length) + SHOTS.length) % SHOTS.length];
  camera.fov = shot.fov;
  camera.position.set(...shot.pos);
  camera.updateProjectionMatrix();
  if (controls) {
    controls.target.set(...shot.target);
    controls.update();
  } else {
    camera.lookAt(...shot.target);
  }
  return shot;
}
