# Evaluating community Three.js adapters, not changing production yet

**Branch**: `experiment/wechat-community-adapter`.

**Candidate**: [minisheeep/threejs-miniprogram-template, `wechat-game-ts`](https://github.com/minisheeep/threejs-miniprogram-template/tree/wechat-game-ts), commit `df8fb2af099d9ef7ada868043602dbd5470a57f1`. Candidate depends on `@minisheep/mini-program-polyfill-core@1.1.4`, `@minisheep/three-platform-adapter@2.0.4`, the independently hosted `https://npm.minisheep.cn` registry, and the build-time plugin.

The `src/wechat-community/probe.ts` entry uses **the real shared** `src/foundation/render.ts` (Three r186 + Pixi 8 + LIPS and original stdlib). No copy of the game logic. It delegates browser-global shims and WebGL canvas to the community adapter; Pixi uses the polyfilled Canvas via its official DOMAdapter.

Acceptance steps:
- [ ] Candidate dependency installation in GitHub Actions with a clean environment
- [ ] Vite plugin builds WebGL2 / Pixi 8 + LIPS (under 4MiB)
- [ ] Mock wx runtime can execute the community-adapted IIFE and increment Scheme via touch
- [ ] Same test with wx.createOffscreenCanvas unavailable, matching the actual WeChat DevTools failure
- [ ] TapTap official converter accepts the candidate build and smoke interaction still works
- [ ] Manual WeChat DevTools, Android, iOS tests

**Do not merge this adapter into the normal mini-game build until these gates pass**. In particular the private registry is a long-term maintenance/dependency risk; evaluate before committing to it. Older alternates: deepkolos/platformize targets Three r133 and Pixi 6; wechat-miniprogram/threejs-miniprogram uses Three r108 and is a Mini Program, not a Mini Game, solution.

Run from `Game/`:

```sh
npm install --no-audit --no-fund
npm run build:wechat-community
npm run smoke:wechat-community
SSNOIR_WX_CANVAS_MODE=missing npm run smoke:wechat-community
```

Automatic tests do not establish actual WeChat or TapTap device support.
