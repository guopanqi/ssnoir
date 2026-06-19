# TODO

## Schemy 的改造

以后如果要改造 Schemy 解释器（为本项目或未来项目复用），以下问题值得处理：

当前判断：现在继续使用现有 Schemy + `stdlib.scm` / native wrapper 绕开问题仍然可以接受；真正 fork / 改造解释器还偏早。等这些限制再次明显拖慢脚本编写、Unity 初始化再次出问题、或开始多个项目复用时，再根据下面线索统一实施。

当前比较丑陋但暂可接受的实现：

- **Unity 初始化反射 hack**：Unity 下通过反射绕开 Schemy 构造函数中的 entry assembly / `.init.ss` 假设。短期可用，但长期应该在解释器本体中移除这种宿主环境假设。
- **项目层 `load-file` wrapper**：游戏内容加载走 SSNoir 自己的 `IScriptLoader`，绕开 Schemy 原生 `load` 的文件系统模型。这个方向是对的，但如果 Schemy 未来独立成库，需要提供通用 loader abstraction，而不是依赖具体游戏项目。
- **语法限制靠写法规避**：目前用 `(lambda args)` 手动解包来代替 dotted rest args；能用，但脚本不够接近 Scheme/Racket 直觉。
- **基础库缺口靠 `stdlib.scm` / native 补**：缺少常用函数时临时补在项目脚本层，能推进内容开发，但长期应把通用函数沉到 Schemy 库或标准库层。

未来改造目标：把 fork 后的 Schemy 做成独立、现代、可复用、Unity 友好的 C# Scheme 库；库本身不包含 SSNoir 概念，SSNoir 只作为使用者。

- **Named let 不支持**：`(let loop (...) body)` 这种写法会报错，需要在解释器的 `let` 展开阶段加一个分支，检测第一个参数是 symbol 时转为 `letrec`。
- **不支持 dot rest 参数**：`(define (f x . rest) ...)` 这种 variadic 写法不可用，目前只能用 `args` + `car/cdr` 手动解包。
- **内置函数偏少**：缺少 `filter`、`for-each`、`list-ref`、`assoc` 等常用 stdlib 函数，场景脚本里只能手写递归替代。
- **文件读取与 Unity 冲突**：`load-file` 的路径处理和 Unity 资源加载机制有摩擦，跨平台部署时需要统一处理。

## Presentation / State Sync

- Notification currently follows real runtime state immediately. Some action-result notifications can appear before the action presentation finishes, while the visible UI is still showing the previous displayed snapshot. Later, consider routing notifications through the same presentation/adopt timing model, or buffering action-scoped notifications until presentation completion.
- **Terminal/fallback grid 的 light outcome 残留位置问题**：动作执行后真实 render tree 会立刻刷新；如果执行节点消失，当前实现可能把结果残留作为 orphan card 排到 grid 末尾，视觉上会像“卡片跑了”。更完整的方案需要让残留保留旧布局信息、或改成最近结果条，但这会增加表现层复杂度。由于最终 Unity 场景里的节点应主要投射到 3D 空间 anchor 上，grid 只是测试/临时兜底，此问题暂不修复，后续根据实际使用频率再决定是否处理。
