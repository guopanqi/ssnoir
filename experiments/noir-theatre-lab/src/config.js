export const PROFILE = {
  renderer: {
    exposure: 0.92,
    background: 0x050505,
  },
  palette: {
    paper: 0xd8d2c3,
    pale: 0xaaa69c,
    mid: 0x595a57,
    dark: 0x171819,
    ink: 0x050505,
    warm: 0xd6b572,
  },
  material: {
    roughness: 0.93,
    metalness: 0.02,
  },
  city: {
    extent: 74,
    rotationY: -0.20,
  },
  alley: {
    extent: 90,
    rotationY: Math.PI,
  },
  line: {
    strength: 0.68,
    threshold: 0.035,
    structuralOpacity: 0.72,
  },
  print: {
    levels: 5,
    hatchStrength: 0.18,
    grain: 0.006,
    vignette: 0.36,
  },
};

export const MODES = ['shape', 'light', 'line', 'final'];

