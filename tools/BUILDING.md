# SSNoir 构建方式

本项目对外只有三种构建工作流。TapTap Release 另有“包内 Data”诊断模式和“COS Data”正式模式，
它们不是两套资源逻辑。Agent 在构建、排查包体或修改发布工具前，先根据测试目标选择入口；不要把
Web Preview 当作 TapTap 发布包，也不要默认把开发工程中的资源直接移走。

三个入口都只从 `UnityClient/` 建立隔离 staging；Scheme、字体和视频已经属于 Unity 工程，不再从仓库
根目录同步第二份 `Content`。三个 staging 彼此独立，避免平台设置和导入缓存相互污染。公共资源层统一
执行字体子集、[resource-plan.json](build/resource-plan.json) 排除项和未使用视频裁剪；资源修改只发生在
staging，主工程始终保持完整。

视频交付只有三种明确模式：`local` 把清单内视频放进 Web 包；`none` 即 `review-no-video`，即使被引用
也排除；`remote` 把清单内视频发布到远程资源目录，并在运行时配置中写入 HTTPS 基址。

## 清理构建产物和缓存

统一使用 `tools/build/clean.sh`，只会删除明确的生成目录，不会碰 `.cache/saves`、Unity 工程资源或 Git
历史：

```bash
# 只清理 UnityClient/Build 下的预览、Release 和 COS 本地产物
./tools/build/clean.sh --builds

# 只清理构建 staging 缓存（下次构建会重新导入，首次构建较慢）
./tools/build/clean.sh --cache

# 两者都清理
./tools/build/clean.sh --all
```

Web Release 和 TapTap Release 成功后会自动删除同一输出目录下旧的时间戳版本，所以通常不需要为了释放
旧版本再手动清理；只有需要彻底回收构建目录或缓存空间时才使用上面的命令。

## 1. Web Preview：局域网快速预览

```bash
./tools/web-preview/build-and-serve.sh
```

它生成标准浏览器 WebGL，并启动局域网 HTTP 服务，适合用手机快速检查画面、布局、触摸和基本性能。
它使用公共资源层，但采用 `local` 视频模式；平台参数则以构建速度为先，不具备 TapTap 容器、登录及
小游戏 API 环境。需要模拟无视频评审包时加 `--review-no-video`。详细参数见
[web-preview/README.md](web-preview/README.md)。

## 2. Web Release：标准浏览器正式包

```bash
./tools/web-release/build.sh
```

需要在构建成功后立即上传到 itch.io 时，直接加 `--itch`：

```bash
./tools/web-release/build.sh --itch
```

它构建可部署到普通静态站点的浏览器 WebGL 正式包：关闭 Development Build 和调试符号，启用 Brotli
压缩与浏览器缓存。产物位于 `UnityClient/Build/WebRelease/<时间戳>/`，同级生成可交付的离线评审 zip。

此入口默认采用 `local` 视频模式，因此保留被场景引用的本地过场视频；字体、明确无用的 Resources 和
未使用视频仍由公共资源层统一处理。它不启动局域网预览服务；部署时，静态服务器必须为 `.br` 资源
返回正确的 `Content-Encoding: br` 响应头。

生成不含任何视频的极简评审包：

```bash
./tools/web-release/build.sh --review-no-video
```

每次完整构建成功后，`WebRelease/` 只保留本次时间戳目录、离线包、itch 包和日志；旧版本会自动删除。

本机或局域网启动该包时，传入本次构建的输出目录：

```bash
./tools/web-release/serve.sh \
  --directory UnityClient/Build/WebRelease/<时间戳>
```

离线评审 zip 解压后的目录只露出三样东西，游戏本体和三份本地服务器（Mac Apple Silicon /
Intel、Windows x64）都收在 `game/` 里：

```
SSNoir-WebDemo/
  START-Mac.command
  START-Windows.bat
  README.txt
  game/
```

评委双击对应平台的 START 脚本即可，无需额外安装 Python、Node 或 Unity。启动器模板在
`tools/web-release/offline-launcher/`。

## Cloudflare Worker + R2：无域名公开链接

`tools/web-release/cloudflare/` 用私有 R2 保存 Web Release，由 Worker 流式返回文件，因此不受
Cloudflare Pages 的 25 MiB 单文件限制。首次部署需要在浏览器完成 `wrangler login`；之后执行：

```bash
./tools/web-release/cloudflare/deploy.sh \
  --release-dir UnityClient/Build/WebRelease/<时间戳>
```

