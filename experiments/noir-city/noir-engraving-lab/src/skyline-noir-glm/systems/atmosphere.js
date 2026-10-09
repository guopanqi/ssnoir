import * as THREE from 'three';
import { getGlowTexture } from '../scene/glowTexture.js';

// 空气层：指数雾 + 探照灯 + 烟柱。
// 雾不用 raymarch（那是隔壁黑水的路线）——海报语言里，雾只是"把远处压进天色"的渐变。
// 探照灯是全城唯一的活动光：加性光锥 + Fresnel 边缘衰减，
// 手法来自 Lee Stemkoski 的经典 glow cone（pow(c + dot(V,N), p) + AdditiveBlending）。

const BEACON_VERT_T = (length) => /* glsl */ `
varying vec3 vNormalW;
varying vec3 vWorldPos;
varying float vT;
void main() {
  vT = clamp(position.y / ${length.toFixed(1)}, 0.0, 1.0);
  vec4 wp = modelMatrix * vec4(position, 1.0);
  vWorldPos = wp.xyz;
  vNormalW = normalize(mat3(modelMatrix) * normal);
  gl_Position = projectionMatrix * viewMatrix * wp;
}
`;

const BEACON_FRAG = /* glsl */ `
uniform vec3 uColor;
uniform float uIntensity;
varying vec3 vNormalW;
varying vec3 vWorldPos;
varying float vT;
void main() {
  vec3 V = normalize(cameraPosition - vWorldPos);
  // 光锥：中心最亮，边缘 Fresnel 衰减；沿长度往远处淡出
  float core = pow(abs(dot(V, normalize(vNormalW))), 1.6);
  float lenFade = pow(1.0 - vT, 1.7);
  float a = core * lenFade * uIntensity;
  gl_FragColor = vec4(uColor * a, a);
}
`;

const SMOKE_FRAG = /* glsl */ `
uniform float uTime;
uniform float uOpacity;
uniform vec3 uColor;
varying vec2 vUv;
float hash(vec2 p) {
  p = fract(p * vec2(123.34, 456.21));
  p += dot(p, p + 45.32);
  return fract(p.x * p.y);
}
float vnoise(vec2 p) {
  vec2 i = floor(p), f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  float a = hash(i), b = hash(i + vec2(1, 0)), c = hash(i + vec2(0, 1)), d = hash(i + vec2(1, 1));
  return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}
float fbm(vec2 p) {
  float v = 0.0, amp = 0.55;
  for (int i = 0; i < 3; i++) { v += vnoise(p) * amp; p *= 2.1; amp *= 0.5; }
  return v;
}
void main() {
  vec2 q = vec2(vUv.x * 2.2, vUv.y * 1.1 - uTime * 0.055);
  float n = fbm(q + fbm(q + uTime * 0.02) * 1.4);
  float edge = smoothstep(0.0, 0.35, vUv.x) * smoothstep(1.0, 0.65, vUv.x);
  float vert = smoothstep(0.02, 0.3, vUv.y) * (1.0 - smoothstep(0.3, 0.95, vUv.y));
  float a = n * edge * vert * uOpacity;
  gl_FragColor = vec4(uColor, a);
}
`;

const SMOKE_VERT = /* glsl */ `
varying vec2 vUv;
void main() {
  vUv = uv;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`;

export function createAtmosphere(scene, profile, landmarks) {
  // 雾
  const fog = new THREE.FogExp2(profile.fog.color, profile.fog.density);
  scene.fog = fog;

  // 探照灯
  const b = profile.beacon;
  const coneGeo = new THREE.ConeGeometry(b.radius, b.length, 20, 1, true);
  coneGeo.rotateX(Math.PI); // 尖端朝下
  coneGeo.translate(0, b.length / 2, 0); // 尖端移到原点，锥体向上张开
  const beaconMat = new THREE.ShaderMaterial({
    vertexShader: BEACON_VERT_T(b.length),
    fragmentShader: BEACON_FRAG,
    uniforms: {
      uColor: { value: new THREE.Color(b.color) },
      uIntensity: { value: b.intensity * 0.5 },
    },
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.DoubleSide,
  });
  const beacon = new THREE.Mesh(coneGeo, beaconMat);
  beacon.renderOrder = 5;
  beacon.name = 'beacon';
  const beaconAnchor = landmarks.anchors.beacon;
  beaconAnchor.add(beacon);
  const beaconTip = new THREE.Sprite(new THREE.SpriteMaterial({
    map: getGlowTexture(),
    color: b.color,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    opacity: 0.9,
  }));
  beaconTip.scale.set(9, 9, 1);
  beaconAnchor.add(beaconTip);

  const UP = new THREE.Vector3(0, 1, 0);
  const beamDir = new THREE.Vector3();

  function updateBeacon(t) {
    const az = t * b.speed;
    beamDir.set(Math.sin(az) * Math.cos(b.elevation), Math.sin(b.elevation), Math.cos(az) * Math.cos(b.elevation));
    beacon.quaternion.setFromUnitVectors(UP, beamDir);
  }
  updateBeacon(0);

  // 烟柱
  const smokeGroup = new THREE.Group();
  smokeGroup.name = 'smoke';
  const smokes = [];
  for (const anchor of landmarks.anchors.stacks) {
    const mat = new THREE.ShaderMaterial({
      vertexShader: SMOKE_VERT,
      fragmentShader: SMOKE_FRAG,
      uniforms: {
        uTime: { value: 0 },
        uOpacity: { value: profile.smoke.opacity },
        uColor: { value: new THREE.Color(profile.smoke.color) },
      },
      transparent: true,
      depthWrite: false,
    });
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(34, 90), mat);
    mesh.position.set(anchor.x, anchor.y + 42, anchor.z);
    mesh.renderOrder = 4;
    smokeGroup.add(mesh);
    smokes.push({ mesh, mat, base: new THREE.Vector3(anchor.x, anchor.y + 42, anchor.z) });
  }
  scene.add(smokeGroup);

  function updateSmokes(t, camera) {
    for (const s of smokes) {
      s.mat.uniforms.uTime.value = t;
      s.mesh.rotation.y = Math.atan2(camera.position.x - s.base.x, camera.position.z - s.base.z);
    }
  }

  return {
    fog,
    beacon,
    beaconTip,
    smokeGroup,
    setBeaconVisible(v) {
      beacon.visible = v;
      beaconTip.visible = v;
    },
    setFogEnabled(v) {
      fog.density = v ? profile.fog.density : 0.00001;
    },
    setSmokeVisible(v) {
      smokeGroup.visible = v;
    },
    update(t, camera) {
      updateBeacon(t);
      updateSmokes(t, camera);
    },
  };
}
