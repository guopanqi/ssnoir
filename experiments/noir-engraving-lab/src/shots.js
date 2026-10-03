export const SHOTS = Object.freeze([
  Object.freeze({
    name: '01 · THEATER STREET',
    position: [-38, 3.9, 6.5],
    target: [0, 5.0, -17.0],
    fov: 40,
    purpose: '低机位长街看剧院；测试地标留白、负空间和第一阅读层级',
  }),
  Object.freeze({
    name: '02 · WAREHOUSE FOG',
    position: [57, 4.5, -5.5],
    target: [30, 4.2, 17.0],
    fov: 42,
    purpose: '道路对角看仓库；测试工业轮廓、面积工作光与空气层次',
  }),
  Object.freeze({
    name: '03 · ALLEY MOUTH',
    position: [-20.7, 2.55, 6.8],
    target: [-21.25, 2.9, 39.0],
    fov: 38,
    purpose: '进入巷口但不过度贴墙；测试墙面切光、连桥遮挡和三层纵深',
  }),
  Object.freeze({
    name: '04 · HIGH CITY',
    position: [50, 17.5, 48],
    target: [0, 5.0, -4],
    fov: 34,
    purpose: '较低高位长焦压缩城市；测试前景遮挡、地标与远景天际线层级',
  }),
]);

export function applyShot(camera, controls, index) {
  const shot = SHOTS[index];
  camera.fov = shot.fov ?? 42;
  camera.updateProjectionMatrix();
  camera.position.fromArray(shot.position);
  controls.target.fromArray(shot.target);
  controls.update();
  return shot;
}
