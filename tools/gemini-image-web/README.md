# Gemini Image Web CLI

通过已登录的 `gemini.google.com/images` 网页生成参考图，并把生成图、提示词、输入哈希和对话地址保存到本地。它是网页适配器，不依赖 Gemini API key。`manifest.json` 的 `download.extraction` 记录提取方式；`canvas-png` 表示页面图片按原像素尺寸重新编码，其他提取方式优先保留网页提供的源文件字节。

首次使用先登录：

```bash
cd tools/gemini-image-web
npm install
node gemini-image-web.mjs login
```

登录后可检查网页是否仍兼容：

```bash
node tools/gemini-image-web/gemini-image-web.mjs doctor
```

生成一张图，直接落到制作目录：

```bash
node tools/gemini-image-web/gemini-image-web.mjs generate \
  --prompt-file city-box/prefabs/review/电影院/01-正门-prompt.txt \
  --out city-box/prefabs/review/电影院/01-正门
```

`--out` 说明了这张图是谁、放哪：session 取上一级目录名（`电影院`），name 取文件名（`01-正门`），扩展名由网页实际返回的格式决定（写了 `.jpg` 结果是 PNG 也会被纠正为 `.png`，不报错）。`--out` 给一个目录（以 `/` 结尾）时文件名用 name。不给 `--out` 就必须给 `--session`（和可选的 `--name`）。`--prompt-file -` 从 stdin 读。

带参考图时重复传 `--reference`：

```bash
node tools/gemini-image-web/gemini-image-web.mjs generate \
  --out city-box/prefabs/review/电影院/02-正门 \
  --prompt-file city-box/prefabs/review/电影院/02-正门-prompt.txt \
  --reference /absolute/path/to/front.jpg \
  --reference /absolute/path/to/style.jpg
```

生成图、`prompt.txt` 和 `manifest.json` 始终保留在 `tmp/gemini-image-web/<session>/<name>/`（`--out` 只是多存一份）。同一浏览器 profile 不能由两个进程同时打开。上传参考图首次触发 Google 的内容确认时，用 `--headed` 运行并手动同意一次。

普通 `generate` 首次调用会在后台启动 Chrome，后续调用复用同一个 profile 和页面；任务顺序执行，后台进程空闲 10 分钟后自动退出。首次仍需支付浏览器启动时间，连续出图可省掉每张图的冷启动。运行 `node tools/gemini-image-web/gemini-image-web.mjs stop` 可立即关闭后台浏览器。`--headed` 和 `doctor` 仍单次启动浏览器；运行它们之前先 `stop`，避免争用同一 profile。

生成已经在 Gemini 历史中持久化、但下载或本地保存阶段中断时，可以恢复对应对话：

```bash
node tools/gemini-image-web/gemini-image-web.mjs resume \
  --manifest tmp/gemini-image-web/电影院/正门-a/manifest.json
```

后台浏览器仍在运行时，`resume` 优先使用尚未关闭的原会话页面；下载失败后应先恢复，再提交下一张图或运行 `stop`。后台浏览器已退出时才重新打开会话；Gemini 不一定会持久化刚生成的临时会话，因此这种恢复不能保证成功。

图片由 Images 页面当前提供的 Nano Banana 2 生成。CLI 不选择输入框旁的 Flash/Pro：那是 Gemini 对话模型，不是图片模型。网页改版或登录失效时先运行 `doctor --headed`，失败目录中的 manifest 会保留错误和截图路径。

等待生成默认且最多 90 秒（`--timeout-ms` 只能缩短）：正常生成通常在 30 秒内完成，90 秒仍无结果就中止。这个上限覆盖提交后的路由与生成等待，不会在两段等待中重复计算。Gemini 有时会在浏览器关闭时取消仍在生成的临时会话；这种会话之后会从 `/app/<id>` 重定向到 `/app`，无法 `resume`，只能重新生成。`resume` 会先验证目标会话和 manifest 中的用户提示词，拒绝在普通首页或错误会话中盲等、下载。点击 Send 后只以匹配的用户回合或带 ID 的会话 URL 作为提交成功判据；若网页只从 `/images` 切到普通 `/app`，CLI 会恢复 Images 模式、确认参考图仍在、重填并重试，最多三次。参考图一旦丢失会立即报错，不会静默降级成无参考生成。