脚本会创建 `ssnoir-web-demo` bucket（若不存在）、上传构建目录并部署 `ssnoir-web-demo` Worker；最终链接由
Wrangler 输出，形如 `https://ssnoir-web-demo.<你的子域>.workers.dev/`。不要公开 R2 bucket。

首次使用还必须在 Cloudflare Dashboard 的 **R2 Object Storage** 页面启用 R2；这可能要求账户确认或计费
设置。启用后重新运行上面的命令即可，部署脚本会处理 bucket 创建和上传。

## itch.io：推荐的评委在线 Demo

每次 Web Release 会额外生成 `UnityClient/Build/WebRelease/SSNoir-ItchWeb-<时间戳>.zip`。它只有 WebGL
文件，且 `index.html` 位于 zip 根目录，符合 itch HTML5 上传要求；不要上传离线评审包。

当前 itch 页面使用 `web` 渠道，可通过 butler 更新：

```bash
butler push UnityClient/Build/WebRelease/<时间戳> guopanqi/noir:web
```

通常无需单独执行这条命令，推荐使用上面的 `./tools/web-release/build.sh --itch` 一次完成构建和上传。
若要临时推送到其他页面或渠道，可设置 `ITCH_TARGET`：

```bash
ITCH_TARGET=你的账号/你的游戏:测试渠道 ./tools/web-release/build.sh --itch
```

首次在 itch 后台上传或更新后，确认该渠道的文件已标为 **This file will be played in the browser**；之后同一渠道
的 butler 更新会保留该页面与渠道配置。

## 3. TapTap Release：TapTap 正式包

默认正式发布通过 COS 承载首资源 Data：

```bash
./tools/taptap-build/build-and-upload-cos.sh \
  --bucket ssnoir-taptap-1466784385 \
  --region ap-guangzhou \
  --cdn-url https://ssnoir-taptap-1466784385.cos.ap-guangzhou.myqcloud.com
```

它执行完整发布处理，把首资源 Data 和清单内过场视频上传到 COS，并逐项检查匿名 HTTPS、CORS、
Content-Type 和文件大小。TapTap 包中只保存远程基址；Data 由小游戏运行时缓存，视频由 `VideoPlayer`
直接读取 HTTPS URL，都不会写回安装包里的 `StreamingAssets`。

### 包内 Data：同一流程的诊断模式

```bash
./tools/taptap-build/build.sh
```

它执行同一套公共资源处理，但把首资源 Data 放进 TapTap 小游戏包并采用 `none` 视频模式。适合用户
自己手机测试、离线排查，或判断问题是否来自 COS；它不是默认正式发布方式，因为包更大且不验证
远程视频链路。

当前默认对象前缀是 `ssnoir/taptap`；可通过 `--prefix` 为
版本指定其他前缀。

COSCLI 凭据只保存在用户环境（默认 `~/.cos.yaml`），不得写入仓库、发布计划或构建日志。可执行文件
默认从 `PATH` 或 `~/.local/bin/coscli` 查找，也可通过 `COSCLI_PATH` 指定。这个 COS 地址目前是 COS
源站域名；项目口语中可能称它为 CDN，但它不等于另行开通的腾讯云 CDN 产品。

## 共同发布契约

所有构建都读取 [build/resource-plan.json](build/resource-plan.json)。该文件由 Agent 审查项目后维护：
`exclude` 只记录已经确认可排除的资源，`cutsceneVideos.keep` 是本地 Web 包和 TapTap 远程视频共同的
唯一发布范围。脚本不会靠文件名猜测 Resources 是否未使用。

产物位于 `UnityClient/Build/TapTapRelease/<时间戳>/`：

- `game.zip`：普通 TapTap 包。
- `game_wasm_split.zip`：用于 TapTap 后台 WASM 函数分包的包；需要分包时上传这一份并在后台完成
  函数采集、生成与提交。
- `build-report.json`、`font-report.json`、`resource-plan.json`、`build.log`：本次构建证据。

TapTap Release 在报告生成成功后会自动清理 `TapTapRelease/` 下旧的时间戳目录，只保留本次成功构建。

COS 承载 Data 和 `Cutscenes/*.mp4`，不承载 TapTap WASM 代码包。WASM 下载慢时，应检查 TapTap 的
WASM 分包流程，而不是调整 COS 流量包。

具体发布脚本参数和环境要求见 [taptap-build/README.md](taptap-build/README.md)。
