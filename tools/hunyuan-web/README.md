# Hunyuan Studio Web

用 Playwright 操作 Hunyuan 3D Studio 的免费网页额度。Geo 与 Poly 是两个明确阶段；Poly 每次上传本地高模，不依赖网页资产库排序。

## 安装与登录

```bash
cd tools/hunyuan-web
npm install
node hunyuan-web.mjs login
```

登录只需在专用 Chrome profile 首次完成。自动化不会读取日常 Chrome profile。

## 检查网页兼容性

```bash
node hunyuan-web.mjs doctor
```

`doctor` 不提交任务、不消耗生成次数，只确认登录状态、关键控件，以及「今日剩余生成次数」。额度为 0 时仍会报告页面可达，但会标明今日不可用。

## 生成

```bash
node tools/hunyuan-web/hunyuan-web.mjs geo \
  --image /absolute/path/reference.png \
  --session 电影院 \
  --faces 50k \
  --out city-box/prefabs/review/电影院/props/sign-geo.fbx

node tools/hunyuan-web/hunyuan-web.mjs poly \
  --source city-box/prefabs/review/电影院/props/sign-geo.fbx \
  --session 电影院 \
  --level low \
  --topology quad \
  --out city-box/prefabs/review/电影院/props/sign-low.fbx
```

`--out` 把结果另存到你指定的路径（扩展名须与 `--format` 一致，默认 fbx），不用再去 manifest 里找服务端的随机文件名。FBX 下载不依赖浏览器的 `download` 事件：工具点选已锁定的预览，捕获查看器实际加载的 COS `.fbx`，校验预览/模型时间戳与 FBX 文件头后直接落盘。STL/USDZ 仍走网页下载菜单。成功时 stdout 先打印一段摘要（文件、sha256、提交前额度、耗时），最后一行是 manifest 路径；生成等待期间 stderr 每 30 秒打印一次心跳，下载阶段每 15 秒打印一次，静默即异常。

每次运行在 `tmp/hunyuan-web/<session>/<stage>-<设置>/manifest.json`（如 `geo-50k`、`poly-low-quad`）保存输入哈希、设置、状态和下载哈希。Low 结果不合格时，保持 `--source` 与 `--session` 不变，把 `--level` 改为 `medium`，会落到 `poly-medium-quad`，不覆盖 low 的记录。下载在独立浏览器里按锁定的预览 URL 进行（和 batch 一样），下载失败时 manifest 状态是 `ready_to_download`（任务已完成、额度已扣）。中断或下载失败后运行下面的命令继续等待并下载；恢复不会重新提交任务：

```bash
node tools/hunyuan-web/hunyuan-web.mjs resume \
  --manifest tmp/hunyuan-web/电影院/poly-low-quad/manifest.json
```

## 上传被拒

站点会在页面里直接写拒绝原因，CLI 抓到就报（如 `站点拒绝了上传：不支持上传灰度图`）。灰度判定按像素（R=G=B），RGB 编码的纯灰图一样被拒；加一点色偏再传即可。

## 同阶段批量生成

`batch` 在一个页面里依次上传并提交多个任务，随后同时等待服务端生成。所有任务完成后关闭监控浏览器，再为每个结果使用独立浏览器逐个下载；单项失败不会连带中断其他任务。

下载阶段独立限时 120 秒。超时不会重新提交任务，manifest 会保持 `ready_to_download`；检查网络或登录状态后直接运行 `resume --manifest ...` 即可。

```json
[
  {"stage":"geo", "image":"/abs/urn.jpg", "session":"urn", "faces":"50k", "out":"/abs/urn-geo.fbx"},
  {"stage":"geo", "image":"/abs/booth.jpg", "session":"booth", "faces":"50k", "out":"/abs/booth-geo.fbx"}
]
```

```bash
node tools/hunyuan-web/hunyuan-web.mjs batch --jobs /abs/jobs.json
```

一批只能包含 Geo 或 Poly 中的一个阶段。工具会在提交任何任务之前校验全部输入、重复 session、重复输出、既有 manifest 和剩余额度；不允许覆盖旧 manifest。Geo batch 完成后应先做 Blender/人工质量门，再为通过的模型建立 Poly batch，不自动消耗下一阶段额度。

同一浏览器 profile 同时只能由一个 `hunyuan-web` 进程使用，第二个进程会直接报错并指出占用者 pid。多件资产应放进同一个 batch，不要并行启动多个 CLI。默认无头运行；诊断时可加 `--headed`。网页改版、登录过期或验证码会明确中断，不会盲目继续点击。
