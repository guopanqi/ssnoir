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
    rainCount: 460,
    rainOpacity: 0.055,
    rainLength: 0.65,
  }),

  lighting: Object.freeze({
    keyColor: 0xb8cdf3,
    keyIntensity: 3.45,
    // 更低的侧后方掠射角：优先读垂直面，避免水平道路抢走亮面积。
    keyPosition: [-44, 17, -30],
    fillColor: 0x5c79a8,
    fillIntensity: 0.10,
    fillPosition: [38, 13, 24],
    ambientSky: 0x1d2a3c,
    ambientGround: 0x030508,
    ambientIntensity: 0.14,
    streetColor: 0xe6c27a,
    streetIntensity: 2.4,
    streetDistance: 6.5,
    warehouseWork: Object.freeze({
      color: 0x91b7e5, intensity: 6.2, width: 8.0, height: 7.0,
      position: [17.0, 7.8, 7.5], target: [30.0, 3.2, 19.0],
    }),
    alleyCut: Object.freeze({
      color: 0x718caf, intensity: 234000, distance: 165,
      angle: 0.095, penumbra: 0.02, decay: 2,
      position: [47.6, 99.0, 50.0], target: [-23.8, 3.0, 32.0],
    }),
    alleyLift: Object.freeze({
      color: 0x17243a, intensity: 12, distance: 13, decay: 2,
      position: [-21.25, 4.2, 27.0],
    }),
    alleyDoor: Object.freeze({
      color: 0xe6c27a, intensity: 1, distance: 4.5, decay: 2,
      position: [-20.45, 1.8, 37.6],
    }),
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
    vignette: 0.20,
  }),
});

