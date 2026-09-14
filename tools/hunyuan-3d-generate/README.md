# Hunyuan 3D Generate

这是 SSNoir 的低成本混元 3D API 入口。它固定调用 TokenHub `hy-3d-3.0` 的 `low_poly` 模式，一次完成图/文生 3D 与智能拓扑。它不会先生成高模，也不会调用独立的 `hy-3d-retopology` 服务。

先在本机设置 TokenHub API Key：

```bash
export HUNYUAN_3D_API_KEY='...'
```

图生 3D：

```bash
python3 tools/hunyuan-3d-generate/generate.py --request - <<'JSON'
{
  "session": "道具｜老式打字机",
  "image": "/absolute/path/typewriter.png",
  "polygon_type": "triangle"
}
JSON
```

文生 3D：

```bash
python3 tools/hunyuan-3d-generate/generate.py --request - <<'JSON'
{
  "session": "道具｜老式打字机",
  "prompt": "一台1930年代美国办公室使用的黑色老式机械打字机",
  "polygon_type": "triangle"
}
JSON
```

请求字段：

- `session`：必填，用于本地结果目录命名。
- `prompt`、`image`、`image_url`：必须且只能提供一个。`image` 是本地图片绝对或相对路径；工具会校验格式、大小和尺寸，再转换为 Base64。
- `polygon_type`：可选，`triangle`（默认）或 `quadrilateral`。
- `enable_pbr`：可选，默认 `false`。SSNoir 通常在 Blender 中替换为项目材质，因此不要无故开启。

模型和生成模式不可配置。工具会拒绝 `model`、`generate_type`、`face_count` 等字段，防止这个低成本入口逐渐变成隐式的高模/拓扑流水线。

任务提交后，工具会立即把 Job ID 和请求清单写入 `tmp/hunyuan-3d-generate/`，然后轮询至完成，并下载全部模型与预览图。每个文件记录 SHA-256。中途超时不会重新提交；错误信息会给出已有 Job ID 和 `manifest.json` 路径。

只校验请求、不访问 API、不产生费用：

```bash
python3 tools/hunyuan-3d-generate/generate.py --request request.json --dry-run
```

输出模型仍是候选资产。必须按 `create-3d-assets` 的网格质量门检查轮廓、缺损、表面噪声和拓扑风险，不能直接进入 CityBox 或 Unity。
