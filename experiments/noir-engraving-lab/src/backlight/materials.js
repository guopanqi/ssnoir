// 逆光 BACKLIGHT · 材质系统
//
// 一个材质家族，三件事：
//   1. **硬终止线的卡通分带**：替换 gradientmap，三档（0.06 / 0.36 / 1.0），fwidth 抗锯齿；
//   2. **空气透视**：替换 fog 两块，把远处几何精确推向"它背后那块天"（skyBase 同源）；
//   3. **role 语义**：地面/外壳按烘焙的表面角色给明度，暖白/冷白发光表面直接当自发光。
// 没有屏幕描线 —— 轮廓由逆光的明暗关系本身给出。
import * as THREE from 'three';
import { SKY, FOG, SURFACE, DISTRICT_TINT, WINDOWS, LAMPS, HERO, BANDS, BAND_FLOOR } from './config.js';
import { FOG_GLSL, SKY_BASE_GLSL, HASH_GLSL } from './glsl.js';

const V = (a) => new THREE.Vector3(a[0], a[1], a[2]);

/** 天空 / 雾的共享 uniform：一个对象，所有材质引用同一份。 */
export const shared = {
  uSkyZenith: { value: V(SKY.zenith) },
  uSkyMid: { value: V(SKY.mid) },
  uSkyHorizon: { value: V(SKY.horizon) },
  uGlowDir: { value: V(SKY.glowDir).normalize() },
  uFogDensity: { value: FOG.density },
  uFogYFall: { value: FOG.yFall },
  uFogHighMul: { value: FOG.highMul },
  uTime: { value: 0 },
};

const BAND_GLSL = /* glsl */ `
// 硬终止线三档；w = fwidth 抗锯齿宽度，近处硬、远处不闪。
vec3 getGradientIrradiance( vec3 normal, vec3 lightDirection ) {
  float dotNL = dot( normal, lightDirection );
  float w = fwidth( dotNL ) * 1.4 + 1e-4;
  float b0 = smoothstep( ${BANDS[0].toFixed(3)} - w, ${BANDS[0].toFixed(3)} + w, dotNL );
  float b1 = smoothstep( ${BANDS[1].toFixed(3)} - w, ${BANDS[1].toFixed(3)} + w, dotNL );
  float g = mix( ${BAND_FLOOR[0].toFixed(3)}, ${BAND_FLOOR[1].toFixed(3)}, b0 );
  g = mix( g, ${BAND_FLOOR[2].toFixed(3)}, b1 );
  return vec3( g );
}
`;

const FOG_PARS = /* glsl */ `
varying vec3 vWorldPos;
${FOG_GLSL}
`;

const FOG_APPLY = /* glsl */ `
gl_FragColor.rgb = aerial( gl_FragColor.rgb, vWorldPos );
`;

const FOGLINE_VERTEX = /* glsl */ `
#include <fog_vertex>
{
  vec4 _wp = vec4( transformed, 1.0 );
  #ifdef USE_INSTANCING
    _wp = instanceMatrix * _wp;
  #endif
  vWorldPos = ( modelMatrix * _wp ).xyz;
}
`;

/**
 * 城市表面材质（MeshToon + 分带 + 空气透视）。
 * @param {object} o
 *   color     反照率（linear）
 *   emissive  恒定自发光（如路面的一口暖）
 *   useRole   读烘焙的 aRole：明度档 + 暖白/冷白发光表面
 */
