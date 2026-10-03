export const SHOTS = Object.freeze([
  Object.freeze({
    name: '01 · THEATER STREET',
    position: [-31, 9.5, 31],
    target: [-2, 5.0, 2],
    purpose: '主街纵深、剧院主体、第一阅读层级',
  }),
  Object.freeze({
    name: '02 · WAREHOUSE FOG',
    position: [54, 8.2, 18],
    target: [25, 4.6, -6],
    purpose: '工业体量、雾与远近层次',
  }),
  Object.freeze({
    name: '03 · ALLEY MOUTH',
    position: [-45, 5.4, -3],
    target: [-24, 3.3, -19],
    purpose: '狭窄空间、遮挡与黑面积',
  }),
  Object.freeze({
    name: '04 · HIGH CITY',
    position: [7, 34, 50],
    target: [0, 4.0, 0],
    purpose: '整体信息密度与城市轮廓',
  }),
]);

export function applyShot(camera, controls, index) {
  const shot = SHOTS[index];
  camera.position.fromArray(shot.position);
  controls.target.fromArray(shot.target);
  controls.update();
  return shot;
}
