import * as THREE from "three";
import { Container, Graphics, Text, WebGLRenderer as PixiRenderer } from "pixi.js";
import { evaluateScheme } from "./scheme/evaluate";

const canvas = document.querySelector<HTMLCanvasElement>("#game");
const errorLabel = document.querySelector<HTMLElement>("#error");
if (!canvas || !errorLabel) throw new Error("Missing web canvas or error output.");

async function start(): Promise<void> {
  const three = new THREE.WebGLRenderer({ canvas, antialias: true, stencil: true });
  three.setPixelRatio(1);
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x111217);

  const camera = new THREE.PerspectiveCamera(42, 16 / 9, 0.1, 100);
  camera.position.set(7, 7, 12);
  camera.lookAt(0, 1, 0);

  const buildings: THREE.Mesh[] = [];
  const color = new THREE.MeshLambertMaterial({ color: 0x252c36 });
  for (let i = 0; i < 8; i++) {
    const height = 2 + (i % 4) * 1.15;
    const building = new THREE.Mesh(new THREE.BoxGeometry(1.3, height, 1.6), color);
    building.position.set((i - 3.5) * 1.55, height / 2, i % 2 ? -1.4 : 0.4);
    buildings.push(building);
    scene.add(building);
  }
  const street = new THREE.Mesh(
    new THREE.PlaneGeometry(40, 40),
    new THREE.MeshLambertMaterial({ color: 0x171b23 })
  );
  street.rotation.x = -Math.PI / 2;
  street.position.y = -0.04;
  scene.add(street);
  scene.add(new THREE.AmbientLight(0xbfc8df, 1.8));
  const key = new THREE.DirectionalLight(0xffd8a6, 3.2);
  key.position.set(-3, 9, 4);
  scene.add(key);

  const pixi = new PixiRenderer();
  await pixi.init({
    context: three.getContext(),
    width: 1024,
    height: 576,
    clearBeforeRender: false,
    preference: "webgl"
  });
  const stage = new Container();

  const panel = new Graphics().roundRect(30, 350, 370, 190, 12).fill({ color: 0x171b24, alpha: 0.94 })
    .stroke({ color: 0x9b9fa7, width: 1 });
  stage.addChild(panel);
  const title = new Text({
    text: "SSNoir  /  技术底座",
    style: { fontFamily: "sans-serif", fontSize: 27, fill: 0xf3eee3 }
  });
  title.position.set(52, 375);
  stage.addChild(title);

  const report = new Text({
    text: "Scheme：正在验证",
    style: { fontFamily: "sans-serif", fontSize: 17, fill: 0xd6c6aa }
  });
  report.position.set(52, 424);
  stage.addChild(report);

  const button = new Graphics().roundRect(50, 458, 220, 52, 8)
    .fill(0xe8d6ae).stroke({ color: 0xfff5d8, width: 1 });
  button.eventMode = "static";
  button.cursor = "pointer";
  stage.addChild(button);

  const buttonLabel = new Text({
    text: "执行 Scheme  (+ 1 n)",
    style: { fontFamily: "sans-serif", fontSize: 16, fill: 0x1b1e24 }
  });
  buttonLabel.position.set(65, 472);
  stage.addChild(buttonLabel);

  let count = 0;
  function execute(): void {
    const value = evaluateScheme(`(let ((n ${count})) (+ 1 n))`);
    if (typeof value !== "number") throw new Error("Unexpected Scheme result: " + String(value));
    count = value;
    report.text = `Scheme 计算结果：${count}`;
  }
  // Read-only browser hook for deterministic Playwright interaction tests.
  Object.assign(window, { __SSNOIR_FOUNDATION__: { getSchemeValue: () => count } });
  button.on("pointertap", () => {
    try { execute(); } catch (error) { errorLabel.textContent = String(error); }
  });
  execute();

  function resize(): void {
    const width = Math.max(1, window.innerWidth);
    const height = Math.max(1, window.innerHeight);
    three.setSize(width, height, false);
    pixi.resize(width, height);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    const scale = Math.min(width / 1024, height / 576);
    stage.scale.set(scale);
    stage.position.set((width - 1024 * scale) / 2, (height - 576 * scale) / 2);
  }
  window.addEventListener("resize", resize);
  resize();
  three.setAnimationLoop(() => {
    three.resetState();
    three.render(scene, camera);
    pixi.resetState();
    pixi.render({ container: stage, clear: false });
  });
}
start().catch(error => { errorLabel.textContent = "Foundation failed: " + String(error?.stack ?? error); });
