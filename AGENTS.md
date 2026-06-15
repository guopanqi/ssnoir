# 我的工程风格偏好

请你以清晰的界限，简洁的结构，但不过度设计的最终目标来计划

- 游戏内容设计以数据驱动为主, 便于llm阅读和修改游戏数据
- 在进行工程上的设计或重构时, 保持 清晰的文件边界, 不进行太多不必要的过度设计
- 减少混乱的兼容或者对错误的容忍, 对于可能存在的游戏逻辑错误, 或者数据配置错误, 直接assert中断游戏, 打印错误, 最大程度确保游戏的健壮性, 如果有一些建议可以向用户提问

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

# 关于schemy
- Schemy 解释器的完整特性、内置符号支持矩阵与 `stdlib.scm` 补全说明请参考 [schemy.md](file:///Users/usr/documents/play/ssnoir/schemy.md)。
- Schemy 不能使用 dotted rest args（如 `first . rest`），但可以使用纯列表形式的 varargs 参数定义来实现相同的消息协议机制（详见 [schemy.md 中的说明](file:///Users/usr/documents/play/ssnoir/schemy.md#1-变参语法限制-varargs-syntax-constraints)）。
- Scheme 脚本里的对白文本不要在字符串内容中再使用中文/英文引号。对话统一写成 `角色：内容`，例如 `"夜莺：你终于来了。"`。不要写 `"夜莺：“你终于来了。”"`，也不要写未转义的嵌套英文双引号；后者会让 Schemy 把后续中文当作 symbol 求值，运行时报 `Symbol not defined`。

# Content 同步规则
- Unity 客户端中的 `StreamingAssets/Content`、`Resources/Content` 和 `Fonts` 资源是从项目根目录的 `Content` 目录同步复制过去的（详见 [ContentSyncEditor.cs](file:///Users/usr/documents/play/ssnoir/UnityClient/Assets/Editor/ContentSyncEditor.cs)）。
- 在修改根目录 `Content` 下的 Scheme 脚本、场景或字体等资源后，Unity 客户端在加载或进入 Play 模式时会自动运行同步导入。如果需要，可在 Unity 中运行顶部菜单 `SSNoir -> Sync Content Now` 进行手动同步。
