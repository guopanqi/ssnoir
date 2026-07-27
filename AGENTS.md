# SSNoir — 工程指南（AGENTS）

本文件是所有 AI / 协作者的**统一入口**。下面先是文档导航,再是常驻工程规则。

## 文档导航

- **架构总览(先读这个理解项目)**：[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- **城市生活设计（内容与机制源头）**：[docs/城市生活设计.md](docs/城市生活设计.md)
- **故事设计总纲（章节写作原则）**：[docs/故事设计总纲.md](docs/故事设计总纲.md)
- **第一章·三封信（真正第一章的故事与玩法设计）**：[docs/第一章·三封信.md](docs/第一章·三封信.md)
- **demo短篇·夜莺故事设计（demo 试验故事，非真主线）**：[docs/demo短篇·夜莺故事设计.md](docs/demo短篇·夜莺故事设计.md)
- **写 .scm 内容（世界观口径 + 引擎隐含行为 + 内容作者的判断）**：[skills/write-scheme/SKILL.md](skills/write-scheme/SKILL.md)
- **要不要验证 / 怎么验证（含 GameTester 红线）**：[skills/verify/SKILL.md](skills/verify/SKILL.md)
- **改 Schemy 解释器本身(库开发)**：[schemy-master/AGENTS.md](schemy-master/AGENTS.md)
- **Unity 客户端 UI 规则**：[UnityClient/AGENTS.md](UnityClient/AGENTS.md)
- **Terminal 客户端视觉设计规范**：[TerminalApp/DESIGN.md](TerminalApp/DESIGN.md)
- **已知限制 / 待办**：[TODO.md](TODO.md)

文档分三种身份：**「设计」文档**回答"游戏该是什么"，长命、随游戏演进持续回修；
**「方案 / 计划」文档**回答"这次怎么落地"，执行完移入 `docs/archive/`；
**[`docs/手稿/`](docs/手稿/)** 是用户手写的故事源头（剧本、人物表），agent 默认只读，但用户明确授权时可以修改
具体实现细节（某个动作的数值、某段文案）以 demo 内容脚本为准，不复制进设计文档。

---

# 我的工程风格偏好

请你以清晰的界限，简洁的结构，但不过度设计的最终目标来计划

- 游戏内容设计以数据驱动为主, 便于llm阅读和修改游戏数据
- 在进行工程上的设计或重构时, 保持 清晰的文件边界, 不进行太多不必要的过度设计
- 在执行时, 不要进行打补丁性的兼容, 按照清晰良好的组织结构来实现
- 减少混乱的兼容或者对错误的容忍, 对于可能存在的游戏逻辑错误, 或者数据配置错误, 直接assert中断游戏, 打印错误, 最大程度确保游戏的健壮性, 如果有一些建议可以向用户提问

# 惯例
在定义角色物品名称时，如果我们是用 dictionary 或字符串来表示 ID 或名称，不是代码中的符号, 一般可以直接用中文来表达, 会更加方便用户来编写脚本

**世界观口径**：内容用中文书写，但世界是**上世纪美国 / 黑色电影**，不是中国。写任何玩家可见文字前，
先读 [skills/write-scheme/SKILL.md](skills/write-scheme/SKILL.md) §一（判断标准与穿帮词，唯一权威）。

**回答语言**：使用中文回答；专有名词等更适合英文的内容保持英文。

# UI-rules
- UI 以代码驱动为主
- UI 优先使用“约束驱动布局”, 减少像素硬编码布局, 纵向使用流式布局，横向使用分隔split, 根据panel的rect来计算和放置元素的位置
- 不要在ui文字中使用emoji
- 禁止用伪造输入状态的 hack 来禁用 UI，例如把鼠标坐标改成屏幕外、篡改事件坐标、吞掉不相关输入等。需要禁用交互时，必须使用明确的交互状态/上下文（如 `IsLocked`、`CanInteract`、`UiInteractionContext`），并让控件显式进入 disabled 视觉和行为状态。

# 测试验证
选择覆盖本次主要风险的最小充分验证；静态审阅足够时可以不运行命令。**要不要验证、验证到什么程度，以及往
`GameTester` 加测试之前**，看 [skills/verify/SKILL.md](skills/verify/SKILL.md)（唯一权威）。

# Content 同步规则
- Unity 客户端中的 `StreamingAssets/Content`、`Resources/Content` 和 `Fonts` 资源是从项目根目录的 `Content` 目录同步复制过去的（详见 [ContentSyncEditor.cs](UnityClient/Assets/Editor/ContentSyncEditor.cs)）, Unity 客户端在加载或进入 Play 模式时会自动运行同步导入, 不需要agent操作这些文件。
- 如果出现不同步的情况，可提醒用户在 Unity 中运行顶部菜单 `SSNoir -> Sync Content Now` 进行手动同步。
