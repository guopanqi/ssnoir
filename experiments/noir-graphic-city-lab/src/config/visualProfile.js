export const PROFILE = Object.freeze({
  seed: 1948,
  palette: Object.freeze({
    void: 0x050505,
    ink: 0x090a0b,
    inkLift: 0x171716,
    mid: 0x4a4842,
    paperDim: 0x8d8779,
    paper: 0xe9e0ca,
    gold: 0xd4a13b,
    goldHot: 0xffd77a,
  }),
  camera: Object.freeze({
    fov: 34,
    near: 0.1,
    far: 180,
  }),
  graphic: Object.freeze({
    lineOpacity: 0.72,
    lineThreshold: 24,
    goldBudget: 0.028,
  }),
  print: Object.freeze({
    levels: 4,
    inBlack: 0.01,
    inWhite: 0.82,
    gamma: 0.88,
    grain: 0.020,
    vignette: 0.23,
  }),
});
