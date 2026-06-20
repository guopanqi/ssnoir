# SSNoir — Unity 客户端 UI 规则

本指导文档适用于 `UnityClient` 下的 IMGUI 界面编写。

- 视觉风格规范（Blueprint Noir 配色 / 字体 / 组件）见 [DESIGN.md](DESIGN.md)。
- 项目整体架构见 [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)。

## UI 绘制与缩放规则
- UI 边缘锚定必须使用 `UIScale.VW` / `UIScale.VH`，严禁硬编码虚拟分辨率（如 1920/1080）来计算右侧或下方位置。
- 定义 `GUIStyle` 的字号时，必须使用 `SF(baseSize, UIScale.Scale)` 包装，防止在分数缩放（如 0.75x）下文字模糊。
