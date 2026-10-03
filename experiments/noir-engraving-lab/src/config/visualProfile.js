export const PROFILE = Object.freeze({
  seed: 1947,

  palette: Object.freeze({
    void: 0x070a10,
    asphalt: 0x090e16,
    sidewalk: 0x141e2b,
    building: 0x1b293c,
    buildingDim: 0x101a28,
    landmark: 0x263a57,
    edge: 0xcbd7e8,
    edgeDim: 0x33445e,
    warm: 0xe6c27a,
    cool: 0x8fbde6,
    red: 0x9b3829,
  }),

  renderer: Object.freeze({
    exposure: 1.22,
  }),

  camera: Object.freeze({
    fov: 42,
    near: 0.1,
    far: 220,
  }),

  atmosphere: Object.freeze({
    fogDensity: 0.011,
    rainCount: 720,
    rainOpacity: 0.18,
    rainLength: 0.72,
  }),

  lighting: Object.freeze({
    keyColor: 0xb8cdf3,
    keyIntensity: 3.25,
    keyPosition: [-38, 24, 28],
    fillColor: 0x5c79a8,
    fillIntensity: 0.12,
    fillPosition: [34, 14, -28],
    ambientSky: 0x1d2a3c,
    ambientGround: 0x030508,
    ambientIntensity: 0.18,
    streetColor: 0xe6c27a,
    streetIntensity: 2.4,
    streetDistance: 6.5,
  }),

  print: Object.freeze({
    levels: 6,
    dither: 0.18,
    ditherScale: 1.0,
    inBlack: 0.006,
    inWhite: 0.34,
    gamma: 0.90,
    bloomStrength: 0.12,
    bloomRadius: 0.34,
    bloomThreshold: 1.20,
  }),
});
