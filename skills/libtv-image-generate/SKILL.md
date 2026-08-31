---
name: libtv-image-generate
description: 为 SSNoir 生成候选人物、场景或道具图片时使用。通过项目封装的 LibTV Image Generate 提交方案批次，调用方无需操作 LibTV 画布或节点。
---

# LibTV Image Generate

这是一个图片生成 API 的命令行入口。调用：

```bash
python3 tools/libtv-image-generate/generate.py --request - <<'JSON'
{"session":"人物｜主题","variants":[{"name":"方案名","prompt":"图片提示词"}]}
JSON
```

输入 JSON：`session` 为非空字符串；`variants` 为至少一个 `{name, prompt}`。可选 `references`（本地图片路径或图片 URL 数组）、`model`、`count`。`references` 可传风格图，也可传上一轮 `images` 中返回的 URL；同一 session 内，工具会优先直接复用该生成图节点，配合增量提示词可用于局部修改。默认 `General image Pro`、`1K`、`1:1`；`count` 可为 `1`、`2`、`4`。需要快速探索时可显式使用 `General image V2`。

### Session 命名契约（统一跨会话画布复用）
- **人物立绘 / 肖像**：固定为 `人物｜<角色名>`（如 `人物｜莱恩`、`人物｜弗兰克`、`人物｜林`），同一个角色的所有历史方案与迭代均集中于同一画布。
- **场景基底 / 镜头**：固定为 `场景｜<地点名>`（如 `场景｜老街酒馆`、`场景｜码头`）。
- **道具 / 物件**：固定为 `道具｜<物件名>`。

输出 JSON：`status`、`canvasUrl`、`results`。每一项 `results` 有 `name`、`status`、`images`；成功时 `images` 是图片 URL 数组，失败时有 `error`。同一 `session` 会自动复用同一个画布。

LibTV 会先显示参考资源，服务端完成后才一起显示生成节点与图片；等待时没有进度输出是正常的。该工具已等待最终响应并返回结果，不需要查询节点。

字段细节见 [`tools/libtv-image-generate/README.md`](../../tools/libtv-image-generate/README.md)。
