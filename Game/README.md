# SSNoir — Game foundation (Stage 1)

This is the future **single game project**, not a second permanent client. During migration, `Engine/` and `UnityClient/` are preserved only as behavioral baselines. The original Scheme content still lives solely in `UnityClient/Assets/Resources/Content`.

## What is implemented

- Browser: Three.js geometry, PixiJS cards and touch/click interaction on **one WebGL context**, with a BiwaScheme expression executing on click.
- Node: smoke tests of BiwaScheme semantics and the **actual unchanged** `scripts/stdlib.scm`.
- Steam/Desktop: minimal Electron shell loading the exact same browser bundle. Steamworks is not integrated.
- WeChat: a distinct **2D host probe** bundling BiwaScheme and calling `wx.createCanvas`. It is intentionally **not** a working Three.js/PixiJS game. It must be validated in WeChat DevTools and on Android/iOS before this stack is approved.

## Commands

Use Node 22 (Vite 8 requires current Node 20/22+). Run from `Game/`.

```bash
npm install
npm run verify          # typecheck, Node Scheme tests, web build, WeChat host probe bundle
npm run capture         # Playwright browser interaction + screenshot (install Chromium first)
npm run dev             # http://localhost:5173
npm run build:web
npm run start:desktop   # run build:web first; requires Electron installed
npm run build:wechat    # import Game/dist/wechat in WeChat developer tools as Mini Game
```

`npm run capture` uses Playwright Chromium. Install once with `npx playwright install chromium`; its screenshot is saved in `artifacts/foundation-web.png`. The CI uploads the capture with the build products. A passing interaction smoke test is not a visual-quality review.

`npm run verify` checks *buildability*; **it does not prove** browser rendering, desktop GPU rendering, touch behavior, or WeChat runtime compatibility. For the browser, open the preview, click the cream card and verify the Scheme count increments while a 3D city is visible. For desktop, run the Electron shell after `build:web`. For WeChat, replace the tourist AppID as needed and run the generated host probe in developer tools and on both target phone OSes. Confirm that canvas renders and prints `Scheme result: 42`.

## Stage 1 acceptance (tracked separately)

| Gate | Automation | Current scope |
| --- | --- | --- |
| JS/TS compilation | GitHub Actions | Typecheck, Scheme smoke tests, Vite builds |
| Real content semantic compatibility | Partial | Only real `stdlib.scm` is checked; `engine.scm` and `world.scm` are NOT ported |
| Three + Pixi browser rendering | GitHub Actions + visual review | Playwright opens the web game, clicks the Pixi card, confirms Scheme result, captures screenshot |
| Electron runtime | Manual | Same Web bundle, no Steam API yet |
| WeChat host runtime | Manual | wx Canvas 2D + Scheme; not Three/Pixi |
| WeChat Three + Pixi | **Not started** | Platform go/no-go gate before migrating UI |
| Save system and game state | **Not started** | To be migrated from C# after platform go/no-go |

## Boundary decisions

- `Game/src/scheme/evaluate.ts` is a synchronous **probe** which starts a fresh interpreter per evaluation. It is **not** yet the proper long-lived World/Encounter interpreter and must not be used to run actual game content.
- No scheme files are copied from Unity; tests read the single source from the existing repository.
- No production UI layout, level art, save system, or platform API is created in this technical experiment.
- Error states are raised explicitly. No silent fallback from a missing WebGL context.
- WeChat project emits an independent IIFE as a compatibility probe. No claim of a working DOM polyfill or Three.js binding is made.
