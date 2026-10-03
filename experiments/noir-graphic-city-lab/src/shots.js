export const SHOTS = Object.freeze([
  Object.freeze({
    id: '01-theater-tableau',
    name: '01 · THEATER TABLEAU',
    position: [5.8, 5.2, 47],
    target: [0, 5.0, -25],
    fov: 36,
    purpose: '测试城市是否先读成大黑形，再由剧院白面、人物和少量金色建立叙事层级。',
  }),
  Object.freeze({
    id: '02-crowd-crossing',
    name: '02 · CROWD CROSSING',
    position: [-5.6, 3.5, 35],
    target: [1.2, 2.8, 18],
    fov: 42,
    purpose: '测试远近人物轮廓、帽檐/大衣比例和 crowd 的 Narrative LOD。',
  }),
  Object.freeze({
    id: '03-alley-confrontation',
    name: '03 · ALLEY CONFRONTATION',
    position: [-4.2, 2.7, 1.4],
    target: [-5.8, 2.9, 18],
    fov: 34,
    purpose: '测试近人物在黑背景前是否靠轮廓和少量结构信息成立，而不是靠脸部细节。',
  }),
  Object.freeze({
    id: '04-city-canyon',
    name: '04 · CITY CANYON',
    position: [6.8, 10.0, 52],
    target: [-2.5, 7.5, -22],
    fov: 28,
    purpose: '测试长焦压缩后，黑白图形关系能否形成一张平面设计而不是普通低模城市。',
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
