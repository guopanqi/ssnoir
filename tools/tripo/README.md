# Tripo 3D 生成工具

使用 Tripo v3 API 从本地参考图、公开图片 URL 或文字生成 3D 模型。默认固定使用 `P1-20260311`，对应官方推荐的低模游戏资产模型。任务产物写在 `tmp/tripo-3d-generate/<task_id>/`，包含模型、预览和 `manifest.json`。生成会消耗账户额度；仅在确定要生成时运行。

## 密钥

在 macOS 终端执行以下命令，终端会隐藏输入；将 Key 粘贴进去并按回车。不要把 Key 写进命令行、仓库或聊天记录。已经发到聊天里的 Key 建议在 Tripo 控制台轮换，然后保存新 Key。

```bash
security add-generic-password -U -s com.ssnoir.tripo.api-key -a 'SSNoir Tripo 3D' -w
```

非 macOS 环境可在当前进程设置 `TRIPO_API_KEY`。工具优先读取 macOS Keychain。

## 使用

```bash
python3 tools/tripo/model.py --image /absolute/path/reference.png --face-limit 5000 --out /absolute/path/review
python3 tools/tripo/model.py --prompt 'A 1930s American streetlamp, low poly' --face-limit 5000
python3 tools/tripo/model.py --task-id task_xxx --out /absolute/path/review
```

## 参考图生成与调整

`image.py` 只提供文生图和单图编辑。默认用 `banana2` 的最低档 `0.5K`；指定 `--model banana_pro` 时用该模型的最低档 `1K`。默认正方形，需要半身像或横图时用 `--ratio`。输出为 PNG，保存在 `tmp/tripo-image-generate/<task_id>/`。

```bash
python3 tools/tripo/image.py --prompt '1940s American film noir detective, isolated half-body portrait' --ratio 3:4
python3 tools/tripo/image.py --image /absolute/path/source.png --edit 'Keep the face and pose; simplify the background' --model banana_pro
python3 tools/tripo/image.py --task-id task_xxx
```

生图 CLI 只处理模型调用与产物。SSNoir 的年代、风格、构图和审阅标准由 `create-3d-assets` 等项目工作流决定。Tripo 图像 API 依据：[文生图](https://developers.tripo3d.ai/zh/docs/generation-text-to-image)、[图生图](https://developers.tripo3d.ai/zh/docs/generation-image-to-image)。

P1 的 `face_limit` 范围为 50–20,000；省略时由服务自适应决定。工具固定使用 P1，不暴露其他模型及其专属参数；具体选型与质量门见 `create-3d-assets/references/providers/tripo.md`。脚本只提交、查询与下载候选资产，不自动写入正式 CityBox/Unity 资源。

API 依据：[模型与版本](https://developers.tripo3d.ai/en/docs/models-and-versions)、[P 系列单图生成](https://developers.tripo3d.ai/zh/docs/generation-image-to-model/p)、[文件上传](https://developers.tripo3d.ai/en/docs/files)、[任务查询](https://developers.tripo3d.ai/en/docs/task-query)。
