export const SHOTS = Object.freeze([
  Object.freeze({
    name: '01 · THEATER STREET',
    position: [-36, 5.4, 0.8],
    target: [-2, 6.0, 14.0],
    purpose: '沿主街斜望剧院：主体、街墙缺口、负空间',
  }),
  Object.freeze({
    name: '02 · WAREHOUSE FOG',
    position: [53, 7.2, 4.5],
    target: [28, 4.8, -15.0],
    purpose: '工业体量、街道纵深与远近层次',
  }),
  Object.freeze({
    name: '03 · ALLEY MOUTH',
    position: [-26, 3.8, 4.5],
    target: [-26, 3.0, -30.0],
    purpose: '从主街正看后巷：狭窄、遮挡与黑面积',
  }),
  Object.freeze({
    name: '04 · HIGH CITY',
    position: [9, 32, 46],
    target: [-2, 7.0, 5],
    purpose: '整体体块层级、地标与背景城市轮廓',
  }),
]);

export function applyShot(camera, controls, index) {
  const shot = SHOTS[index];
  camera.position.fromArray(shot.position);
  controls.target.fromArray(shot.target);
  controls.update();
  return shot;
}
