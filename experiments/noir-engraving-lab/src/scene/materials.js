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
      color: 0x060a10,
      roughness: 0.34,
      metalness: 0.10,
      clearcoat: 0.34,
      clearcoatRoughness: 0.28,
    }),

    edge: new THREE.LineBasicMaterial({
      color: p.edge,
      transparent: true,
      opacity: 0.58,
    }),

    edgeDim: new THREE.LineBasicMaterial({
      color: p.edgeDim,
      transparent: true,
      opacity: 0.16,
    }),

    warm: flat(0x151412, {
      emissive: p.warm,
      emissiveIntensity: 2.4,
    }),

    cool: flat(0x10151c, {
      emissive: p.cool,
      emissiveIntensity: 1.5,
    }),

    red: flat(0x140c0c, {
      emissive: p.red,
      emissiveIntensity: 1.25,
    }),
  };
}
