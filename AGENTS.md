# SSNoir — 工程指南（AGENTS）

本文件是所有 AI / 协作者的**统一入口**。下面先是文档导航,再是常驻工程规则。

## 文档导航

- **架构总览**：[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

- **Schemy 解释器(库开发)**：[schemy-master/AGENTS.md](schemy-master/AGENTS.md)

- **写 .scm 内容（世界观口径 + 引擎隐含行为 + 内容作者的判断）**：[skills/write-scheme/SKILL.md](skills/write-scheme/SKILL.md)
- **制作 3D 资产（参考图、低模、Blender 加工、Unity 命名契约）**：[skills/create-3d-assets/SKILL.md](skills/create-3d-assets/SKILL.md)
- **要不要验证 / 怎么验证**：[skills/verify/SKILL.md](skills/verify/SKILL.md)
- **Web Preview、TapTap 包内资源、TapTap + COS 三种构建方式**：[tools/BUILDING.md](tools/BUILDING.md)

文档分两种身份：
**「设计」文档**回答"游戏该是什么"，长命、随游戏演进持续回修；
**「方案 / 计划」文档**回答"这次怎么落地"，执行完移入 `docs/归档/`；
具体实现细节（某个动作的数值、某段文案）以 代码中的数值 为准。

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

**一套版面，到哪都一样——不许按平台做 UI 分支。**

游戏发布在手机上（TapTap 小游戏 / 横屏），所以版面就按手机设计，Editor、桌面构建、真机跑的是
同一套数字。代码里**不许出现 `if (是手机)` 这类判断去改尺寸、间距、字号、卡片大小或版面结构**。
一旦有了这种分支，Editor 里看到的就不是玩家看到的，预览失去意义，问题只能等打包到真机才暴露；
而且每加一个平台分支，要验证的组合就翻一倍。宁可显示器上看着像「一台放大的手机」，
也要保证在哪预览都等于最终效果。

推论：
- 虚拟画布高度恒定（`UIScale` 里那个唯一的设计高度），**能变的只有宽高比**。所以版面要对
  宽度有弹性（约束驱动、流式、按可用宽度分配），但不需要也不应该对「什么设备」有弹性。
- 宿主占位（小游戏右上角的胶囊按钮）这类「不是我们的地盘」也**恒定预留**，Editor 里照样留出来。
- 唯一允许分平台的是**输入能力**（鼠标有悬停、手指没有）。它只影响交互反馈，不影响任何元素
  画在哪、画多大。

- UI 以代码驱动为主
- UI 优先使用“约束驱动布局”, 减少像素硬编码布局, 纵向使用流式布局，横向使用分隔split, 根据panel的rect来计算和放置元素的位置
- 不要在ui文字中使用emoji
- 禁止用伪造输入状态的 hack 来禁用 UI，例如把鼠标坐标改成屏幕外、篡改事件坐标、吞掉不相关输入等。需要禁用交互时，必须使用明确的交互状态/上下文（如 `IsLocked`、`CanInteract`、`UiInteractionContext`），并让控件显式进入 disabled 视觉和行为状态。

# 测试验证
选择覆盖本次主要风险的最小充分验证；静态审阅足够时可以不运行命令。**要不要验证、验证到什么程度，以及往
`GameTester` 加测试之前**，看 [skills/verify/SKILL.md](skills/verify/SKILL.md)。

# Content 同步规则
- Unity 客户端中的 `StreamingAssets/Content`、`Resources/Content` 和 `Fonts` 资源是从项目根目录的 `Content` 目录同步复制过去的（详见 [ContentSyncEditor.cs](UnityClient/Assets/Editor/ContentSyncEditor.cs)）, Unity 客户端在加载或进入 Play 模式时会自动运行同步导入, 不需要agent操作这些文件。
- 如果出现不同步的情况，可提醒用户在 Unity 中运行顶部菜单 `SSNoir -> Sync Content Now` 进行手动同步。
