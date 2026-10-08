import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import './style.css';

import { PROFILE } from './config/visualProfile.js';
import { buildWorld } from './scene/city.js';
import { createPost } from './post/pipeline.js';
import { SHOTS, applyShot } from './shots.js';
import { installCaptureApi } from './captureApi.js';

const renderer = new THREE.WebGLRenderer({
  canvas: document.querySelector('#scene'),
  antialias: false,
  powerPreference: 'high-performance',
});
renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.NoToneMapping;

const scene = new THREE.Scene();
scene.background = new THREE.Color(PROFILE.palette.background);
scene.fog = new THREE.FogExp2(PROFILE.palette.background, 0.0075);

const camera = new THREE.PerspectiveCamera(
  PROFILE.camera.fov,
  innerWidth / innerHeight,
  PROFILE.camera.near,
  PROFILE.camera.far,
);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.06;
controls.maxPolarAngle = Math.PI * 0.49;
controls.minDistance = 5;
controls.maxDistance = 100;

const world = buildWorld(scene, PROFILE);
const post = createPost(
  renderer,
  scene,
  camera,
  PROFILE,
  world.outlineTargets,
);

function render() {
  controls.update();
  post.composer.render();
}

function setShot(index) {
  const shot = applyShot(camera, controls, index);
  document.querySelector('#shot').textContent = shot.name;
  render();
}

function resize() {
  const width = innerWidth;
  const height = innerHeight;
  renderer.setSize(width, height, false);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
  post.resize(width, height);
}
addEventListener('resize', resize);
resize();
setShot(0);

installCaptureApi({
  profile: PROFILE, world, post, camera, controls, applyShot, render,
});

let modeIndex = 2;
const modeNames = ['structure', 'clean', 'final'];
addEventListener('keydown', (event) => {
  const index = Number(event.key) - 1;
  if (index >= 0 && index < SHOTS.length) setShot(index);
  if (event.key.toLowerCase() === 'h') document.body.classList.toggle('hide-hud');
  if (event.key.toLowerCase() === 'm') {
    modeIndex = (modeIndex + 1) % modeNames.length;
    window.__graphicCityLab.setMode(modeNames[modeIndex]);
  }
});

const captureMode = new URLSearchParams(location.search).has('capture');
if (captureMode) {
  render();
} else {
  function animate() {
    requestAnimationFrame(animate);
    render();
  }
  animate();
}
