# 第一阶段的三个交付物

1. **技术验证底座工程**：`Game/src/`、`platforms/`、`scripts/` 和 `tests/`。
   负责 Three/Pixi 共用 WebGL2、两个 Scheme 实例、原始 stdlib、布局、输入和生命周期验收。
2. **微信小游戏适配层**：[wechat](wechat/README.md)。独立目录包含宿主、Pixi 接入、编解码、构建修正及原始 vendor。
3. **TapTap 转换与启动适配层**：[taptap](taptap/README.md)。独立目录包含原版转换器、通用转换入口和早期错误报告入口生成器。

项目通过相对模块引用消费这两个适配包；也可在另一个 Vite 工程中用本地包路径安装。
包是 TypeScript 源码和构建工具，尚未发布到 npm。拷贝适配目录应连同 vendor、许可证和变更记录一起拷贝。

## 可复用的边界

相似技术栈的应用可复用宿主和转换过程，不需要 SSNoir 的内容、Scheme 计数器或游戏 ID。
这不是把任意网站 ZIP 自动变成小游戏的工具：应用需要独立 JS 入口，使用适配层提供的 Canvas、
输入和生命周期接口，并遵守小游戏资源与网络约束。真实 HTML 布局、浏览器完整 DOM、XML 资源、
远程资源请求和平台账号 SDK 不在当前实现范围内；碰到这些需求应明确增加运行契约和测试。

## 验收证据

构建、模拟宿主、微信开发者工具和 TapTap 真机分别记录，不互相替代。
2026-10-11 用户确认 TapTap iOS App 5.34.2 完整底座显示正常、点击正常。同日用户确认前后台恢复、计数保留、连续三次点击恰好增加三及关闭重新扫码正常。微信最终版与其他目标设备仍待验收，第一阶段尚未全部完成。
详见 [Phase 1 acceptance](../docs/PHASE1-ACCEPTANCE.md) 和 [TapTap acceptance](../docs/TAPTAP-ACCEPTANCE.md)。
