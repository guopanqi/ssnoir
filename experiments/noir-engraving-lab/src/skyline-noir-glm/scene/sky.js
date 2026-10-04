import * as THREE from 'three';

const VERT = /* glsl */ `
varying vec3 vDir;
void main() {
  vDir = normalize(position);
  vec4 mv = modelViewMatrix * vec4(position, 1.0);
  gl_Position = projectionMatrix * mv;
  gl_Position.z = gl_Position.w; // 推到远平面，永远在最后面
}
`;

const FRAG = /* glsl */ `
varying vec3 vDir;
uniform vec3 uTop;
uniform vec3 uMid;
uniform vec3 uHorizon;
uniform vec3 uSmog;
uniform float uSmogHeight;
uniform vec3 uMoonDir;
uniform vec3 uMoonColor;
uniform float uMoonCos;
uniform float uMoonHaloExp;
uniform float uMoonHaloStrength;
uniform float uStarDensity;

float hash21(vec2 p) {
  p = fract(p * vec2(233.34, 851.73));
  p += dot(p, p + 23.45);
  return fract(p.x * p.y);
}

void main() {
  vec3 d = normalize(vDir);
  float h = clamp(d.y, -1.0, 1.0);

  // 三段渐变：天顶近黑 -> 板岩蓝 -> 地平线被城市呼吸染亮的底色
  vec3 col = mix(uMid, uTop, smoothstep(0.08, 0.65, h));
  col = mix(uHorizon, col, smoothstep(0.015, 0.22, h));

  // 暖雾带：贴着地平线的一条暖带，是"城市本身在发光"的暗示；塔顶剪影切在它上面
  float band = exp(-pow(max(h, 0.0) / uSmogHeight, 2.0));
  col += uSmog * band * 1.0;

  // 星：只在高处，极稀疏
  vec2 cell = floor(d.xz / (0.018 + 0.004 * abs(d.y)) * 12.0);
  float star = step(1.0 - uStarDensity * 0.05, hash21(cell)) * smoothstep(0.12, 0.4, h);
  col += vec3(0.55, 0.62, 0.7) * star * 0.5;

  // 月：很小的盘 + 收得很紧的晕。晕一松整片天就亮了。
  float m = dot(d, normalize(uMoonDir));
  float disc = smoothstep(uMoonCos, uMoonCos + 0.00035, m);
  float halo = pow(clamp(m, 0.0, 1.0), uMoonHaloExp);
  col += uMoonColor * disc * 1.15;
  col += uMoonColor * halo * uMoonHaloStrength;

  // IGN 抖动：8bit 渐变不出色带
  float g = fract(52.9829189 * fract(0.06711056 * gl_FragCoord.x + 0.00583715 * gl_FragCoord.y));
  col += (g - 0.5) * (1.6 / 255.0);

  gl_FragColor = vec4(col, 1.0);
}
`;

export function createSky(profile) {
  const s = profile.sky;
  const uniforms = {
    uTop: { value: new THREE.Color(s.topColor) },
    uMid: { value: new THREE.Color(s.midColor) },
    uHorizon: { value: new THREE.Color(s.horizonColor) },
    uSmog: { value: new THREE.Color(s.smogBandColor) },
    uSmogHeight: { value: s.smogBandHeight },
    uMoonDir: { value: new THREE.Vector3(...s.moonDir).normalize() },
    uMoonColor: { value: new THREE.Color(s.moonColor) },
    uMoonCos: { value: Math.cos((s.moonAngularRadius * Math.PI) / 180) },
    uMoonHaloExp: { value: s.moonHaloExponent },
    uMoonHaloStrength: { value: s.moonHaloStrength },
    uStarDensity: { value: s.starDensity },
  };
  const mesh = new THREE.Mesh(
    new THREE.SphereGeometry(1400, 32, 20),
    new THREE.ShaderMaterial({
      vertexShader: VERT,
      fragmentShader: FRAG,
      uniforms,
      side: THREE.BackSide,
      depthWrite: false,
      fog: false,
    }),
  );
  mesh.frustumCulled = false;
  mesh.renderOrder = -10;
  return { mesh, uniforms };
}
