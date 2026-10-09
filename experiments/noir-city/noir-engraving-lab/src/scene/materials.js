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

    // Do not let generic PBR specular decide the composition. Wet reflection
    // accents will be introduced later as deliberate narrative shapes.
    wetRoad: flat(0x0b121c, {
      roughness: 1.0,
      metalness: 0,
    }),

    alleyFloor: flat(0x121d2b, {
      roughness: 1.0,
      metalness: 0,
    }),

    edge: new THREE.LineBasicMaterial({
      color: p.edge,
      transparent: true,
      opacity: 0.68,
    }),

    edgeDim: new THREE.LineBasicMaterial({
      color: p.edgeDim,
      transparent: true,
      opacity: 0.11,
    }),

    warm: flat(0x151412, {
      emissive: p.warm,
      emissiveIntensity: 1.25,
    }),

    warmSoft: flat(0x11100e, {
      emissive: p.warm,
      emissiveIntensity: 0.48,
    }),

    cool: flat(0x10151c, {
      emissive: p.cool,
      emissiveIntensity: 0.92,
    }),

    red: flat(0x140c0c, {
      emissive: p.red,
      emissiveIntensity: 0.95,
    }),
  };
}
