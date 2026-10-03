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

  const positions = new Float32Array(cfg.rainCount * 3);
  const speeds = new Float32Array(cfg.rainCount);

  for (let i = 0; i < cfg.rainCount; i++) {
    positions[i * 3] = (random() - 0.5) * 125;
    positions[i * 3 + 1] = 3 + random() * 34;
    positions[i * 3 + 2] = (random() - 0.5) * 75;
    speeds[i] = 12 + random() * 16;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

  const rain = new THREE.Points(
    geometry,
    new THREE.PointsMaterial({
      color: 0xb9cce0,
      size: cfg.rainSize,
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
      p[i * 3 + 1] -= speeds[i] * dt;
      p[i * 3] -= 2.8 * dt;

      if (p[i * 3 + 1] < 0.15) {
        p[i * 3 + 1] = 25 + random() * 15;
        p[i * 3] = (random() - 0.5) * 125;
        p[i * 3 + 2] = (random() - 0.5) * 75;
      }
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
    get enabled() {
      return enabled;
    },
  };
}
