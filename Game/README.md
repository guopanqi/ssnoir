# SSNoir / Game — Phase 1 foundation

This is the planned **only** game project. Legacy C#/Unity code is retained during migration as a reference, not as a second long-term client.

## Shared architecture (real prototype)

- Browser: \`src/main.ts\` provides HTML Canvas and pointer events.
- WeChat: \`src/wechat/bootstrap.ts\` exposes a deliberately *minimal* WX Canvas/browser compatibility surface; \`src/wechat/probe.ts\` installs a Pixi \`DOMAdapter\`.
- **Both hosts execute the same \`src/foundation/render.ts\`** using Three.js, PixiJS, a single WebGL context, and a persistent BiwaScheme VM.
- Desktop/Electron opens the exact same browser bundle via \`desktop/main.cjs\`.
- Scheme is currently a compatibility probe only. The existing \`.scm\` content remains in \`UnityClient/Assets/Resources/Content\`, without a second copy. Game rules/native functions are not yet migrated.

## Test commands (from Game/)

\`\`\`bash
npm install
npm run verify             # TypeScript, real stdlib / Scheme tests, browser build, WeChat bundle
npx playwright install chromium
npm run capture            # headless Chromium actually renders and clicks, uploads PNG in CI
npm run dev                # browser interactive preview
npm run build:web
npm run start:desktop      # Electron requires a graphical desktop
npm run build:wechat       # import dist/wechat in WeChat DevTools as Mini Game
\`\`\`

## Automated / manual acceptance (never conflate them)

| Gate | Implemented | Evidence required |
|---|---|---|
| Scheme syntax/semantics | Yes, tests using *real stdlib.scm* | GitHub Actions pass |
| Browser 3D + 2D same GL context | Yes | Playwright click succeeds, screenshot reviewed |
| Browser resized/mobile touch | Basic layout | Playwright additional viewport / touch tests |
| Electron window | Browser bundle reused | Desktop graphical smoke still needed |
| WeChat Three + Pixi shared GL | Code and packaging in place | **WeChat DevTools + Android and iOS real devices pending** |
| WeChat font/touch/lifecycle | Touch manually routed by WX event | Real devices pending |
| Full \`engine.scm\` / \`world.scm\` | Not yet | Stage 2 after platform decision |
| Game state, saves, cutscenes | Not yet | Stage 2+ |

### WeChat steps

1. \`npm run build:wechat\`; open \`Game/dist/wechat\` as a **Mini Game** project (not Mini Program).
2. Replace tourist AppID with your own before real-device tests. SDK interface support depends on the device and WeChat base library version.
3. Confirm 3D block city **and** the 2D Pixi card are visible in **the same image**, then tap the cream card: counter should rise from 1 to 2.
4. Check console for \`[SSNoir] Three/Pixi/Scheme shared foundation mounted\`; any error must be recorded, never silently work around it.
5. Repeat on Android and iOS. A successful bundle and browser screenshot are **not** proof of actual WeChat support.

The WeChat environment adapter intentionally throws for missing offscreen-canvas, remote asset fetch or XML parsing. Fix unsupported essentials deliberately after collecting device logs; do not claim that untested APIs are supported.

## Continuation

CI workflow: \`.github/workflows/ssnoir-game-foundation.yml\`. Each run uploads browser screenshots and all build outputs as \`ssnoir-game-foundation\`; it publishes the \`ssnoir/game-foundation\` commit status with a link to its run. This makes the test/inspect/fix cycle retrievable across agents.

**Do not remove Unity or claim Phase 1 passed until actual desktop and WeChat target-runtime evidence is present.**
