# bgm-lab — 配乐实验

研究游戏 BGM 的生成、循环无缝性与场景适配。
各实验内部保持独立，结论成熟后迁回正式游戏的音频管线。

- [noir-loop-scores/](noir-loop-scores/) — 五版黑色电影无缝循环 BGM（纯 Node.js DSP 合成 + Web Audio 试听室；`node noir-loop-scores/generate-bgm.mjs` 可重渲全部或单曲，`check-seam.mjs` 体检循环接缝）
- 02「烟雾俱乐部」的四个变体在 `noir-loop-scores/variants.mjs`（`last-call` / `house-band` / `backroom-deal` / `dance-floor`），音频在 `noir-loop-scores/variants/`，试听已并入 `noir-loop-scores/index.html`
