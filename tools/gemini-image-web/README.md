# Gemini Image Web CLI

通过已登录的 Gemini 网页生成图片，不需要 API key。默认后台运行、选择 **Flash**；`--model Argon` 可明确切换。网页模型名称不能证明实际调用的后台图片模型。

## 图片制作经验

这些是本工具的参考图与输出经验，项目视觉标准由调用方决定。

- 文生图使用 `--prompt` / `--prompt-file`；编辑已有形象使用 `--reference` 并说明保留与修改范围。保持人物身份和尺度时，优先只上传当前标准基础图，用文字描述旧姿势；多个参考可能让旧图的取景覆盖新标准，需要多参考时明确各图职责并检查结果。
- 不依赖网页服务直接输出真实透明通道。需后续分离背景时指定便于抠图的均匀纯色底，避免背景物件、投影和模拟透明的棋盘格；底色应考虑主体衣物与肤色，避免同色区域误删。先审造型，再处理 alpha，并检查边缘与浅色衣物。
- 网页显示或生成下载成功不代表图片满足调用方的风格、比例或透明要求；检查实际下载文件。服务模型与并发能力按下文和 CLI 当前行为判断，不从页面营销名称推断后台模型。

## 登录与运行

```bash
npm --prefix tools/gemini-image-web install
node tools/gemini-image-web/gemini-image-web.mjs login
node tools/gemini-image-web/gemini-image-web.mjs doctor
node tools/gemini-image-web/gemini-image-web.mjs generate \
  --prompt-file /absolute/path/prompt.txt \
  --out /absolute/path/review/候选图
```

登录由用户在打开的 Chrome 中手动完成。关闭该登录窗口后再运行其他命令，不需要退出日常使用的 Chrome。默认会话目录为 `~/.codex/browser-profiles/gemini-manual-login`；使用 `--profile DIR` 可指定其他目录。登录是否被 Google 接受仍取决于服务端。

- `--prompt TEXT` 与 `--prompt-file FILE` 二选一；`--prompt-file -` 从 stdin 读取。
- `--reference FILE` 可重复指定。首次上传若需同意网页条款，用 `--headed` 手动完成。附件未上传完、恢复图片模式后附件丢失时，立即停止，不退化为无参考图生成。
- `--out` 指定文件或以 `/` 结尾的目录；扩展名按实际文件格式修正。指定后自动推导 session/name；不指定时需要 `--session`，可选 `--name`。同名任务会覆盖旧记录，候选对照应使用不同名称。
- `--model Argon|Flash` 默认 Flash，匹配版本前缀；Flash-Lite 不会被当作 Flash。无法确认指定选择时停止。
- `--headed` 打开可见窗口用于排查；日常省略，后台默认最多六个 generate 并行执行、各用独立标签页并复用 Chrome，日志和心跳实时显示。空闲 10 分钟自动关闭。
- `--debug` 保存脱敏的 `debug.json`：端点不含查询参数；不保存 Cookie、请求头或原始网络响应正文。诊断日志中的 `ERR_ABORTED` 本身不能证明失败原因。

网页重定向到普通聊天时，CLI 会显式启用 Create image 并确认模型。提示词折叠时根据用户气泡前缀确认提交，不因会话 ID 延迟出现而重复发送。CLI 不自动改写或精简提示词。

```bash
node tools/gemini-image-web/gemini-image-web.mjs generate \
  --model Flash --headed --debug \
  --prompt 'Generate one image of a blue ceramic mug.' --session 调试 --name 杯子
node tools/gemini-image-web/gemini-image-web.mjs stop
```

同时启动两条 `generate` 命令即可并行，无需新增参数；多个命令在后台冷启动时会共用同一个启动过程，不会争抢 Chrome profile。默认容量为 6；超过容量立即报忙，不排队或自动重试；同名任务目录或相同制作落点同时运行会报冲突。`resume` 和 `doctor` 需要后台空闲。并行不增加账号额度，网页仍可能限流。

`stop` 停止接受新任务，等待所有后台任务结束后关闭 Chrome。`login` 和 `--headed` 运行前先 `stop`，避免同一 profile 冲突；不会关闭用户日常 Chrome。后台启动错误写入 profile 下的 `gemini-daemon.log`。修改 CLI 后先停止旧后台进程，再启动新版本。

## 并发容量与批次调度

一个后台进程持有 profile 锁，每个生成任务使用独立标签页；AsyncLocalStorage 将异步日志路由给对应调用者，图片下载绑定对应页面。不要用多个浏览器进程争用同一 profile。

```bash
node tools/gemini-image-web/gemini-image-web.mjs status
# 后台空闲时停止，再显式启动服务并指定容量：
node tools/gemini-image-web/gemini-image-web.mjs stop
node tools/gemini-image-web/gemini-image-web.mjs start --concurrency 3
```

`start` 显式启动服务；不运行 start 时，首次 generate 也会自动启动。重复 start 复用现有服务。`--concurrency 1–6` 仅在启动后台时设定，默认 6；已运行后台收到不同容量会明确报错，不静默忽略。6 是本地保护上限，不代表 Gemini 保证支持六路生成。更大吞吐量应先测量成功率、耗时和账号限流，再决定是否采用正式 API；不自动扩容。

`status` 返回 running、capacity、active、stopping、blocked；后台不存在时返回 `running: false`。并行接单、容量检查、输出冲突保护与停止排空都由此服务负责，调用方只需发出独立命令，不承担浏览器调度。多个 CLI、chat 共享同一容量；查询状态不能预留名额，满载仍会明确返回“后台忙”。批量调用需逐项保留结果（如 `Promise.allSettled`），不要把一个失败当成整批失败并重新提交。此能力仅属于本工具，不代表其他生图服务也支持并行。

