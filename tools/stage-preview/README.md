# 舞台序列图

从真实 Scheme 入口执行 `play-stage!`，导出引擎解析后的拍点，再合成 Agent 可审阅的关键帧和 contact sheet。命令会自动构建内容校验器：

```bash
python3 tools/stage-preview/preview.py "(baines 'debug-play-street!)" --out /tmp/酒馆舞台预览
python3 tools/stage-preview/preview.py "(baines 'debug-play-street!)" --out /tmp/酒馆舞台预览 --gif
python3 tools/stage-preview/preview.py "(baines 'debug-play-street!)" --out /tmp/酒馆舞台预览 --gif-beats 12:18
python3 tools/stage-preview/preview.py "(baines 'debug-play-street!)" --out /tmp/酒馆舞台预览 --video
python3 tools/stage-preview/preview.py "(baines 'debug-play-street!)" --out /tmp/酒馆舞台预览 --gif --video
```

默认始终写出 `stage.json`、逐帧 PNG、`contact-*.png` 和时间 / 拍号 / 对白 / 音效的 `manifest.json`。`--gif` / `--gif-beats` 额外生成 640 像素宽、10 fps 的 `preview.gif`（可调 `--gif-width`、`--gif-fps`），不替代序列帧。`--video` 额外生成带 VO / SFX 的 `preview.mp4`（依赖本机 `ffmpeg` / `ffprobe`），同样是附加产物；可与 `--gif` 同开。

## 对白拍时长

已移除误导性的 `--say-seconds`。对白拍（`stage-say`）与 GIF / 视频共用同一公式，对齐客户端打字机与语音下限：

- 打字机：`len(Text) / 30` 秒（`StoryStageDrawer.TypewriterCharactersPerSecond`）
- 若 `VoiceId` 能在 `UnityClient/Assets/Resources/Voices/` 下解析到音频，取该 wav/ogg/mp3 时长
- `duration = max(打字机, VO) + 0.4s` 短留白；无 VO 时只用打字机 + 留白
- `StageSounds` 音效在拍起点叠入，**不**延长拍长（与 `StoryStagePlayer` 一致）

非对白拍仍取指令里的最大 `Seconds`（移动 / 路径 / 停顿等）。

依赖 Python Pillow；`--video` 另需 ffmpeg。默认画布 1227×690，对应 `UIScale` 默认 Compact 档的 16:9 比例。

Agent 自审优先看默认 4×4 的密集 contact sheet，每格另写拍号、时间戳与对白摘要；定位问题后再打开原尺寸单帧。缩小索引图减少翻页，但生成时间主要取决于逐帧渲染和 GIF / 视频编码，不会按页数成比例缩短。给用户审核时可导出 GIF 看整体节奏，或 `--video` 听 VO/SFX；并保留序列帧供精确反馈。时间采样与直线 / 贝塞尔路径沿用舞台拍点语义；灯管电流、过渡光晕、打字机逐字显现和真实混音空间感仍须在 Unity 试演中验收。若舞台排版常量修改，应同步审阅此工具中的对应公式，避免离线图与客户端漂移。
