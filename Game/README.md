# SSNoir / Game — Web 游戏与玩法实验

这是独立发展的 SSNoir Web 游戏项目，不再以 Unity → WeChat/TapTap 的移植为目标。
长期路线见 [docs/WEB-GAME-ROADMAP.md](docs/WEB-GAME-ROADMAP.md)。

## 启动

在 Game/ 目录执行：

```bash
npm ci
npm run dev
```

打开 Vite 提供的 URL。可添加 `?seed=42` 固定随机数，便于重复实验。

当前入口：`src/web/main.jsx`（React UI）和 `src/web/city-scene.jsx`（独立 Three.js 视口）。
Runtime 从 `src/runtime/world-host.ts` 启动，原始 Scheme 内容仍只读取
`UnityClient/Assets/Resources/Content`，没有独立的内容拷贝。
Browser UI 通过 `src/web/session.ts` 发出命令；不直接调用 Scheme。

```bash
npm run verify      # 类型、运行时单测、Web 生产构建
npm run smoke:web   # 原始 world.scm 的浏览器启动、真实地点导航和截图
npm run build:web
```

开发环境暴露 `window.__SSNOIR_WEB__`（只为实验和 Playwright），可检查快照、进入节点或结束当天。

## 开发边界

- `src/web/`：新交互及独立视觉。UI 不应包含游戏规则。
- `src/runtime/`、`src/scheme/`：原有 LIPS Scheme 运行时、动作/Scenario/存档，继续复用。
- `src/foundation/`、`adapters/`、`platforms/`、`desktop/`：历史迁移/验证实现，不是当前 Web 入口；暂不删除，避免丢失既有研究证据。
- 第一阶段不改动任何 .scm，先验证 Runtime 与表现分离。
- 不能把新 Web UI 的编译成功当作真实故事完整可玩；P1 只是一段竖切片。

Web 目前将 Stage/Theatre 等复杂事件以简化的故事层显示，正式演出表现属于 P3；
完整的节点语义、卷宗和正式内容通关仍需在 P2–P5 逐步验收。
