# 手机 Web 预览

这是三种构建方式中的快速局域网预览，不是 TapTap 发布入口。三种方式的选择见
[../BUILDING.md](../BUILDING.md)。

在仓库根目录运行：

```bash
./tools/web-preview/build-and-serve.sh
```

构建完成后，终端会打印手机访问地址。电脑和手机需要在同一个局域网；保持命令运行，手机浏览器打开该地址即可。

- 指定端口：`./tools/web-preview/build-and-serve.sh --port 8080`
- 不重新构建，只启动上一次产物：`./tools/web-preview/build-and-serve.sh --serve-only`
- 停止服务器：在终端按 `Ctrl-C`

预览复用 TapTap 构建的独立 staging 和持久 `Library` 缓存，但不执行字体子集、视频清单或资源排除。产物固定在 `UnityClient/Build/WebPreview`，后续构建可以复用 Unity 的增量缓存。

这是标准浏览器 WebGL，用于快速检查手机上的画面、布局、触摸和基本性能；TapTap 登录、小游戏原生 API、小游戏容器性能仍需通过 TapTap 包验证。
