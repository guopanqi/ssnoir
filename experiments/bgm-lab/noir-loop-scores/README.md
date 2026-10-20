# BGM 试听室 — 黑色电影循环配乐

五版**程序化合成**的黑色电影（film noir）BGM，全部按无缝循环设计：

| # | 曲名 | 气质 | 关键参数 | 建议场景 |
|---|------|------|----------|----------|
| 01 | 雨夜长巷 Rainy Alley | 传统 noir 慢板爵士 | 72 BPM · D 小调 · 26.7s | 主菜单／漫步／独白 |
| 02 | 烟雾俱乐部 Smoky Club | F 小调 12 小节布鲁斯 | 96 BPM · 30s | 酒吧／爵士俱乐部 |
| 03 | 侦探办公室 Cold Case | 无节拍氛围悬疑 | 16s | 解谜／翻线索／审讯 |
| 04 | 长衫侠影 Shanghai Noir | 五声音阶 × noir（致敬活侠传式民乐气质） | 84 BPM · A 小调 · 22.9s | 过场／江湖章节 |
| 05 | 缉凶行动 The Manhunt | Peter Gunn 式 riff | 112 BPM · E 小调 · 17.1s | 跟踪／追逐／倒计时 |

## 文件

- `audio/*.m4a` — AAC 压缩版（试听与游戏分发用，每首 2–3MB）
- `audio/*.wav` — 16-bit 无损母带（渲染为 `循环长度 × 重复次数`，便于拖进剪辑软件肉眼确认接缝；已加入 .gitignore）
- `index.html` — 试听室（正曲五版 + 烟雾俱乐部四个变体，Web Audio 解码后 `loop = true`，逐采样无缝）
- `variants/` — 变体音频目录（由 `variants.mjs` 生成）
- `generate-bgm.mjs` — 生成器（纯 Node.js DSP，零依赖）
- `check-seam.mjs` — 接缝体检（循环边界 RMS 对比 + 削波样本计数）

## 无缝循环的实现

1. 事件（音符、鼓、人声）渲染进 `[循环长 + 残响长]` 的 buffer，
   越过循环终点的衰减通过 tail wrap-add 折回开头；
2. 常驻层（雨声、黑胶底噪、持续音 drone）按整数周期／整频率量化生成，
   构造上即无缝；
3. 母带做响度一致化（各曲 RMS ≈ −16 dBFS）＋ 软限幅，便于 A/B 对比。

## 常用操作

```bash
node generate-bgm.mjs      # 渲染全部五版（wav + m4a）
node generate-bgm.mjs 3    # 只重新渲染第 3 版
node check-seam.mjs        # 接缝体检
```

调风格：改 `track1()` … `track5()` 里的 BPM、和弦进行、乐器密度、环境层参数
（`bed`、`vinyl`、`drone`）。乐器音色与合成细节在同文件的
`piano / bass / trumpet / pluck / saxStab / swish / ride …` 函数。
