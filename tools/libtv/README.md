# LibTV 服务层

通过已登录的官方 `libtv` CLI 操作画布。`image.py` 生成候选图片，`video.py` 生成候选视频；各自只暴露 SSNoir 实际需要的请求字段。创作判断与验收标准仍由相应 Skill 定义。

## 图片生成

这是 SSNoir 面向 Agent 的候选图片生成入口。它接收一个“创作会话 + 方案列表”，自行复用 LibTV 画布、上传并去重参考图、等待每个方案完成，最后一次性返回画布链接和全部图片 URL。调用方不操作画布、节点、轮询或图生图模式。

默认是 `General image Pro`、`1K`、`1:1`。需要更快地进行低成本探索时，可在请求里显式写 `"model": "General image V2"`。每个方案默认一张；`count` 只能是 `1`、`2` 或 `4`，会增加额度消耗。

```bash
python3 tools/libtv/image.py --request - <<'JSON'
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
python3 tools/libtv/image.py --request request.json --dry-run
```

脚本只通过已登录的 `libtv` CLI 操作 LibTV，不读写 API 密钥。


## 视频生成

SSNoir 面向 Agent 的候选视频生成入口。它接收一个「创作会话 + 场景 + 角色 + 方案列表」，
自行复用 LibTV 画布、按内容去重上传素材、拼装提示词、等待每个方案完成，
下载全部出片并产出验证图，最后一次性返回画布链接和结果。

调用方不操作画布、节点、连线、占位符、轮询或下载。

```bash
python3 tools/libtv/video.py --request - <<'JSON'
{
  "session": "过场｜酒馆后巷｜艾迪初登场",
  "duration": 6,
  "scene": { "path": "tmp/libtv/酒馆-后巷.png" },
  "characters": [
    { "name": "艾迪", "image": "UnityClient/Assets/Resources/Portraits/Neon/艾迪.png" }
  ],
  "variants": [
    { "name": "方案A｜门口停步", "prompt": "艾迪从后巷门口出现，停下观察街面。" },
    { "name": "方案B｜倚门点烟", "prompt": "艾迪倚在后巷门口，低头点烟后望向街面。" }
  ]
}
JSON
```

## 请求字段

| 字段 | 必填 | 默认 | 说明 |
| --- | --- | --- | --- |
| `session` | 是 | — | 创作会话名。同名会话始终落在同一张画布，首次才创建。 |
| `scene.path` | 是 | — | 场景图路径。**由用户在游戏里截取**，工具不生成也不挑选。 |
| `characters[]` | 否 | `[]` | 每项 `{name, image}`。`name` 是角色在提示词里被称呼的名字。 |
| `variants[]` | 是 | — | 每项 `{name, prompt}`，至少一个。`prompt` 写自然语言剧情即可。 |
| `duration` | 否 | `6` | 4–15 秒。 |
| `takes` | 否 | `2` | 同一个方案跑几条候选，1–4。用于 A/B 选优。 |
| `resolution` | 否 | `720p` | `480p` 或 `720p`。 |
| `sound` | 否 | `true` | 是否生成音轨。 |
| `model` | 否 | `Seedance 2.0 Mini` | 目前只允许这一个；传别的直接报错。 |
| `outputDir` | 否 | `tmp/libtv/<session>` | 视频与验证图的落地目录。 |

`variants` 和 `takes` 是两个不同的轴：**`variants` 是不同提示词的创作方案，
`takes` 是同一句提示词跑出的多条候选**（模型自身的差异）。选片时两个都要看。

## 固定在内部、不接受传入

- **`mixed2video`**：只有这个模式能同时挂场景底片和角色参考。
- **`16:9`**：与游戏画面一致。换比例等于把场景底片裁掉一块。
- **`autoCompliance=0`**：参考图全是插画，跳过真人合规检测省一轮等待。
- **提示词头部**：工具会在你的 `prompt` 前面自动加上角色/场景的绑定、
  「全程镜头完全固定」和「禁止 BGM，保留人声和音效」。
  **镜头固定是硬编码的，当前不可配** —— 它绑的是「场景底片 + 固定机位」这个机制本身，
  不是创作口味。要运动镜头得改这个工具，不是改请求。

传 `aspectRatio` / `ratio` / `modeType` 会被拒绝。传 `audio` 也会被拒绝：
参考音频这条路径**还没有实测过**，不做未验证的承诺。

## 返回字段

```json
{
  "status": "completed",
  "session": "过场｜酒馆后巷｜艾迪初登场",
  "canvasUrl": "https://www.liblib.tv/canvas?spaceId=...&projectId=...",
  "model": "Seedance 2.0 Mini",
  "duration": 6, "takes": 2, "resolution": "720p", "aspectRatio": "16:9", "sound": true,
  "outputDir": "tmp/libtv/过场｜酒馆后巷｜艾迪初登场",
  "runStamp": "20260831-160528",
  "results": [
    {
      "name": "方案A｜门口停步",
      "status": "completed",
      "prompt": "<工具实际提交的完整提示词，含自动加的头部>",
      "takes": [
        {
          "take": "A",
          "videoUrl": "https://...mp4",
          "video": "tmp/libtv/.../方案A｜门口停步｜20260831-160528-A.mp4",
          "frameStrip": "tmp/libtv/.../方案A｜门口停步｜20260831-160528-A-抽帧条.jpg",
          "plateComparison": "tmp/libtv/.../方案A｜门口停步｜20260831-160528-A-首帧对比.jpg"
        }
      ]
    }
  ]
}
```

`status` 为 `completed` 或 `partial_failure`。**某个方案失败不影响其余方案**，
失败项带 `status: "failed"` 和 `error`。退出码：全部成功 `0`，有失败 `1`。

`frameStrip` 是均匀抽 9 帧铺成的一条图，用来看节拍和人数。
`plateComparison` 是「输入场景图 / 输出首帧」上下叠图，用来看风格有没有漂——
描线还在吗、建筑有没有被重新打光。这个只有叠起来看才发现得了。

## 会话与素材复用

同一个 `session` 的所有请求落在同一张画布（`视频会话｜<session>`）。

素材节点名带内容哈希（`场景｜酒馆-后巷｜a1b2c3d4`），所以：
**同一张图在会话内只上传一次**，后续请求直接引用；
换了内容自然是另一个节点名，不会拿旧图去生成。

**每次提交都是一轮新候选。** 本次提交的所有方案共用一个 `runStamp`（返回 JSON 里有），
画布节点名和本地文件名都带上它：

```
画布   生成｜方案A｜门口停步｜20260831-160528
本地   方案A｜门口停步｜20260831-160528-A.mp4
```

所以同一个方案名重复提交**既不会覆盖画布上的上一轮，也不会覆盖本地的上一轮文件**——
两轮并排放着才能比较。

## 重要的 LibTV 语义

素材会立即入画布；**生成节点和视频只在服务端任务完成后才一起入画布**。
所以等待期间没有进度输出、画布上暂时看不到生成节点，都是正常现象。
工具会在标准错误打印当前正在等待的方案，并从最终响应直接取得 URL；
它不会要求调用方查询节点状态，也不会因为看不到节点就重复提交。

一条 6 秒的片子通常要等一两分钟，12 秒更久。**不要设短超时，不要中途重试。**

`--dry-run` 只检查请求格式与素材文件是否存在，不访问 LibTV：

```bash
python3 tools/libtv/video.py --request request.json --dry-run
```

脚本只通过已登录的 `libtv` CLI 操作 LibTV，不读写 API 密钥。
需要 `ffmpeg` 才能产出验证图；缺了不会失败，会在对应 take 上返回 `verifyError`。
