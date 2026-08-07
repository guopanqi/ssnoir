# SSNoir — Terminal 客户端 UI 规则

本指导文档适用于 `TerminalApp` 下的 Raylib 界面编写。

- 美术 / 视觉风格规范（颜色语言 / 卡片结构 / 交互状态）见 [DESIGN.md](DESIGN.md)。
- 项目整体架构见 [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)。
- 通用 UI 规则（约束驱动布局、显式交互状态、禁 emoji 等）见 [../AGENTS.md](../AGENTS.md)「UI-rules」。

## 通用规则

- 公共颜色集中取自 `src/Rendering/TerminalPalette.cs`，各 Widget 不得重复发明近似颜色。
- 布局采用约束驱动流式布局，后一个区域根据前一个区域的实际底边定位，不各自使用互不相关的绝对坐标。
- 禁用交互必须走明确的交互状态（`UiInteractionContext`），禁止伪造输入状态的 hack。
- 视觉修改完成后至少检查窄内容、长说明、多标签、禁用原因、多项结算影响和附件展开这几类状态。
