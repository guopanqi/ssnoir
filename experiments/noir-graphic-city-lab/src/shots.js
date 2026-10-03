export const SHOTS = Object.freeze([
  Object.freeze({
    id: '01-plaza-approach',
    name: '01 · PLAZA APPROACH',
    position: [10.8, 4.0, 39.5],
    target: [-3.7, 4.1, -13.5],
    fov: 43,
    purpose: '参考 Genesis Noir 城市场景：前景侦探、远处人群、密集亮窗、线稿街景与暗蓝空间同时成立。',
  }),
  Object.freeze({
    id: '02-spotlight-gathering',
    name: '02 · SPOTLIGHT GATHERING',
    position: [15, 5.8, 16],
    target: [-4.0, 5.0, -8],
    fov: 42,
    purpose: '测试体积光成为巨大白色图形，但人物和背景仍保留线稿与中间灰，而不是被纯黑吞没。',
  }),
  Object.freeze({
    id: '03-detective-silhouette',
    name: '03 · DETECTIVE SILHOUETTE',
    position: [10.4, 3.25, 27.5],
    target: [3.0, 2.9, 17.2],
    fov: 34,
    purpose: '中近景测试黑色实心人物、细白轮廓和少量内部线，避免低模块面感。',
  }),
  Object.freeze({
    id: '04-line-city',
    name: '04 · LINE CITY',
    position: [-3.0, 10.5, 46],
    target: [-5.0, 9.0, -26],
    fov: 30,
    purpose: '测试城市是否依靠大量白线、亮窗和中灰体块形成手绘空间，而不是少量描边的盒子城。',
  }),
]);

export function applyShot(camera, controls, index) {
  const shot = SHOTS[index];
  camera.fov = shot.fov;
  camera.updateProjectionMatrix();
  camera.position.fromArray(shot.position);
  controls.target.fromArray(shot.target);
  controls.update();
  return shot;
}
