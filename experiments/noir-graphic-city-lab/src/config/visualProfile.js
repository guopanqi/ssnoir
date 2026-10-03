export const PROFILE = Object.freeze({
  palette: Object.freeze({
    background: 0x080d1a,
    surface: 0x111827,
    surfaceLift: 0x1a2436,
    surfaceMid: 0x263246,
    line: 0xe8e5dc,
    lineDim: 0xa8adba,
    white: 0xf8f6eb,
  }),
  camera: Object.freeze({ fov: 38, near: 0.1, far: 180 }),
  line: Object.freeze({
    primaryOpacity: 0.92,
    secondaryOpacity: 0.54,
  }),
  atmosphere: Object.freeze({
    grain: 0.032,
    vignette: 0.14,
    bloomStrength: 0.16,
    bloomRadius: 0.16,
    bloomThreshold: 0.72,
  }),
});
