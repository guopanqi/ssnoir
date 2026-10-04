/**
 * 黑水 · 自由观察
 *
 * 固定机位是**被设计过的构图**（联络表的四阶段证据全部出自它们），
 * 但研究画面不能只能看七张。这里补上拖动观察：
 *
 *   左键拖动   环视（绕当前机位的 target）
 *   滚轮       推拉
 *   右键 / 中键 平移（贴地，符合"城市视角"的直觉）
 *   双指       缩放 + 平移（触屏）
 *   R          回到当前固定机位
 *
 * 用 three 官方 OrbitControls，不自己写一套 —— 阻尼、触屏、平移的边界情况
 * 都在里面调好了，重写一遍只会引入新的差异。
 *
 * 唯一的自制部分：**地面约束**。OrbitControls 没有"相机不许钻到地面以下"，
 * 所以每帧按当前的 target 高度与距离反算极角上限：
 *   cos(phi) >= (FLOOR - targetY) / dist
 * 这样低机位（相机在 target 之下，比如码头那一格）仍然合法，
 * 而拉远之后不会穿到地底下去。
 */
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

/** 相机最低高度（米）。地面在 0 附近，水面在 -2.6。 */
const FLOOR = 5;

export function createControls(camera, dom) {
  const c = new OrbitControls(camera, dom);
  c.enableDamping = true;
  c.dampingFactor = 0.075;
  c.rotateSpeed = 0.62;
  c.zoomSpeed = 0.9;
  c.panSpeed = 0.9;
  c.screenSpacePanning = false;        // 平移贴着地面走
  c.minDistance = 24;
  c.maxDistance = 7000;
  c.minPolarAngle = 0.05;
  c.mouseButtons = { LEFT: THREE.MOUSE.ROTATE, MIDDLE: THREE.MOUSE.DOLLY, RIGHT: THREE.MOUSE.PAN };
  c.touches = { ONE: THREE.TOUCH.ROTATE, TWO: THREE.TOUCH.DOLLY_PAN };

  const _p = new THREE.Vector3();
  const _t = new THREE.Vector3();

  /** 把相机摆到某个固定机位。 */
  function setShot(shot) {
    camera.position.set(...shot.pos);
    c.target.set(...shot.target);
    camera.lookAt(c.target);
    update();
  }

  /** 每帧调用（阻尼需要）。同时收紧极角上限，保证不钻地。 */
  function update() {
    const d = Math.max(camera.position.distanceTo(c.target), 1e-3);
    const cos = Math.min(1, Math.max(-1, (FLOOR - c.target.y) / d));
    c.maxPolarAngle = Math.acos(cos);
    c.update();
  }

  /** 当前相机偏离这个固定机位多远（米）。用来判断"已进入自由观察"。 */
  function deviation(shot) {
    _p.set(...shot.pos);
    _t.set(...shot.target);
    return Math.max(camera.position.distanceTo(_p), c.target.distanceTo(_t));
  }

  return { controls: c, setShot, update, deviation, floor: FLOOR };
}
