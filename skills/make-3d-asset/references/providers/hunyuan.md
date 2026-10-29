# Hunyuan Studio（Geo → Poly）

用法看 CLI：`node tools/hunyuan-web/hunyuan-web.mjs --help`（详见 `tools/hunyuan-web/README.md`）。低成本的 TokenHub 直出低拓扑见 [hunyuan-api.md](hunyuan-api.md)；两者不得在一次任务中隐式切换。这里只记这个服务的两阶段工作流和档位经验。

## 两阶段工作流

提交前先 `doctor` 看今日剩余次数。Geo 从参考图出高模，Poly 把高模拓扑成低模，两次各消耗一次额度。所以中间有一道确认门：Geo 结果先过 Agent 形体门（Blender 导入、clay 3/4 预览、`audit_mesh_quality.py`），通过后把预览和判断交给用户确认，再进 Poly；不合格就换参考图重做 Geo，不进 Poly。

`--out` 直接落到 `city-box/prefabs/review/<场景>/props/<key>-geo.fbx` / `<key>-low.fbx`。Poly 重试保持 `--source`、`--session` 不变只改 `--level`，各档落各自文件。

**两件以上用 `batch --jobs <jobs.json>`，不要串行跑单件。** 服务端并行生成，串行是把 4–5 分钟乘以件数；同一 profile 又不允许并行起多个 CLI，所以 batch 是并行的唯一入口。jobs 每项就是单次命令的参数，一批只能同一阶段；节奏是一批 Geo → 形体门 → 通过的组一批 Poly。

单件 `geo`/`poly` 下载失败时：任务在服务端已完成、额度已消耗，manifest 状态 `ready_to_download` 且记有 `remote_asset_preview`。不要重新提交，用 `resume --manifest ... --out ...` 取回；也可用 `fetch --stage <阶段> --preview <该 URL> --out <文件>`。默认 FBX 会捕获查看器实际加载的 COS 模型 URL，不依赖网页的 blob `download` 事件；预览图与 FBX 时间戳允许相差 2 秒但必须校验。

**站点拒收纯灰度图**（页面提示"不支持上传灰度图"，按像素判定 R=G=B，与文件是否 RGB 编码无关）。我们的 noir 参考图经常就是纯灰——生成前检查 `max|R-B|`，为 0 就给图加 ±2 的色偏再传（PIL 两行）。CLI 现在会把这类站点拒绝原因直接报出来。

## 档位经验

- **单体实心**的（花瓶、钢琴）`low` 够用。
- **带内部小件**的（卡座里的圆桌/台灯、背柜上的酒瓶）`low` 会糊掉或穿洞，直接从 `medium` 起，别浪费一次额度试 low。
- Poly 结果再审一次：非流形边、退化面、翻面黑洞；面数记进 `props.json` 或 prompts.md。
