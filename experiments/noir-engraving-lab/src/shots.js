export const SHOTS = Object.freeze([
  Object.freeze({
    name: '01 · THEATER STREET',
    position: [-44, 4.7, 2.5],
    target: [-1.5, 5.2, -15.2],
    purpose: '低机位斜看剧院；测试主街纵深、地标留白与第一阅读层级',
  }),
  Object.freeze({
    name: '02 · WAREHOUSE FOG',
    position: [57, 5.5, -1.5],
    target: [29, 4.6, 16.2],
    purpose: '横跨道路看仓库；测试工业轮廓、灯光节奏与空气层次',
  }),
  Object.freeze({
    name: '03 · ALLEY MOUTH',
    position: [-21.5, 3.0, 3.4],
    target: [-21.5, 2.8, 31.5],
    purpose: '从道路正看后巷；测试狭窄空间、遮挡、负空间与单一远端亮点',
  }),
  Object.freeze({
    name: '04 · HIGH CITY',
    position: [43, 29, 45],
    target: [0, 4.5, 0],
    purpose: '斜俯视整体；测试地标、背景体块和城市信息密度是否形成层级',
  }),
]);

export function applyShot(camera, controls, index) {
  const shot = SHOTS[index];
  camera.position.fromArray(shot.position);
  controls.target.fromArray(shot.target);
  controls.update();
  return shot;
}
