# SSNoir 构建方式

本项目保留四种构建方式。Agent 在构建、排查包体或修改发布工具前，先根据测试目标选择入口；不要把
Web Preview 当作 TapTap 发布包，也不要默认把开发工程中的资源直接移走。

## 1. Web Preview：局域网快速预览

```bash
./tools/web-preview/build-and-serve.sh
```

它生成标准浏览器 WebGL，并启动局域网 HTTP 服务，适合用手机快速检查画面、布局、触摸和基本性能。
它保留开发资源，不执行字体子集、视频清单和发布资源排除，也不具备 TapTap 容器、登录及小游戏 API
环境。详细参数见 [web-preview/README.md](web-preview/README.md)。

## 2. Web Release：标准浏览器正式包

```bash
./tools/web-release/build.sh
```

它构建可部署到普通静态站点的浏览器 WebGL 正式包：关闭 Development Build 和调试符号，启用 Brotli
压缩与浏览器缓存。产物位于 `UnityClient/Build/WebRelease/<时间戳>/`，同级生成可交付的离线评审 zip。

此入口**保留全部资源和本地过场视频**，不执行 TapTap 发布构建的字体裁剪、发布排除或
`StreamingAssets` 删除，因此功能与开发工程一致。它也不启动局域网预览服务；部署时，静态服务器必须为
`.br` 资源返回正确的 `Content-Encoding: br` 响应头。

本机或局域网启动该包时，传入本次构建的输出目录：

```bash
./tools/web-release/serve.sh \
  --directory UnityClient/Build/WebRelease/<时间戳>
```

离线评审 zip 中同时带有 Mac（Apple Silicon / Intel）和 Windows x64 的本地服务器；评委解压后双击
`Start SSNoir Demo.command` 或 `Start SSNoir Demo.bat`，无需额外安装 Python、Node 或 Unity。

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

首次在 itch 后台上传或更新后，确认该渠道的文件已标为 **This file will be played in the browser**；之后同一渠道
的 butler 更新会保留该页面与渠道配置。

## 3. TapTap 包内资源：不依赖 COS 的真机测试包

```bash
./tools/taptap-build/build.sh
```

它执行完整发布处理，包括 staging 隔离、字体子集、发布排除清单和视频策略，但把首资源 Data 放进
TapTap 小游戏包。适合用户自己手机测试、离线排查，或判断问题是否来自 COS；它不是默认正式发布
方式，因为包更大，首包下载也更容易成为瓶颈。

## 4. TapTap + 腾讯云 COS：默认正式发布方式

```bash
./tools/taptap-build/build-and-upload-cos.sh \
  --bucket ssnoir-taptap-1466784385 \
  --region ap-guangzhou \
  --cdn-url https://ssnoir-taptap-1466784385.cos.ap-guangzhou.myqcloud.com
```

它先执行同一套完整发布处理，再把首资源 Data 上传到 COS，并检查匿名 HTTPS、CORS、文件大小和下载
速度。TapTap 包中保存对应的远程 URL。当前默认对象前缀是 `ssnoir/taptap`；可通过 `--prefix` 为
版本指定其他前缀。

COSCLI 凭据只保存在用户环境（默认 `~/.cos.yaml`），不得写入仓库、发布计划或构建日志。可执行文件
默认从 `PATH` 或 `~/.local/bin/coscli` 查找，也可通过 `COSCLI_PATH` 指定。这个 COS 地址目前是 COS
源站域名；项目口语中可能称它为 CDN，但它不等于另行开通的腾讯云 CDN 产品。

## 共同发布契约

两种 TapTap 构建都读取 [taptap-build/release-plan.json](taptap-build/release-plan.json)。该文件由 Agent
审查项目后维护：字体在 staging 中生成子集；`exclude` 只记录已经确认可排除的资源；过场视频目前不
写入小游戏包，未来正式启用 HTTPS 视频时，`cutsceneVideos.keep` 作为远程视频发布范围。

产物位于 `UnityClient/Build/TapTapRelease/<时间戳>/`：

- `game.zip`：普通 TapTap 包。
- `game_wasm_split.zip`：用于 TapTap 后台 WASM 函数分包的包；需要分包时上传这一份并在后台完成
  函数采集、生成与提交。
- `build-report.json`、`font-report.json`、`release-plan.json`、`build.log`：本次构建证据。

COS 只承载 Data 和未来的远程资源，不承载 TapTap WASM 代码包。WASM 下载慢时，应检查 TapTap 的
WASM 分包流程，而不是调整 COS 流量包。

具体发布脚本参数和环境要求见 [taptap-build/README.md](taptap-build/README.md)。
