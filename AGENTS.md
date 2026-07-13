# SSNoir — 工程指南（AGENTS）

本文件是所有 AI / 协作者的**统一入口**。下面先是文档导航,再是常驻工程规则。

## 文档导航

- **架构总览(先读这个理解项目)**：[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- **城市生活设计（内容与机制源头）**：[docs/城市生活设计.md](docs/城市生活设计.md)
- **夜莺主线设计（故事源头）**：[docs/夜莺主线设计.md](docs/夜莺主线设计.md)
- **Scheme 内容编写指南(写 .scm:DSL + 约定)**：[SCHEMY.md](SCHEMY.md)
- **改 Schemy 解释器本身(库开发)**：[schemy-master/AGENTS.md](schemy-master/AGENTS.md)
- **Unity 客户端 UI 规则**：[UnityClient/AGENTS.md](UnityClient/AGENTS.md)
- **Terminal 客户端视觉设计规范**：[TerminalApp/DESIGN.md](TerminalApp/DESIGN.md)
- **已知限制 / 待办**：[TODO.md](TODO.md)

---

# 我的工程风格偏好

请你以清晰的界限，简洁的结构，但不过度设计的最终目标来计划

- 游戏内容设计以数据驱动为主, 便于llm阅读和修改游戏数据
- 在进行工程上的设计或重构时, 保持 清晰的文件边界, 不进行太多不必要的过度设计
- 在执行时, 不要进行打补丁性的兼容, 按照清晰良好的组织结构来实现
- 减少混乱的兼容或者对错误的容忍, 对于可能存在的游戏逻辑错误, 或者数据配置错误, 直接assert中断游戏, 打印错误, 最大程度确保游戏的健壮性, 如果有一些建议可以向用户提问
- 

# 惯例
在定义角色物品名称时，如果我们是用 dictionary 或字符串来表示 ID 或名称，不是代码中的符号, 一般可以直接用中文来表达, 会更加方便用户来编写脚本

# UI-rules
- UI 以代码驱动为主
- UI 优先使用“约束驱动布局”, 减少像素硬编码布局, 纵向使用流式布局，横向使用分隔split, 根据panel的rect来计算和放置元素的位置
- 不要在ui文字中使用emoji
- 禁止用伪造输入状态的 hack 来禁用 UI，例如把鼠标坐标改成屏幕外、篡改事件坐标、吞掉不相关输入等。需要禁用交互时，必须使用明确的交互状态/上下文（如 `IsLocked`、`CanInteract`、`UiInteractionContext`），并让控件显式进入 disabled 视觉和行为状态。

# 测试验证
避免不必要的频繁测试和构建。只有在必要时（例如：进行大量代码修改、完成某一模块的重构、或者需要排查并验证是否存在特定错误时）才进行检查。
- 语法检查（仅在可能遗留括号缺失等低级语法错误或大范围重构时）
- 全量内容校验与最小状态模拟（仅在需要验证复杂边界逻辑行为或修复疑难问题时）
- 不要默认向 `GameTester` 添加测试。它只用于验证稳定的底层引擎 / DSL 契约、高风险且容易静默损坏的基础功能，或用户明确要求覆盖的行为。
- 不要把频繁变化的剧情流程、内容节点名称、数值平衡或完整游玩弧线固化进 `GameTester`；这类测试难以跟随内容迭代，会拖慢修改速度。

# Content 同步规则
- Unity 客户端中的 `StreamingAssets/Content`、`Resources/Content` 和 `Fonts` 资源是从项目根目录的 `Content` 目录同步复制过去的（详见 [ContentSyncEditor.cs](UnityClient/Assets/Editor/ContentSyncEditor.cs)）, Unity 客户端在加载或进入 Play 模式时会自动运行同步导入, 不需要agent操作这些文件。
- 如果出现不同步的情况，可提醒用户在 Unity 中运行顶部菜单 `SSNoir -> Sync Content Now` 进行手动同步。
