# TapTap 精简构建

三种构建方式的选择、腾讯云 COS 项目配置和产物用途统一记录在
[../BUILDING.md](../BUILDING.md)。本目录提供两种 TapTap 构建入口。

构建脚本在 `.cache/taptap-build/staging` 维护独立 Unity 工作区，只在该副本中生成字体子集和排除资源。
正式项目的 `Assets` 与 `Library` 不会被构建流程修改。仓库内的两个本地 Unity Package 由 staging
只读引用，以避免 Package Manager 重建一整份 PackageCache。

```bash
# 首资源打在小游戏包内：用于不依赖 COS 的真机测试
./tools/taptap-build/build.sh
./tools/taptap-build/build.sh --clean-cache
./tools/taptap-build/build.sh --plan /absolute/path/to/release-plan.json

# 首资源上传腾讯云 COS：默认正式发布方式
./tools/taptap-build/build-and-upload-cos.sh \
  --bucket ssnoir-taptap-1466784385 \
  --region ap-guangzhou \
  --cdn-url https://ssnoir-taptap-1466784385.cos.ap-guangzhou.myqcloud.com
```

`release-plan.json` 是 Agent 审查当前资源后维护的发布记录：`cutsceneVideos.keep` 明确列出场景需要的
视频，`exclude` 记录其他确认无用的资源；脚本不会自行推断资源是否未使用。
当前过审构建会验证保留清单中的源视频存在，但不会把视频写入两个 zip；构建完成后还会断言产物中
不存在 `StreamingAssets/Cutscenes/`。正式发布切换到 HTTPS 视频地址后，这份清单继续作为远程视频
发布范围使用。
每次构建都会把计划、字体报告、日志和最终体积报告保存到
`UnityClient/Build/TapTapRelease/<时间戳>/`。两个 zip 验证成功后，脚本会删除可重建的 `webgl/`
和 `minigame/` 中间目录。`build-report.json` 同时记录 staging 准备、字体子集、Unity batch、
Unity Player 内部步骤、产物清理与哈希校验耗时，用于区分增量构建和完整 WebAssembly 重链接。

环境要求：

- macOS APFS
- Unity 2022.3.62f3c1；若 Unity 不在默认路径，通过 `UNITY_PATH` 指定
- HarfBuzz 命令行工具（`hb-subset`、`hb-info`）
- 至少 2 GiB 可用磁盘空间，用于 WebGL/IL2CPP 中间产物
