---
name: Blueprint Noir
colors:
  surface: '#11131a'
  surface-dim: '#11131a'
  surface-bright: '#373940'
  surface-container-lowest: '#0b0e14'
  surface-container-low: '#191c22'
  surface-container: '#1d2026'
  surface-container-high: '#272a31'
  surface-container-highest: '#32353c'
  on-surface: '#e1e2eb'
  on-surface-variant: '#c2c6d5'
  inverse-surface: '#e1e2eb'
  inverse-on-surface: '#2e3037'
  outline: '#8c909e'
  outline-variant: '#424753'
  surface-tint: '#abc7ff'
  primary: '#abc7ff'
  on-primary: '#002f66'
  primary-container: '#0464cb'
  on-primary-container: '#dde6ff'
  inverse-primary: '#005cbc'
  secondary: '#b1c7f5'
  on-secondary: '#193056'
  secondary-container: '#31476e'
  on-secondary-container: '#a0b5e3'
  tertiary: '#ffb693'
  on-tertiary: '#561f00'
  tertiary-container: '#ae4701'
  on-tertiary-container: '#ffe0d3'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#d7e2ff'
  primary-fixed-dim: '#abc7ff'
  on-primary-fixed: '#001b3f'
  on-primary-fixed-variant: '#004590'
  secondary-fixed: '#d7e2ff'
  secondary-fixed-dim: '#b1c7f5'
  on-secondary-fixed: '#001a40'
  on-secondary-fixed-variant: '#31476e'
  tertiary-fixed: '#ffdbcc'
  tertiary-fixed-dim: '#ffb693'
  on-tertiary-fixed: '#351000'
  on-tertiary-fixed-variant: '#7a3000'
  background: '#11131a'
  on-background: '#e1e2eb'
  surface-variant: '#32353c'
typography:
  display-lg:
    fontFamily: Hanken Grotesk
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Hanken Grotesk
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
  headline-lg-mobile:
    fontFamily: Hanken Grotesk
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  body-md:
    fontFamily: Hanken Grotesk
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  data-mono:
    fontFamily: JetBrains Mono
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: 0.05em
  label-xs:
    fontFamily: JetBrains Mono
    fontSize: 11px
    fontWeight: '700'
    lineHeight: 16px
spacing:
  unit: 4px
  gutter: 24px
  margin-mobile: 16px
  margin-desktop: 40px
  hud-padding: 12px
---

## Brand & Style
The design system establishes a "Blueprint Noir" aesthetic—a synthesis of high-contrast technical drafting and atmospheric urban exploration. It targets users seeking a deep, investigative experience where the interface feels like a digital lens over a physical city. 

The style is an **Expressive** hybrid of Minimalism and Tactile Technicality, now refined with a **Fidelity** color approach. It leverages the raw, hand-drawn precision of architectural cross-sections paired with the immersive depth of a dark-mode HUD. The visual tone is intellectual and precise; with the updated palette, it shifts from "electric" back to a more "authentic technical" atmosphere. It evokes the feeling of an architect or detective uncovering hidden layers of a metropolis using high-fidelity sensors. Key visual motifs include hairline-thin technical lines, coordinate overlays, and a strict adherence to a "deep zoom" spatial logic where detail density increases as the user focuses.

## Colors
The palette is rooted in a "Deep Midnight" base but utilizes a "Fidelity" color variant to provide natural, sensor-accurate contrast for technical data.

- **Primary (Steel Blue):** Used for foundational interactive elements and the primary "canvas" of the experience. It creates a sense of professional-grade utility and digital precision.
- **Secondary (Muted Slate):** Used for active data paths, blueprint outlines, and interactive HUD elements. This represents the technical layer with a grounded, balanced feel.
- **Tertiary (Burnt Amber):** Reserved for points of interest (POIs), critical alerts, and discovery markers. Its earthy, high-fidelity orange tone provides a sharp, functional break from the blue-heavy environment.
- **Neutral (Industrial Gray):** Used for secondary technical annotations, standard text, and subtle structural lines, maintaining a neutral, non-distracting undertone throughout the UI.

## Typography
Typography is split into two functional roles: narrative and data.

- **Narrative (Hanken Grotesk):** A clean, sharp sans-serif used for headlines, building names, and descriptions. It provides a contemporary, professional feel that balances the technical monospaced elements.
- **Technical (JetBrains Mono):** Used for all HUD data, coordinates, dimensions, and metadata. The monospaced nature ensures that data tables and numerical values align perfectly, reinforcing the architectural blueprint theme.

All labels should be treated as "call-outs," often paired with thin leader lines that point directly to the subject in the 3D space.

## Layout & Spacing
The layout follows a **Fluid HUD** model. Rather than a traditional grid, elements are anchored to the corners of the viewport or "tethered" to specific coordinates within the 3D map.

- **Safe Zones:** High-level navigation (Map/Global view) is anchored to the top and bottom edges.
- **Contextual Panels:** When a building is selected, side-mounted panels slide in using a 24px gutter from the screen edge.
- **Focus Reflow:** On mobile, the UI collapses into a vertical stack; architectural specifics are presented in an expandable bottom sheet to keep the central "Deep Zoom" area visible.
- **Rhythm:** A 4px base unit ensures all technical lines and borders align to a consistent mathematical rhythm, mimicking a drafting board.

## Elevation & Depth
Depth is not conveyed via shadows, but through **Tonal Layers and Line Weight**, now rendered with a high-fidelity color profile.

- **Z-Axis Hierarchy:** Background elements are rendered in low-opacity primary or neutral lines. As the user zooms in ("Focus"), the line weight of the target building increases and its color shifts to full-opacity Steel Blue.
- **Glassmorphism HUD:** HUD panels use a high-blur background (20px+) with a 40% opacity fill. This allows the city geometry to remain visible beneath the interface.
- **Technical Outlines:** Surfaces are defined by 1px solid borders. Hovering over an element adds a secondary "glow" stroke (2px) using the primary color to simulate a light-table effect.
- **Parallax:** UI labels and data points exist on a plane slightly "above" the city geometry, moving at a slower rate during rotation to emphasize the HUD overlay.

## Shapes
The design system utilizes **Sharp (0)** roundedness. All corners are 90-degree angles to maintain the architectural and blueprint-inspired aesthetic. 

Small exceptions are made for "Points of Interest" markers which may use 45-degree chamfered corners (clipped corners) to denote interactive status without softening the overall technical "edge" of the system. Buttons and input fields must be strictly rectangular.

## Components
- **Technical Buttons:** Rectangular, 1px Steel Blue border, with label text in monospaced caps. No fill by default; on hover, they fill with a 10% Primary tint.
- **HUD Chips:** Small, transparent labels with a vertical "leader line" connecting them to map features. Use Burnt Amber for critical POI chips to ensure they stand out against the blue environment.
- **Focus Cards:** Appear on the right side of the screen when a building is selected. They feature a wireframe thumbnail and a vertical scroll of technical specs.
- **Coordinate Inputs:** Input fields feature a subtle "crosshair" icon in the corner. Text entry is always monospaced.
- **Crosshair Cursor:** The standard pointer is replaced with a technical crosshair that displays local X/Y coordinates in real-time.
- **Scan Lines:** A subtle, low-opacity horizontal scan line animation should run over the active "Focus" panels to reinforce the digital sensor narrative.