export function toonSurface({ color, emissive = [0, 0, 0], useRole = false, name = 'surface' }) {
  const mat = new THREE.MeshToonMaterial({
    color: new THREE.Color().setRGB(color[0], color[1], color[2]),
    dithering: false,
  });
  mat.name = name;
  mat.userData.uniforms = {
    ...shared,
    uEmissive: { value: V(emissive) },
    uEmissiveWarm: { value: V(SURFACE.emissiveWarm) },
    uEmissiveCool: { value: V(SURFACE.emissiveCool) },
    uEmissiveGain: { value: SURFACE.emissiveGain },
    uRoleMul: { value: SURFACE.roleMul.slice() },
  };
  // 阶段开关要的基准值
  mat.userData.baseEmissive = V(emissive);
  mat.userData.baseGain = SURFACE.emissiveGain;
  mat.onBeforeCompile = (shader) => {
    Object.assign(shader.uniforms, mat.userData.uniforms);

    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', `#include <common>
${useRole ? 'attribute float aRole; varying float vRole;' : ''}
varying vec3 vWorldPos;`)
      .replace('#include <fog_vertex>', FOGLINE_VERTEX)
      .replace('#include <begin_vertex>', `#include <begin_vertex>
${useRole ? 'vRole = aRole;' : ''}`);

    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', `#include <common>
${useRole ? `varying float vRole;
uniform float uRoleMul[10];
uniform vec3 uEmissiveWarm;
uniform vec3 uEmissiveCool;
uniform float uEmissiveGain;` : ''}
uniform vec3 uEmissive;`)
      .replace('#include <gradientmap_pars_fragment>', BAND_GLSL)
      .replace('#include <fog_pars_fragment>', FOG_PARS)
      .replace('#include <color_fragment>', `#include <color_fragment>
${useRole ? 'diffuseColor.rgb *= uRoleMul[ int( vRole + 0.5 ) ];' : ''}`)
      .replace('#include <emissivemap_fragment>', `#include <emissivemap_fragment>
totalEmissiveRadiance += uEmissive;
${useRole ? `
{
  int ri = int( vRole + 0.5 );
  if ( ri == 7 ) totalEmissiveRadiance += uEmissiveWarm * uEmissiveGain;
  else if ( ri == 8 ) totalEmissiveRadiance += uEmissiveCool * uEmissiveGain;
}` : ''}`)
      .replace('#include <fog_fragment>', FOG_APPLY);
  };
  mat.customProgramCacheKey = () => 'backlight-surface-' + (useRole ? 'role' : 'plain');
  return mat;
}

/** 窗光：不参与照明，按 tier / 街区簇 / 随机决定点不点灯。 */
export function windowMaterial() {
  const mat = new THREE.ShaderMaterial({
    name: '窗光',
    uniforms: {
      ...shared,
      uTierWhite: { value: V(WINDOWS.tierColor.white) },
      uTierGrey: { value: V(WINDOWS.tierColor.grey) },
      uTierBlue: { value: V(WINDOWS.tierColor.blue) },
      uIntensity: { value: WINDOWS.intensity },
      uOn: { value: 1.0 },
      uBlockDark: { value: WINDOWS.blockDark },
      uOffRate: { value: WINDOWS.offRate },
      uFlicker: { value: WINDOWS.flicker },
    },
    vertexShader: /* glsl */ `
      attribute float aTier;
      attribute float aRand;
      attribute float aBlock;
      varying float vTier;
      varying float vRand;
      varying float vBlock;
      varying vec3 vWorldPos;
      void main() {
        vTier = aTier;
        vRand = aRand;
        vBlock = aBlock;
        vec4 wp = vec4( position, 1.0 );
        #ifdef USE_INSTANCING
          wp = instanceMatrix * wp;
        #endif
        vWorldPos = ( modelMatrix * wp ).xyz;
        gl_Position = projectionMatrix * modelViewMatrix * wp;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      uniform vec3 uTierWhite;
      uniform vec3 uTierGrey;
      uniform vec3 uTierBlue;
      uniform float uIntensity;
      uniform float uOn;
      uniform float uBlockDark;
      uniform float uOffRate;
      uniform float uFlicker;
      uniform float uTime;
      varying float vTier;
      varying float vRand;
      varying float vBlock;
      varying vec3 vWorldPos;
      ${FOG_GLSL}
      void main() {
        if ( uOn < 0.5 ) discard;
        if ( vBlock < uBlockDark ) discard;
        if ( vRand < uOffRate ) discard;
        vec3 c = uTierWhite;
        if ( vTier > 1.5 ) c = uTierBlue;
        else if ( vTier > 0.5 ) c = uTierGrey;
        // 少数窗明灭（可被暂停冻住，capture 是确定的）
        float fl = 1.0;
        if ( vRand > 0.93 ) fl = 1.0 - uFlicker * ( 0.5 + 0.5 * sin( uTime * 2.3 + vRand * 91.0 ) );
        vec3 col = c * uIntensity * fl;
        gl_FragColor = vec4( aerial( col, vWorldPos ), 1.0 );
      }
    `,
  });
  return mat;
}

/** 河面：黑水之上只留天空的反射 + 一条向热点走的碎光。 */
export function waterMaterial() {
  const mat = new THREE.ShaderMaterial({
    name: '河面',
    uniforms: {
      ...shared,
      uBase: { value: V(SURFACE.water) },
      uRefl: { value: SURFACE.waterRefl },
    },
    vertexShader: /* glsl */ `
      varying vec3 vWorldPos;
      void main() {
        vec4 wp = vec4( position, 1.0 );
        #ifdef USE_INSTANCING
          wp = instanceMatrix * wp;
        #endif
        vWorldPos = ( modelMatrix * wp ).xyz;
        gl_Position = projectionMatrix * modelViewMatrix * wp;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      uniform vec3 uBase;
      uniform float uRefl;
      uniform float uTime;
      varying vec3 vWorldPos;
      ${FOG_GLSL}
      void main() {
        // 细浪法线：两组交叉长波 + 一组短波（时间由外部冻住时完全确定）
        vec3 wp = vWorldPos;
        float dist = length( cameraPosition - wp );
        float t = uTime * 0.35;
        // 细浪：三组交叉波，幅度随距离衰减（远处别再起摩尔纹）
        float lod = exp( -dist * 0.018 );
        vec2 d1 = vec2( 0.94, 0.34 ), d2 = vec2( -0.42, 0.91 ), d3 = vec2( 0.71, -0.70 );
        float a = sin( dot( wp.xz, d1 ) * 1.30 + t );
        float b = sin( dot( wp.xz, d2 ) * 1.85 - t * 1.3 );
        float c = sin( dot( wp.xz, d3 ) * 2.60 + t * 2.1 );
        float amp = 0.016 * lod;
        vec3 n = normalize( vec3( a * amp + c * amp * 0.5, 1.0, b * amp + c * amp * 0.4 ) );
        vec3 V = normalize( cameraPosition - wp );
        vec3 R = reflect( -V, n );
        R.y = abs( R.y );
        vec3 sky = skyBase( R );
        float fres = 0.10 + 0.90 * pow( 1.0 - max( dot( n, V ), 0.0 ), 5.0 );
        vec3 col = uBase + sky * fres * uRefl;
        // 向热点的碎光带
        float spec = pow( max( dot( R, uGlowDir ), 0.0 ), 40.0 );
        col += uSkyHorizon * spec * 2.0;
        gl_FragColor = vec4( aerial( col, vWorldPos ), 1.0 );
      }
    `,
  });
  return mat;
}

/** 天穹：地平线发光带 + 热点扇区 + 稀疏星点。相机跟随，永远在 6 km 外。 */
export function skyMaterial() {
  const mat = new THREE.ShaderMaterial({
    name: '天穹',
    side: THREE.BackSide,
    depthWrite: false,
    uniforms: {
      ...shared,
      uStarGain: { value: SKY.starGain },
      uBelowHaze: { value: V(SKY.belowHaze) },
    },
    vertexShader: /* glsl */ `
      varying vec3 vWorldPos;
      void main() {
        vec4 wp = vec4( position, 1.0 );
        #ifdef USE_INSTANCING
          wp = instanceMatrix * wp;
        #endif
        vWorldPos = ( modelMatrix * wp ).xyz;
        gl_Position = projectionMatrix * modelViewMatrix * wp;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      uniform float uStarGain;
      uniform vec3 uBelowHaze;
      varying vec3 vWorldPos;
      ${SKY_BASE_GLSL}
      float hash21( vec2 p ) {
        p = fract( p * vec2( 233.34, 851.73 ) );
        p += dot( p, p + 23.45 );
        return fract( p.x * p.y );
      }
      void main() {
        vec3 d = normalize( vWorldPos - cameraPosition );
        vec3 col = skyBase( d );
        if ( d.y < 0.0 ) col = mix( col, uBelowHaze, clamp( -d.y * 8.0, 0.0, 1.0 ) );
        // 星：3D 网格撒在天球上（方位角均匀），只在高处，稀疏且小
        if ( uStarGain > 0.001 && d.y > 0.06 ) {
          vec3 gp = d * 78.0;
          vec3 cell = floor( gp );
          vec3 f = fract( gp ) - 0.5;
          vec3 r = fract( sin( cell * vec3( 127.1, 311.7, 74.7 ) ) * 43758.5453 );
          float h = dot( r, vec3( 0.299, 0.587, 0.114 ) );
          if ( h > 0.972 ) {
            float star = smoothstep( 0.22, 0.0, length( f ) );
            col += vec3( 0.62, 0.70, 0.95 ) * star * uStarGain * ( h - 0.972 ) * 34.0
                 * smoothstep( 0.06, 0.40, d.y );
          }
        }
        gl_FragColor = vec4( col, 1.0 );
      }
    `,
  });
  return mat;
}

/** 可见光锥（仰射灯 / 探照灯）：边缘衰减 + 轴向衰减 + 慢波纹，加法混合。 */
export function beamMaterial({ color, intensity, fade }) {
  const mat = new THREE.ShaderMaterial({
    name: '光锥',
    transparent: true,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
    uniforms: {
      ...shared,
      uColor: { value: V(color) },
      uIntensity: { value: intensity },
      uFade: { value: fade },
    },
    userData: { baseIntensity: intensity },
    vertexShader: /* glsl */ `
      varying vec3 vP;
      varying vec3 vN;
      varying float vT;
      void main() {
        vec4 wp = vec4( position, 1.0 );
        vP = ( modelMatrix * wp ).xyz;
        vN = normalize( mat3( modelMatrix ) * normal );
        vT = clamp( uv.y, 0.0, 1.0 );
        gl_Position = projectionMatrix * viewMatrix * wp;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      uniform vec3 uColor;
      uniform float uIntensity;
      uniform float uFade;
      uniform float uTime;
      varying vec3 vP;
      varying vec3 vN;
      varying float vT;
      void main() {
        vec3 V = normalize( cameraPosition - vP );
        float facing = abs( dot( normalize( vN ), V ) );
        float a = pow( facing, uFade ) * pow( 1.0 - vT, 1.4 ) * uIntensity;
        a *= 0.82 + 0.18 * sin( vT * 26.0 - uTime * 0.55 );
        gl_FragColor = vec4( uColor, a );
      }
    `,
  });
  return mat;
}

/**
 * 点光源辉光（钠灯珠 / 塔顶信标 / 三处叙事色标）：一个 Points，一次 draw。
 * size 按世界尺寸换算像素，随距离被雾吃掉。
 */
export function glowPointsMaterial({ color, intensity, worldSize, fogFade = 1.0 }) {
  const mat = new THREE.ShaderMaterial({
    name: '辉光点',
    transparent: true,
    depthWrite: false,
    depthTest: true,
    blending: THREE.AdditiveBlending,
    uniforms: {
      ...shared,
      uColor: { value: V(color) },
      uIntensity: { value: intensity },
      uWorldSize: { value: worldSize },
      uSizeScale: { value: 700 },
      uFogFade: { value: fogFade },
    },
    userData: { baseIntensity: intensity },
    vertexShader: /* glsl */ `
      attribute float aI;
      attribute float aRand;
      uniform float uWorldSize;
      uniform float uSizeScale;
      uniform float uFogDensity;
      uniform float uFogFade;
      varying float vI;
      varying float vFade;
      void main() {
        vI = aI;
        vec4 wp = vec4( position, 1.0 );
        #ifdef USE_INSTANCING
          wp = instanceMatrix * wp;
        #endif
        vec4 mv = viewMatrix * wp;
        float dist = length( wp.xyz - cameraPosition );
        vFade = exp( -dist * uFogDensity * uFogFade );
        gl_PointSize = clamp( uWorldSize * ( 0.75 + 0.5 * aRand ) * uSizeScale / max( 1.0, -mv.z ), 1.0, 512.0 );
        gl_Position = projectionMatrix * mv;
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      uniform vec3 uColor;
      uniform float uIntensity;
      varying float vI;
      varying float vFade;
      void main() {
        vec2 q = gl_PointCoord - 0.5;
        float d = length( q ) * 2.0;
        if ( d > 1.0 ) discard;
        float core = exp( -d * d * 5.5 );
        float halo = exp( -d * d * 1.6 ) * 0.55;
        float a = ( core + halo ) * vI * vFade;
        gl_FragColor = vec4( uColor * uIntensity, a );
      }
    `,
  });
  return mat;
}

/** 打点（钠灯/信标/色标共用的小工具）。 */
export function makePoints(positions, intensity, rand, material) {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  g.setAttribute('aI', new THREE.Float32BufferAttribute(intensity, 1));
  g.setAttribute('aRand', new THREE.Float32BufferAttribute(rand, 1));
  const p = new THREE.Points(g, material);
  p.frustumCulled = false;
  return p;
}

export { DISTRICT_TINT, LAMPS, HERO, HASH_GLSL };
