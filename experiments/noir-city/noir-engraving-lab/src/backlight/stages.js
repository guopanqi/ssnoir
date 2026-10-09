// 逆光 BACKLIGHT · 阶段开关
//
// 四个阶段，逐层叠加，对应"一次只回答一个问题"的实验纪律：
//   1 体块 shape  天 + 低角度键光 + 硬分带 + 真实阴影（没有雾、没有一盏灯）
//   2 空气 air    + 空气透视 + god rays（逆光的价值结构在这里成立）
//   3 灯   light  + 窗光 / 路面暖 / 仰射灯 / 光锥 / 钠灯珠 / 信标 / 色标 / bloom
//   4 成片 final  + 印刷层（split tone、暗角、颗粒、色散）
//
// 只改 uniform 与 pass 参数，不重建任何对象；capture 因此是可复现的。
import * as THREE from 'three';
import { POST, FOG, SURFACE } from './config.js';

const ZERO = new THREE.Vector3();

export const STAGE_ORDER = ['shape', 'air', 'light', 'final'];
export const STAGE_LABEL = { shape: '1 体块', air: '2 空气', light: '3 灯', final: '4 成片' };

const FLAGS = {
  shape: { fog: 0, windows: 0, lights: 0, emissive: 0, rays: 0, bloom: 0, grade: 0 },
  air: { fog: 1, windows: 0, lights: 0, emissive: 0, rays: 1, bloom: 0, grade: 0 },
  light: { fog: 1, windows: 1, lights: 1, emissive: 1, rays: 1, bloom: 1, grade: 0 },
  final: { fog: 1, windows: 1, lights: 1, emissive: 1, rays: 1, bloom: 1, grade: 1 },
};

/**
 * @param {object} rig
 *   shared      共享 uniform（uFogDensity）
 *   fogBase     雾的基准密度
 *   windows     窗光材质
 *   glow        带 uIntensity 的辉光材质（钠灯 / 信标 / 色标 / 光锥）
 *   spots       SpotLight（用 userData.baseIntensity 记基准）
 *   emissive    带 uEmissive / uEmissiveGain 的表面材质
 *   post        createPost 的返回值
 */
export function applyStage(stage, rig) {
  const f = FLAGS[stage];
  if (!f) throw new Error('未知阶段: ' + stage);

  rig.shared.uFogDensity.value = f.fog ? rig.fogBase : 0;
  rig.windows.uniforms.uOn.value = f.windows;

  for (const m of rig.glow) m.uniforms.uIntensity.value = f.lights ? m.userData.baseIntensity : 0;
  for (const s of rig.spots) s.intensity = f.lights ? s.userData.baseIntensity : 0;

  for (const m of rig.emissive) {
    const u = m.userData;
    if (u.baseEmissive) u.uniforms.uEmissive.value.copy(f.emissive ? u.baseEmissive : ZERO);
    if (u.uniforms.uEmissiveGain) u.uniforms.uEmissiveGain.value = f.emissive ? u.baseGain : 0;
  }

  rig.post.rays.strength = f.rays ? POST.rays.strength : 0;
  rig.post.bloom.strength = f.bloom ? POST.bloom.strength : 0;

  const g = rig.post.grade.uniforms;
  g.uVignette.value = f.grade ? POST.grade.vignette : 0;
  g.uGrain.value = f.grade ? POST.grade.grain : 0;
  g.uAberration.value = f.grade ? POST.grade.aberration : 0;
  g.uDesat.value = f.grade ? POST.grade.desat : 0;
  g.uBlackPoint.value = f.grade ? POST.grade.blackPoint : 0;
  g.uShadowTint.value.set(...(f.grade ? POST.grade.shadowTint : [1, 1, 1]));
  g.uHighTint.value.set(...(f.grade ? POST.grade.highTint : [1, 1, 1]));

  return f;
}

export { FOG, SURFACE };
