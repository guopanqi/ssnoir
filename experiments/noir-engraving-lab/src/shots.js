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
    position: [-21.0, 2.45, 9.8],
    target: [-21.25, 2.9, 39.0],
    fov: 36,
    purpose: '真正进入巷口；测试硬质侧光、连桥阴影和近中远三层纵深',
  }),
  Object.freeze({
    name: '04 · CITY COMPRESSION',
    position: [72, 12.5, 1.5],
    target: [2, 5.0, -1.0],
    fov: 30,
    purpose: '沿街长焦压缩；测试城市峡谷、前景遮挡、地标和远景层级',
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
