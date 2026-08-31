---
name: libtv-video-generate
description: 为 SSNoir 生成候选镜头视频时使用。通过项目封装的 LibTV Video Generate 提交场景、角色与方案批次，调用方无需操作 LibTV 画布或节点。
---

# LibTV Video Generate

这是一个视频生成 API 的命令行入口。调用：

```bash
python3 tools/libtv-video-generate/generate.py --request - <<'JSON'
{
  "session": "过场｜酒馆后巷｜艾迪初登场",
  "scene": {"path": "tmp/libtv/酒馆-后巷.png"},
  "characters": [{"name": "艾迪", "image": "UnityClient/Assets/Resources/Portraits/Neon/艾迪.png"}],
  "variants": [
    {"name": "方案A｜门口停步", "prompt": "艾迪从后巷门口出现，停下观察街面。"},
    {"name": "方案B｜倚门点烟", "prompt": "艾迪倚在后巷门口，低头点烟后望向街面。"}
  ]
}
JSON
```

输入 JSON：`session` 为非空字符串；`scene.path` 为场景图路径（由用户在游戏里截取）；
`variants` 为至少一个 `{name, prompt}`，`prompt` 写自然语言剧情即可。
可选 `characters`（`{name, image}` 数组）、`duration`（默认 `6`，4–15）、
`takes`（默认 `2`，同一方案跑几条候选）、`resolution`（默认 `720p`）、
`sound`（默认 `true`）、`model`（默认且目前仅支持 `Seedance 2.0 Mini`）、`outputDir`。

`16:9`、`mixed2video`、镜头固定与禁 BGM 固定在内部，不接受传入；`audio` 尚未支持。

输出 JSON：`status`、`session`、`canvasUrl`、`outputDir`、`runStamp`、`results`。
每一项 `results` 有 `name`、`status`、`prompt` 和 `takes`；
每个 take 有 `videoUrl`、本地 `video`、以及验证产物 `frameStrip`、`plateComparison`。
失败项有 `error`，其余方案继续完成，整体返回 `partial_failure`。

`session` 是创作会话：同名会话始终复用同一张画布，素材按内容哈希去重、只上传一次。
每次提交共用一个 `runStamp`，画布节点名和本地文件名都带上它，所以重复提交同一个方案名
**不会覆盖上一轮候选**，两轮可以并排比较。要迭代就沿用同一个 `session`。

LibTV 会先显示素材，服务端完成后才一起显示生成节点与视频；等待时没有进度输出是正常的。
该工具已等待最终响应并返回结果，**不需要查询节点，也不要中途重试**。
一条 6 秒片子通常要等一两分钟。

字段细节见 [`tools/libtv-video-generate/README.md`](../../tools/libtv-video-generate/README.md)。
