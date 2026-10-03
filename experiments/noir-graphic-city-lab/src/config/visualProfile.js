export const PROFILE = Object.freeze({
  palette: Object.freeze({
    background: 0x0a1020,
    surface: 0x182236,
    surfaceLift: 0x253249,
    surfaceMid: 0x39475e,
    line: 0xe8e5dc,
    lineDim: 0xb8bec8,
    white: 0xf8f6eb,
  }),
  camera: Object.freeze({ fov: 38, near: 0.1, far: 180 }),
  line: Object.freeze({
    primaryOpacity: 0.92,
    secondaryOpacity: 0.54,
  }),
  atmosphere: Object.freeze({
    grain: 0.040,
    vignette: 0.14,
    bloomStrength: 0.18,
    bloomRadius: 0.16,
    bloomThreshold: 0.72,
  }),
});
