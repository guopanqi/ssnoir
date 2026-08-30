# 手机 Web 预览

这是三种构建方式中的快速局域网预览，不是 TapTap 发布入口。三种方式的选择见
[../../BUILDING.md](../../BUILDING.md)。

在仓库根目录运行：

```bash
./tools/build/preview/build-and-serve.sh
```

构建完成后，终端会打印手机访问地址。电脑和手机需要在同一个局域网；保持命令运行，手机浏览器打开该地址即可。

- 指定端口：`./tools/build/preview/build-and-serve.sh --port 8080`
- 不重新构建，只启动上一次产物：`./tools/build/preview/build-and-serve.sh --serve-only`
- 模拟无视频评审包：`./tools/build/preview/build-and-serve.sh --review-no-video`
- 停止服务器：在终端按 `Ctrl-C`

预览使用自己的 staging 和持久 `Library`，不会与 Web Release 或 TapTap 共用可变缓存。字体子集、资源
排除和视频清单走公共构建层；平台设置仍以快速浏览器预览为目标。产物固定在
`UnityClient/Build/WebPreview`。

这是标准浏览器 WebGL，用于快速检查手机上的画面、布局、触摸和基本性能；TapTap 登录、小游戏原生 API、小游戏容器性能仍需通过 TapTap 包验证。
