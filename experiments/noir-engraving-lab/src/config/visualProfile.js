export const PROFILE = Object.freeze({
  seed: 1947,

  palette: Object.freeze({
    void: 0x080b12,
    asphalt: 0x0b1018,
    sidewalk: 0x101722,
    building: 0x121b2a,
    buildingDim: 0x0d1420,
    landmark: 0x1a2638,
    edge: 0xd8e1ef,
    edgeDim: 0x526078,
    warm: 0xf3d08a,
    cool: 0xa7d5ff,
    red: 0xb3402a,
  }),

  camera: Object.freeze({
    fov: 42,
    near: 0.1,
    far: 220,
  }),

  atmosphere: Object.freeze({
    fogDensity: 0.024,
    rainCount: 1900,
    rainOpacity: 0.48,
    rainSize: 0.055,
  }),

  lighting: Object.freeze({
    keyColor: 0xc8d9ff,
    keyIntensity: 1.5,
    keyPosition: [-22, 34, 18],
    fillColor: 0x6d8fc7,
    fillIntensity: 0.42,
    fillPosition: [34, 16, -28],
    ambientSky: 0x26354c,
    ambientGround: 0x05070b,
    ambientIntensity: 0.48,
    streetColor: 0xf3d08a,
    streetIntensity: 16,
    streetDistance: 15,
  }),

  print: Object.freeze({
    levels: 6,
    dither: 0.85,
    ditherScale: 2,
    inBlack: 0.018,
    inWhite: 0.58,
    gamma: 0.92,
    bloomStrength: 0.65,
    bloomRadius: 0.72,
    bloomThreshold: 0.86,
  }),
});
