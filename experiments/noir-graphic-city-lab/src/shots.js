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
    position: [-5.8, 3.1, 20.0],
    target: [0.0, 2.7, 10.3],
    fov: 38,
    purpose: '中近景双人交锋；测试人物是否靠背景明暗切割、帽檐/裙摆和少量实体标记成立，而不是靠全身描边。',
  }),
  Object.freeze({
    id: '04-city-canyon',
    name: '04 · CITY CANYON',
    position: [-4.8, 8.2, 43],
    target: [3.0, 6.7, -18],
    fov: 24,
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
