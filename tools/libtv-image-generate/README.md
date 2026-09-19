# LibTV Image Generate

这是 SSNoir 面向 Agent 的候选图片生成入口。它接收一个“创作会话 + 方案列表”，自行复用 LibTV 画布、上传并去重参考图、等待每个方案完成，最后一次性返回画布链接和全部图片 URL。调用方不操作画布、节点、轮询或图生图模式。

默认是 `General image Pro`、`1K`、`1:1`。需要更快地进行低成本探索时，可在请求里显式写 `"model": "General image V2"`。每个方案默认一张；`count` 只能是 `1`、`2` 或 `4`，会增加额度消耗。

```bash
python3 tools/libtv-image-generate/generate.py --request - <<'JSON'
{
  "session": "人物｜弗兰克",
  "references": ["UnityClient/Assets/Resources/Portraits/Neon/尼尔.png"],
  "variants": [
    {"name": "方案A｜码头工头", "prompt": "Full-body neon-sign character portrait ..."},
    {"name": "方案B｜工会领袖", "prompt": "Full-body neon-sign character portrait ..."}
  ]
}
JSON
```

请求字段只有这些：

- `session`：创作主题名。同名会话始终复用同一画布；首次才创建。项目里的命名契约：人物立绘用
  `人物｜<角色名>`（如 `人物｜莱恩`），场景基底用 `场景｜<地点名>`，道具用 `道具｜<物件名>`——同一对象的
  所有历史方案集中在同一张画布上。
- `references`：可选的本地图片路径或图片 URL。它既可以是风格参考，也可以是上一轮 `results[].images` 返回的 URL；后者在同一 session 内会优先直接连到已有生成节点，配合“只修改……”类提示词可作为局部编辑的输入。历史结果 URL、外部 URL 或本地文件无法匹配已有结果时，工具才下载/上传为参考资源。某个方案独有的 reference 可写在该方案的 `references`。
- `model`：可选，默认 `General image Pro`；快速探索可用 `General image V2`。
- `count`：可选的默认候选数；可由单个方案覆盖。
- `variants`：至少一个 `{ "name", "prompt" }`。工具依次完成，某项失败会继续其余项并以 `partial_failure` 明确返回。

标准输出是一个 JSON 结果，包含 `canvasUrl` 和每个方案的 `results[].images`。工具不规定调用方如何组织创作、选择或后续迭代；再次使用相同 `session` 时，结果会进入同一画布。

## 重要的 LibTV 语义

参考资源会立即入画布；**生成节点和图片则只会在服务端任务完成后一起入画布**。所以等待过程中没有进度输出、画布暂时只显示参考图，都是正常现象。工具会在标准错误打印当前正在等待的方案，并从 `--run` 的最终响应直接取得 URL；它绝不会要求调用方查询节点状态，也不会因看不到“空生成节点”而重复提交。

同一 session 中，同一文件内容的 reference 会复用已上传的参考节点。工具也会记录自己生成的“图片 URL → 节点”映射：把上一轮返回 URL 传回时会优先直接复用其生成节点，不必下载或新增参考节点。历史结果 URL 或不属于当前 session 的 URL 没有可用映射时，会安全回退到下载/上传参考图。

`--dry-run` 只检查请求格式，不访问 LibTV：

```bash
python3 tools/libtv-image-generate/generate.py --request request.json --dry-run
```

脚本只通过已登录的 `libtv` CLI 操作 LibTV，不读写 API 密钥。
