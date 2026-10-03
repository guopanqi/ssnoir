import * as THREE from 'three';
import { seededRandom } from '../scene/primitives.js';

export function createAtmosphere(scene, profile) {
  const cfg = profile.atmosphere;
  const random = seededRandom(profile.seed);

  scene.background = new THREE.Color(profile.palette.void);
  scene.fog = new THREE.FogExp2(profile.palette.void, cfg.fogDensity);

  const group = new THREE.Group();
  group.name = 'Atmosphere';
  scene.add(group);

  const positions = new Float32Array(cfg.rainCount * 6);
  const speeds = new Float32Array(cfg.rainCount);

  function resetDrop(i, initial = false) {
    const x = (random() - 0.5) * 125;
    const y = initial ? 2 + random() * 35 : 27 + random() * 12;
    const z = (random() - 0.5) * 75;
    const length = cfg.rainLength * (0.55 + random() * 0.75);
    const base = i * 6;

    positions[base] = x;
    positions[base + 1] = y;
    positions[base + 2] = z;
    positions[base + 3] = x - length * 0.20;
    positions[base + 4] = y - length;
    positions[base + 5] = z + length * 0.05;

    speeds[i] = 12 + random() * 16;
  }

  for (let i = 0; i < cfg.rainCount; i++) resetDrop(i, true);

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

  const rain = new THREE.LineSegments(
    geometry,
    new THREE.LineBasicMaterial({
      color: 0xaabbd0,
      transparent: true,
      opacity: cfg.rainOpacity,
      depthWrite: false,
    }),
  );
  group.add(rain);

  let enabled = true;

  function update(dt) {
    if (!enabled) return;

    const p = geometry.attributes.position.array;
    for (let i = 0; i < cfg.rainCount; i++) {
      const base = i * 6;
      const fall = speeds[i] * dt;
      const drift = fall * 0.20;

      p[base] -= drift;
      p[base + 1] -= fall;
      p[base + 3] -= drift;
      p[base + 4] -= fall;

      if (p[base + 4] < 0.15) resetDrop(i, false);
    }

    geometry.attributes.position.needsUpdate = true;
  }

  return {
    group,
    update,
    setEnabled(value) {
      enabled = value;
      group.visible = value;
    },
    setFogDensity(value) {
      scene.fog.density = value;
    },
    get fogDensity() {
      return scene.fog.density;
    },
    get enabled() {
      return enabled;
    },
  };
}