- 输出目录、最终制作目标及符号链接别名冲突会在写入前拒绝；顺序执行同名任务仍按既有规则覆盖。
- 额度或登录错误会暂停后台接收新生成任务；已提交任务继续保存各自结果，不强行取消。诊断／恢复在后台空闲时仍可执行；处理额度／登录问题后 stop 并重启，不自动试探额度。
- 普通单任务失败释放名额，不自动重跑整批。忙、冲突、容量不匹配等接单前错误不会创建 manifest，因为尚未开始任务。
- 调用者断连不取消已提交任务。先查对应 manifest，确认完成或执行 resume；不要将 socket 报错当成服务端未提交。
- `stop` 排空活动任务期间不再接单；不是强制取消。浏览器崩溃可能同时影响多个任务，各任务尽力保存失败记录；进程被强杀时 manifest 可能停在 generating，需人工核对与恢复。

## 结果与下载

生成图、原提示词和 `manifest.json` 保存到 `tmp/gemini-image-web/<session>/<name>/`。`--out` 是额外的制作落点。

优先点击当前回复的 **Download full size image**，保留原文件字节。文件格式与尺寸按下载内容校验，SHA-256 写入 manifest。按钮存在但下载失败时明确报错，不能静默用预览代替。页面没有该入口时才保存预览，并同时输出警告、记录 `download.original: false` 和 `extraction: preview-canvas`。完整尺寸下载记录 `original: true`、`extraction: download-button`。

只有图片已校验、制作落点也保存成功，任务才标记 `completed`。成功时最后一行 stdout 为 manifest 路径；失败返回非零退出码，stderr 提供失败记录路径及网页原始回复。

## 错误与恢复

生成等待默认且最多 90 秒，`--timeout-ms` 只能缩短；这是提交后的等待预算，包含一次自动 Redo。浏览器启动最多 30 秒；原图下载另有最多 30 秒的事件等待。准备、上传、下载都有各自的限时，不使用无限等待。

| `response_kind` | 行为与处理 |
| --- | --- |
| `auth-error` | 登录无效，运行 `login`，不误报模型或按钮缺失 |
| `human-verification` | 检测到 Google 人机验证后立即暂停，不点击、不解答；运行 `stop`、`login`，由用户在打开的 Chrome 窗口完成人工验证并关闭，再用原 manifest 运行 `resume`。仅当 manifest 确认验证发生在提交前，resume 才会从新 Images 对话提交一次；提交阶段或生成期间触发验证时不会自动重发 |
| `quota` | 发现真实额度通知立即停止，不提交或自动重试；等待额度恢复 |
| `transient-error` | 原回复有 Redo 且预算足够时最多重试一次；仍失败就保留原文退出 |
| `generation-error` | 网页拒绝生成；不自动重试，不擅自推断具体政策或内部原因 |
| `text-only` | 已结束的回复只有文字；明确报错，不当成成功也不空等 90 秒 |
| `timeout` | 保存当前会话和回复；优先 `resume`，不要立即重复生成 |
| `download-error` | 图片已生成但原图下载或校验失败；用 `resume` 重试下载 |
| `save-error` | 本地图已保存，制作落点写入失败；修复路径后 `resume` 复用本地图片 |
| `browser-error` / `automation-error` | 浏览器关闭、启动失败或页面变化；检查错误与截图，按阶段处理 |

manifest 的 `phase` 区分 `browser`、`preparing`、`submitting`、`generating`、`downloading`、`saving`、`completed`；失败时保存 `status: interrupted`、错误类型、原始回复和可用的截图。用户提示词中的“Limit reached”等文字不会当成网页额度通知。

```bash
node tools/gemini-image-web/gemini-image-web.mjs resume \
  --manifest tmp/gemini-image-web/调试/杯子/manifest.json
# 修复制作落点时可覆盖原 --out：
node tools/gemini-image-web/gemini-image-web.mjs resume \
  --manifest tmp/gemini-image-web/调试/杯子/manifest.json \
  --out /absolute/path/fixed/杯子
```

人机验证处理：

```bash
node tools/gemini-image-web/gemini-image-web.mjs stop
node tools/gemini-image-web/gemini-image-web.mjs login
# 在打开的 Chrome 窗口手动完成人机验证，关闭窗口后：
node tools/gemini-image-web/gemini-image-web.mjs resume \
  --manifest tmp/gemini-image-web/统一限色人物设计/夜莺/manifest.json
```

普通会话恢复优先复用 SHA-256 校验通过的本地图片；否则只恢复原会话并确认用户提示词，不重新提交生成请求。重复恢复已完成的本地任务也安全。未获得会话 ID 时，仅在后台原页面仍存在且提示词匹配时允许恢复；页面关闭或服务端丢弃会话后会明确报无法恢复。前台超时会关闭窗口，因此后台更适合保留生成中的任务。普通会话恢复前不要运行 `stop`；人机验证则按上方流程先停止后台以释放 profile，再打开登录窗口供用户完成验证。

## 回归测试

```bash
node --test tools/gemini-image-web/*.test.mjs
```

测试使用本地浏览器页面与下载／socket 夹具，不调用 Gemini，不消耗生图额度。真实服务成功与否需另做前台／后台实际生图检查；服务端拒绝、额度与可用性不能由本地测试保证。
