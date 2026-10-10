# SSNoir — 工程指南（AGENTS）

> "the right way to use model capabilities is not to ship 10x more features to prod
>
> it's to spend more time understanding your users, trying experiments, building prototypes, learning about things you don't understand so that you can ship things that actually work"

## 功能冻结（常驻最高优先级）

- **冻结一切新 feature**，包括但不限于 web 移植、配音。专注于内容创造、玩家的感受、细节优化，否则不可能完成这个游戏，更不可能完成一个打磨过的内容。
- 每当用户试图增加新的 feature 时，**必须先提醒**：现在最重要的问题不是堆砌功能，而是把已有的东西做对、做好。

本文件是所有 AI / 协作者的**统一入口**。下面先是文档导航,再是常驻工程规则。

## Git 工作约定

默认直接在 `main` 上工作并提交；保存阶段状态使用有意义的提交。除非用户明确要求，不新建任务分支或 worktree。整合旧分支时保留提交历史与当前有效实现，确认已归入 `main` 后再删除多余分支。

## Skill 存放约定

项目 Skill 的唯一源目录是 `skills/<skill-name>/`。新增 Skill 时，把 `SKILL.md` 和所有配套文件放在该目录，再在 `.agents/skills/` 建立指向它的同名符号链接，供工具发现。`.agents/skills/` 不存放独立的 Skill 文件或副本；修改 Skill 时只编辑 `skills/` 下的源文件。Skill 中引用项目文件时，以源文件所在位置计算相对路径。

`tools/` 放可执行的 CLI、脚本及其参数说明；`skills/` 放不能从 `--help` 推断的工作流、项目判断标准和验收规则。同一服务可以同时有工具和 Skill，但不要为每个工具入口另建一份只重复命令用法的 Skill。

## 文档导航

- **架构总览**：[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

- **Schemy 解释器(库开发)**：[schemy-master/AGENTS.md](schemy-master/AGENTS.md)

- **写 .scm 内容（世界观口径 + 引擎隐含行为 + 内容作者的判断）**：[skills/write-scheme/SKILL.md](skills/write-scheme/SKILL.md)
- **交锋机制的数学研究与长期续研**：[skills/research-mechanisms/SKILL.md](skills/research-mechanisms/SKILL.md)
- **制作 3D 资产（参考图、低模、Blender 加工、Unity 命名契约）**：[skills/create-3d-assets/SKILL.md](skills/create-3d-assets/SKILL.md)
- **制作游戏图片（舞台人物、姿势、2D 道具、3D 参考图）**：[skills/make-images/SKILL.md](skills/make-images/SKILL.md)
- **要不要验证 / 怎么验证**：[tools/VERIFYING.md](tools/VERIFYING.md)
- **Web Preview、Web Release、TapTap Release 三种构建方式**：[tools/BUILDING.md](tools/BUILDING.md)

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

**持久化规则**：项目运行时代码、内容脚本和配置不得使用 `PlayerPrefs`。凡是需要持久化的游戏状态，统一纳入现有存档系统；不要新增设备级或平台级的独立持久化。Unity 自动维护的、且明确关闭支持的项目元数据不视为运行时使用。

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

- 选择覆盖本次主要风险的最小充分验证；静态审阅足够时可以不运行命令。验证放在一次改动的整合点，不要每改几行就重复构建。
- 用户明确要求的验证应执行；环境不支持时说明限制，不用不等价的检查替代。
- 编译通过、内容加载通过和实际交互正确是不同的证据。`error` / `throw` 只有在相关路径被加载或执行时才有价值，不能把“下次运行会暴露”当成已经验证。
- 如实报告运行过的检查及其结果；未运行就写“未运行”，静态审阅不称为“已验证”。失败时区分本次改动导致的问题与已有问题。

需要选择具体检查、确认覆盖范围或修改 `GameTester` 时，按相关章节查阅 [项目验证指南](tools/VERIFYING.md)，不要求每次改动通读。

# Content 资源规则
- `UnityClient/Assets/Resources/Content` 是可执行 Scheme 内容的唯一来源；直接修改其中的 `.scm`，不存在同步副本。
- `UnityClient/Assets/Resources/Fonts` 保存字体源文件；发布构建只在 staging 工程中生成和替换字体子集，不修改主工程资源。
- `UnityClient/Assets/StreamingAssets` 只放必须以原始文件形式读取的资源，目前是 `Cutscenes/*.mp4`；不要把 Scheme 或字体放进去。
- 已退出运行时的旧内容放在 `docs/归档/Content`，只作历史说明；Git 才是版本历史的权威来源。
