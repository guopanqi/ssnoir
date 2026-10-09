import * as THREE from 'three';

// 窗光：城市的神经系统。全部窗合并成一张非索引几何，一次 draw call。
// 亮/灭与色温在生成期由 seed 决定；少量窗做"量化时间"的呼吸闪烁（CPU 改顶点色）。

export function createWindows(specs, profile, rng) {
  const count = specs.length;
  const positions = new Float32Array(count * 6 * 3);
  const colors = new Float32Array(count * 6 * 3);
  const flickers = [];

  // 随机挑一小撮窗加入闪烁序列（用可复现 rng）
  const flickerSet = new Set();
  while (flickerSet.size < Math.min(profile.city.flickerWindowCount, count)) {
    flickerSet.add(Math.floor(rng.next() * count));
  }

  for (let i = 0; i < count; i++) {
    const s = specs[i];
    // 面向法线的四边形：right = up × normal
    const rx = s.nz, rz = -s.nx;
    const hw = s.w / 2, hh = s.h / 2;
    const ox = s.nx * 0.16, oz = s.nz * 0.16;
    const cx = s.x + ox, cy = s.y, cz = s.z + oz;
    const v = [
      [cx - rx * hw, cy - hh, cz - rz * hw],
      [cx + rx * hw, cy - hh, cz + rz * hw],
      [cx + rx * hw, cy + hh, cz + rz * hw],
      [cx - rx * hw, cy - hh, cz - rz * hw],
      [cx + rx * hw, cy + hh, cz + rz * hw],
      [cx - rx * hw, cy + hh, cz - rz * hw],
    ];
    const br = s.bright;
    const col = [s.r * br, s.g * br, s.b * br];
    for (let k = 0; k < 6; k++) {
      positions.set(v[k], (i * 6 + k) * 3);
      colors.set(col, (i * 6 + k) * 3);
    }
    if (flickerSet.has(i)) {
      flickers.push({ vertStart: i * 6, col, phase: (i * 0.618) % 1, speed: 0.12 + ((i * 0.37) % 0.2) });
    }
  }

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const mesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ vertexColors: true }));
  mesh.name = 'windows';
  mesh.frustumCulled = false;

  function applyFlicker(t) {
    if (!flickers.length) return;
    const attr = geo.attributes.color;
    for (const f of flickers) {
      const step = Math.floor(t * f.speed * 2) + f.phase * 97;
      const on = Math.sin(step * 12.9898) * 43758.5453;
      const factor = 0.5 + 0.5 * (on - Math.floor(on) > 0.45 ? 1 : 0.25);
      for (let k = 0; k < 6; k++) {
        attr.array[(f.vertStart + k) * 3] = f.col[0] * factor;
        attr.array[(f.vertStart + k) * 3 + 1] = f.col[1] * factor;
        attr.array[(f.vertStart + k) * 3 + 2] = f.col[2] * factor;
      }
    }
    attr.needsUpdate = true;
  }

  return { mesh, applyFlicker, count };
}
