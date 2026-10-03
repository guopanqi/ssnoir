import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import './style.css';

import { PROFILE } from './config/visualProfile.js';
import { RENDER_LAYERS } from './config/layers.js';
import { buildWorld } from './scene/city.js';
import { createLighting } from './systems/lighting.js';
import { createAtmosphere } from './systems/atmosphere.js';
import { createPost } from './post/pipeline.js';
import { applyShot } from './shots.js';
import { bindResearchUI } from './ui.js';
import { installCaptureApi } from './captureApi.js';

const canvas = document.querySelector('#scene');

const renderer = new THREE.WebGLRenderer({
  canvas,
  antialias: true,
  powerPreference: 'high-performance',
});
renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.AgXToneMapping;
renderer.toneMappingExposure = PROFILE.renderer.exposure;

const scene = new THREE.Scene();

const camera = new THREE.PerspectiveCamera(
  PROFILE.camera.fov,
  innerWidth / innerHeight,
  PROFILE.camera.near,
  PROFILE.camera.far,
);

camera.layers.enable(RENDER_LAYERS.WAREHOUSE);
camera.layers.enable(RENDER_LAYERS.ALLEY);

const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.06;
controls.maxPolarAngle = Math.PI * 0.48;
controls.minDistance = 12;
controls.maxDistance = 90;

const world = buildWorld(scene, PROFILE);
const atmosphere = createAtmosphere(scene, PROFILE);
const lighting = createLighting(scene, PROFILE, world.lampPositions);
const post = createPost(renderer, scene, camera, PROFILE);

function renderFrame() {
  controls.update();
  post.composer.render();
}

const ui = bindResearchUI({
  profile: PROFILE,
  post,
  world,
  atmosphere,
  lighting,
  onShot(index) {
    return applyShot(camera, controls, index);
  },
});
ui.selectShot(0);

installCaptureApi({
  profile: PROFILE,
  camera,
  controls,
  world,
  atmosphere,
  lighting,
  post,
  applyShot,
  render: renderFrame,
});

function resize() {
  const width = innerWidth;
  const height = innerHeight;
  renderer.setSize(width, height, false);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
  post.resize(width, height);
}
window.addEventListener('resize', resize);
resize();

const captureMode = new URLSearchParams(location.search).has('capture');
const clock = new THREE.Clock();
let fpsFrames = 0;
let fpsTime = 0;

function animate() {
  requestAnimationFrame(animate);

  const dt = Math.min(clock.getDelta(), 0.05);
  atmosphere.update(dt);
  post.noir.uniforms.uTime.value += dt;
  renderFrame();

  fpsFrames += 1;
  fpsTime += dt;
  if (fpsTime >= 0.6) {
    ui.fps.textContent = `${Math.round(fpsFrames / fpsTime)} fps`;
    fpsFrames = 0;
    fpsTime = 0;
  }
}

if (captureMode) {
  renderFrame();
} else {
  animate();
}
