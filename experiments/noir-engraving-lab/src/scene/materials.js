import * as THREE from 'three';

export function createMaterials(profile) {
  const p = profile.palette;
  const flat = (color, extra = {}) => new THREE.MeshStandardMaterial({
    color,
    roughness: 1,
    metalness: 0,
    flatShading: true,
    ...extra,
  });

  return {
    ground: flat(p.asphalt),
    sidewalk: flat(p.sidewalk),
    building: flat(p.building),
    buildingDim: flat(p.buildingDim),
    landmark: flat(p.landmark),

    wetRoad: new THREE.MeshPhysicalMaterial({
      color: 0x070b11,
      roughness: 0.22,
      metalness: 0.2,
      clearcoat: 0.55,
      clearcoatRoughness: 0.18,
    }),

    edge: new THREE.LineBasicMaterial({
      color: p.edge,
      transparent: true,
      opacity: 0.72,
    }),

    edgeDim: new THREE.LineBasicMaterial({
      color: p.edgeDim,
      transparent: true,
      opacity: 0.36,
    }),

    warm: flat(0x171615, {
      emissive: p.warm,
      emissiveIntensity: 5.0,
    }),

    cool: flat(0x11161d, {
      emissive: p.cool,
      emissiveIntensity: 3.6,
    }),

    red: flat(0x160d0d, {
      emissive: p.red,
      emissiveIntensity: 2.3,
    }),
  };
}
