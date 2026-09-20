# SSNoir — Unity 客户端 UI 规则

本指导文档适用于 `UnityClient` 下的 IMGUI 界面编写。

- 美术 / 视觉风格规范（「墨与纸」Ink & Paper Noir 视觉语言）见 [DESIGN.md](DESIGN.md)。
- 项目整体架构见 [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)。

## UI 绘制与缩放规则
- UI 边缘锚定必须使用 `UIScale.VW` / `UIScale.VH`，严禁硬编码虚拟分辨率（如 1920/1080）来计算右侧或下方位置。
- 定义 `GUIStyle` 的字号时，必须使用 `IMGUIStyles.FontSize(baseSize)` 包装，防止在分数缩放
  （如 0.75x）下文字模糊。它是全项目唯一的字号入口：内部是**除以** `UIScale.Scale` 反算回虚拟
  空间，组件里不要写裸的 `fontSize = 12`，更不要自备一份「乘以 Scale」的换算——`GUI.matrix`
  已经缩过一次，再乘就是平方缩放。
- 字体资产的 `fontRenderingMode` 必须是 **Hinted Smooth（1）**。默认的 Smooth（0）不做像素栅格
  对齐，小字号中文会糊；项目又是 Linear 色彩空间，IMGUI 字体图集本就偏软，两者叠加最明显。
- 所有绘制、布局和可见性判断只读 `DisplayedSnapshot`；只有执行命令和决定下一步流程时才读
  `SceneManager` 的实时状态。动作结算到表现落地之间两者允许不同步，不要把未来状态提前画出来。
