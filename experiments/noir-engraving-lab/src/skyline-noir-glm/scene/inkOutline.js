import * as THREE from 'three';

// 墨线壳（inverted hull）：只给地标。概念与顶点数学来自 three.js OutlineEffect
// （examples/jsm/effects/OutlineEffect.js）：
//   pos2 = projectionMatrix * modelViewMatrix * vec4(position + normal, 1.0)
//   pos + normalize(pos - pos2) * thickness * pos.w * ratio
// 其中 * pos.w 抵消透视除法，得到屏幕空间恒定线宽。
// 与 OutlineEffect 的差异：这里是独立 BackSide 材质（不为全场景包一层渲染器），
// 并手动做 FogExp2 混合，让墨线与场景空气一致。

const VERT = /* glsl */ `
uniform float uThickness;
uniform float uRatio;
uniform float uFogDensity;
uniform vec3 uFogColor;
varying vec3 vColor;
void main() {
  vec4 pos = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  vec4 pos2 = projectionMatrix * modelViewMatrix * vec4(position + normal, 1.0);
  vec4 norm = normalize(pos - pos2);
  vec4 result = pos + norm * uThickness * pos.w * uRatio;
  float depth = -(modelViewMatrix * vec4(position, 1.0)).z;
  float fogFactor = 1.0 - exp(-uFogDensity * uFogDensity * depth * depth);
  vColor = mix(vec3(1.0), uFogColor, clamp(fogFactor, 0.0, 1.0));
  gl_Position = result;
}
`;

const FRAG = /* glsl */ `
uniform vec3 uInk;
varying vec3 vColor;
void main() {
  gl_FragColor = vec4(uInk * vColor, 1.0);
}
`;

export function createInkMaterial(profile, thickness) {
  return new THREE.ShaderMaterial({
    vertexShader: VERT,
    fragmentShader: FRAG,
    uniforms: {
      uThickness: { value: thickness },
      uRatio: { value: 1.0 },
      uInk: { value: new THREE.Color(profile.ink.color) },
      uFogDensity: { value: profile.fog.density },
      uFogColor: { value: new THREE.Color(profile.fog.color) },
    },
    side: THREE.BackSide,
  });
}

// 为地标 mesh 套一层壳；返回的 mesh 加入场景，与本体同变换（这里本体都在原点，无需同步）
export function addInkShell(mesh, profile, thickness) {
  const shell = new THREE.Mesh(mesh.geometry, createInkMaterial(profile, thickness));
  shell.position.copy(mesh.position);
  shell.rotation.copy(mesh.rotation);
  shell.scale.copy(mesh.scale);
  shell.renderOrder = mesh.renderOrder;
  shell.name = `${mesh.name}-ink`;
  return shell;
}
