export const PROFILE = Object.freeze({
  palette: Object.freeze({
    background: 0x0a1020,
    surface: 0x1e2a40,
    surfaceLift: 0x2d3b55,
    surfaceMid: 0x45556f,
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
    bloomStrength: 0.22,
    bloomRadius: 0.18,
    bloomThreshold: 0.64,
  }),
});
