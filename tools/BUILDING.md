# SSNoir 构建方式

本项目保留三种构建方式。Agent 在构建、排查包体或修改发布工具前，先根据测试目标选择入口；不要把
Web Preview 当作 TapTap 发布包，也不要默认把开发工程中的资源直接移走。

## 1. Web Preview：局域网快速预览

```bash
./tools/web-preview/build-and-serve.sh
```

它生成标准浏览器 WebGL，并启动局域网 HTTP 服务，适合用手机快速检查画面、布局、触摸和基本性能。
它保留开发资源，不执行字体子集、视频清单和发布资源排除，也不具备 TapTap 容器、登录及小游戏 API
环境。详细参数见 [web-preview/README.md](web-preview/README.md)。

## 2. TapTap 包内资源：不依赖 COS 的真机测试包

```bash
./tools/taptap-build/build.sh
```

它执行完整发布处理，包括 staging 隔离、字体子集、发布排除清单和视频策略，但把首资源 Data 放进
TapTap 小游戏包。适合用户自己手机测试、离线排查，或判断问题是否来自 COS；它不是默认正式发布
方式，因为包更大，首包下载也更容易成为瓶颈。

## 3. TapTap + 腾讯云 COS：默认正式发布方式

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
