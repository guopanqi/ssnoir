# 我的工程风格偏好

- UI 以代码驱动为主
- 内容以数据驱动为主, 便于llm阅读和修改游戏数据
- 保持工程结构清晰, 可靠, 优雅, 但不要过度设计,过度拆分或者过度复杂
- 减少混乱的兼容或者对错误的容忍, 对于可能存在的游戏逻辑错误, 或者数据配置错误, 直接assert中断游戏, 打印错误, 最大程度确保游戏的健壮性, 如果有一些建议可以向用户提问

# rules
UI优先使用“约束驱动布局”, 减少像素硬编码布局, 纵向使用流式布局，横向使用分隔split, 根据panel的rect来计算和放置元素的位置
- 不要在ui文字中使用emoji

# 测试验证
避免不必要的频繁测试和构建。只有在必要时（例如：进行大量代码修改、完成某一模块的重构、或者需要排查并验证是否存在特定错误时）才进行检查。
- 语法检查（仅在可能遗留括号缺失等低级语法错误或大范围重构时）
- 全量内容校验与最小状态模拟（仅在需要验证复杂边界逻辑行为或修复疑难问题时）

# tips
- Schemy 解释器的完整特性、内置符号支持矩阵与 `stdlib.scm` 补全说明请参考 [schemy.md](file:///Users/usr/documents/play/ssnoir/schemy.md)。
- Schemy 不能使用 dotted rest args（如 `first . rest`），但可以使用纯列表形式的 varargs 参数定义来实现相同的消息协议机制（详见 [schemy.md 中的说明](file:///Users/usr/documents/play/ssnoir/schemy.md#1-变参语法限制-varargs-syntax-constraints)）。

