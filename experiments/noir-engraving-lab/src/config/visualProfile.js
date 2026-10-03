export const PROFILE = Object.freeze({
  seed: 1947,

  palette: Object.freeze({
    void: 0x070a10,
    asphalt: 0x090e16,
    sidewalk: 0x0d1520,
    building: 0x111b29,
    buildingDim: 0x0b121c,
    landmark: 0x172438,
    edge: 0xcbd7e8,
    edgeDim: 0x33445e,
    warm: 0xe6c27a,
    cool: 0x8fbde6,
    red: 0x9b3829,
  }),

  camera: Object.freeze({
    fov: 42,
    near: 0.1,
    far: 220,
  }),

  atmosphere: Object.freeze({
    fogDensity: 0.016,
    rainCount: 1500,
    rainOpacity: 0.32,
    rainSize: 0.045,
  }),

  lighting: Object.freeze({
    keyColor: 0xb8cdf3,
    keyIntensity: 1.18,
    keyPosition: [-24, 32, 16],
    fillColor: 0x5c79a8,
    fillIntensity: 0.24,
    fillPosition: [34, 14, -28],
    ambientSky: 0x1d2a3c,
    ambientGround: 0x030508,
    ambientIntensity: 0.30,
    streetColor: 0xe6c27a,
    streetIntensity: 4.5,
    streetDistance: 8.5,
  }),

  print: Object.freeze({
    levels: 6,
    dither: 0.35,
    ditherScale: 1.0,
    inBlack: 0.012,
    inWhite: 0.56,
    gamma: 0.94,
    bloomStrength: 0.30,
    bloomRadius: 0.48,
    bloomThreshold: 1.05,
  }),
});
