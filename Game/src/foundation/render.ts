import * as THREE from "three";
import { Container, Graphics, Text, WebGLRenderer as PixiRenderer } from "pixi.js";
import { SchemeSession } from "../scheme/evaluate";

export interface FoundationOptions {
  canvas: HTMLCanvasElement;
  width: number;
  height: number;
  context?: WebGL2RenderingContext;
  animationFrame: (callback: FrameRequestCallback) => number;
  cancelAnimationFrame: (handle: number) => void;
}

export interface FoundationSession {
  increment(): void;
  activateAt(x: number, y: number): boolean;
  getCount(): number;
  resize(width: number, height: number): void;
  dispose(): void;
}

const VIRTUAL_WIDTH = 1024;
const VIRTUAL_HEIGHT = 576;
const BUTTON = { x: 50, y: 458, width: 220, height: 52 };

export async function mountFoundation(options: FoundationOptions): Promise<FoundationSession> {
  const three = new THREE.WebGLRenderer({
    canvas: options.canvas,
    context: options.context,
    antialias: true,
    stencil: true,
    powerPreference: "high-performance",
  });
  three.setPixelRatio(1);

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x111217);
  const camera = new THREE.PerspectiveCamera(42, options.width / options.height, 0.1, 100);
  camera.position.set(7, 7, 12);
  camera.lookAt(0, 1, 0);

  const blocks = new THREE.MeshLambertMaterial({ color: 0x252c36 });
  for (let i = 0; i < 8; i++) {
    const height = 2 + (i % 4) * 1.15;
    const building = new THREE.Mesh(new THREE.BoxGeometry(1.3, height, 1.6), blocks);
    building.position.set((i - 3.5) * 1.55, height / 2, i % 2 ? -1.4 : 0.4);
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
  const lamp = new THREE.DirectionalLight(0xffd8a6, 3.2);
  lamp.position.set(-3, 9, 4);
  scene.add(lamp);

  const sharedContext = three.getContext();
  if (typeof WebGL2RenderingContext !== "undefined" &&
      !(sharedContext instanceof WebGL2RenderingContext)) {
    throw new Error("PixiJS 8 shared rendering requires WebGL2");
  }
  const pixi = new PixiRenderer();
  await pixi.init({
    context: sharedContext as WebGL2RenderingContext,
    canvas: options.canvas,
    width: options.width,
    height: options.height,
    clearBeforeRender: false
  });
  const stage = new Container();
  stage.addChild(new Graphics().roundRect(30, 350, 370, 190, 12)
    .fill({ color: 0x171b24, alpha: 0.94 })
    .stroke({ color: 0x9b9fa7, width: 1 }));

  const title = new Text({
    text: "SSNoir / 技术底座",
    style: { fontFamily: "sans-serif", fontSize: 26, fill: 0xf3eee3 }
  });
  title.position.set(52, 375);
  stage.addChild(title);

  const counterText = new Text({
    text: "Scheme: 启动中",
    style: { fontFamily: "sans-serif", fontSize: 18, fill: 0xd6c6aa }
  });
  counterText.position.set(52, 423);
  stage.addChild(counterText);

  const button = new Graphics()
    .roundRect(BUTTON.x, BUTTON.y, BUTTON.width, BUTTON.height, 8)
    .fill(0xe8d6ae)
    .stroke({ color: 0xfff5d8, width: 1 });
  // UI input is normalized by each host and dispatched through activateAt().
  button.eventMode = "none";
  stage.addChild(button);
  const label = new Text({
    text: "执行 Scheme 计数",
    style: { fontFamily: "sans-serif", fontSize: 17, fill: 0x1b1e24 }
  });
  label.position.set(65, 472);
  stage.addChild(label);

  const scheme = new SchemeSession();
  scheme.evaluate("(define foundation-count 0)");
  scheme.evaluate("(define (foundation-next!) (set! foundation-count (+ foundation-count 1)) foundation-count)");
  let count = 0;
  function increment(): void {
    const value = scheme.evaluate("(foundation-next!)");
    if (typeof value !== "number") throw new Error("Unexpected Scheme result: " + String(value));
    count = value;
    counterText.text = "Scheme 计算结果：" + count;
  }
  increment();

  let size = { width: options.width, height: options.height, scale: 1, left: 0, top: 0 };
  function resize(width: number, height: number): void {
    width = Math.max(1, width);
    height = Math.max(1, height);
    three.setSize(width, height, false);
    pixi.resize(width, height);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    const scale = Math.min(width / VIRTUAL_WIDTH, height / VIRTUAL_HEIGHT);
    size = {
      width, height, scale,
      left: (width - VIRTUAL_WIDTH * scale) / 2,
      top: (height - VIRTUAL_HEIGHT * scale) / 2
    };
    stage.scale.set(scale);
    stage.position.set(size.left, size.top);
  }
  resize(options.width, options.height);

  // For WeChat, where the SDK provides touch events rather than HTML pointer events.
  // Expose coordinates in native screen space, then map to the exact same Pixi button.
  function activateAt(x: number, y: number): boolean {
    const xx = (x - size.left) / size.scale;
    const yy = (y - size.top) / size.scale;
    if (xx >= BUTTON.x && xx <= BUTTON.x + BUTTON.width &&
        yy >= BUTTON.y && yy <= BUTTON.y + BUTTON.height) {
      increment();
      return true;
    }
    return false;
  }

  let frame = 0;
  let disposed = false;
  function tick(): void {
    if (disposed) return;
    three.resetState();
    three.render(scene, camera);
    pixi.resetState();
    pixi.render({ container: stage, clear: false });
    frame = options.animationFrame(tick);
  }
  tick();

  return {
    increment,
    activateAt,
    getCount: () => count,
    resize,
    dispose() {
      disposed = true;
      options.cancelAnimationFrame(frame);
      pixi.destroy();
      scene.traverse(obj => {
        if (obj instanceof THREE.Mesh) obj.geometry.dispose();
      });
      blocks.dispose();
      three.dispose();
    }
  };
}
