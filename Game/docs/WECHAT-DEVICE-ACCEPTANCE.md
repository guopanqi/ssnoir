# WeChat Mini Game — developer tools and device acceptance

This is the manual release gate for the **single main-branch** SSNoir game. CI can validate mocked wx API contracts but cannot certify the real WeChat graphics runtime.

## Build/import

1. Use a checkout of `main`, run `cd Game && npm install && npm run build:wechat`.
2. Import **`Game/dist/wechat`**, not `Game/dist/web`, as a **小游戏** project in WeChat Developer Tools. Use your own AppID for device previews; the committed tourist AppID is for local experiments only.
3. Set landscape mode according to the committed `game.json`. Record Developer Tools version, base library version, device/OS and WeChat version.

## What to look for

1. The console should show `[SSNoir] Mini Game adapter=finscn/weapp-adapter+SSNoir`, followed by `Pixi 2D canvas backend` or `Pixi 2D canvas fallback`.
2. It must render the eight simple 3D city buildings and the Pixi Chinese text/card, **not just a blank canvas**.
3. It must show `Scheme 计算结果：1`. Tap **执行 Scheme 计数**; it should become `2`. Repeat once, check `3`.
4. On console supporting expressions, inspect `globalThis.__SSNOIR_WECHAT_FOUNDATION__?.getDiagnostics()` for `canvas2D`, `webglVersion` and API presence. The diagnostic does not contain identifiers or credentials.
5. Switch to background and return, verify the scene continues and touch still works; repeat after a cold restart.
6. Repeat once on Android and once on iOS, with native WeChat rather than just the macOS simulator.
7. For TapTap, use the **same** build via `npm run build:taptap` and the official converter output `dist/taptap/game.zip`; test the generated package, not an independent copy of the game.

## Reporting a failure

Send: device/tool version, base library, OS, whether the scene or Chinese fonts are visible, the last `[SSNoir]` console message, and the **first** red exception with the stack. Screenshots are sufficient for on-device errors. A HarmonyOS compatibility announcement is informational, not an exception.

Important interpretation:
- `wx.createOffscreenCanvas is required`: outdated build; ensure your `dist/wechat/game.js` comes from recent `main`.
- `canvas2D: secondary-canvas`: expected fallback when the offscreen API is absent; *not* itself an error.
- `WebGL2 is required` or stencil/shader failure: capture device graphics context details. Do not silently downgrade to WebGL1 because PixiJS 8 currently shares WebGL2 with Three.
- Failure after initial counter: investigate input/coordinate mapping or LIPS runtime, not just the canvas API.

Automatic green CI and successful TapTap conversion **do not** close these native device gates.

## 2026-10-11 macOS 开发者工具实测

Stable 2.02.2608080 的 3.17.4 / 3.17.3 均曾出现 `WAGame.js` 500 和 `app.json` 启动页面错误。实际导入目录与 `compileType: game` 正确；不能仅凭日志中的 `mp` 判断误导入。工具日志先报告基础库下载 `Client network socket disconnected before secure TLS connection was established`，随后 `WAGame.js not found in vendor contents map`。

将开发者工具从系统代理临时切到直连、使用 3.17.3 并重新编译后，实际显示 Three 城市、Pixi 中文卡片与 Scheme 初始值 1，控制台打印 shared foundation mounted；WebGL2 与 secondary-canvas 回退路径均实际执行。此次未确认点击计数及横屏切换，Android / iOS / TapTap 真机仍待验收。工具当前保留直连与 3.17.3，后续可在网络恢复后重新测试系统代理。

微信重建保留 `project.private.config.json` 和本地 AppID；共享项目配置仍由源目录生成，避免构建清空本地设置。请勿提交个人 AppID 到源配置。

### 按钮输入补验

同日游客 AppID 下曾出现画面正常但触摸回调完全没有响应。用户改用测试号 AppID 后恢复横屏和输入，实际点击计数增长到 20，诊断显示 `changedTouches.clientX/clientY` 正常、命中结果为 true。此结果没有隔离重启与模式切换的影响，不能把根因归到 DOM 适配或监听注册顺序；临时输入修改和日志均撤回；重建后再次实际点击，计数从 1 增长到 2，确认原始事件代码在测试号下正常。后续工具交互验收使用可用的小游戏 AppID，游客号仅作为构建默认值，不作为输入通过证据。个人 AppID 只保留在本地产物配置，重建会保留它。
