export const SHOTS = Object.freeze([
  Object.freeze({
    name: '01 · THEATER STREET',
    position: [-31, 4.3, 4.0],
    target: [0, 5.4, -16.2],
    purpose: '从道路斜看完整剧院正面；测试地标留白、主街纵深与第一阅读层级',
  }),
  Object.freeze({
    name: '02 · WAREHOUSE FOG',
    position: [55, 4.7, -5.5],
    target: [30, 4.4, 16.0],
    purpose: '从道路对角看工业体量；测试山墙轮廓、前景道路与空气层次',
  }),
  Object.freeze({
    name: '03 · ALLEY MOUTH',
    position: [-21.5, 2.8, 4.4],
    target: [-21.5, 3.0, 34.0],
    purpose: '正看后巷；测试两侧墙面、负空间和唯一远端亮点',
  }),
  Object.freeze({
    name: '04 · HIGH CITY',
    position: [41, 27, 42],
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
