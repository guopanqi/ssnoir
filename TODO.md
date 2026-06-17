# TODO

## Schemy 的改造

以后如果要改造 Schemy 解释器（为本项目或未来项目复用），以下问题值得处理：

- **Named let 不支持**：`(let loop (...) body)` 这种写法会报错，需要在解释器的 `let` 展开阶段加一个分支，检测第一个参数是 symbol 时转为 `letrec`。
- **不支持 dot rest 参数**：`(define (f x . rest) ...)` 这种 variadic 写法不可用，目前只能用 `args` + `car/cdr` 手动解包。
- **内置函数偏少**：缺少 `filter`、`for-each`、`list-ref`、`assoc` 等常用 stdlib 函数，场景脚本里只能手写递归替代。
- **文件读取与 Unity 冲突**：`load-file` 的路径处理和 Unity 资源加载机制有摩擦，跨平台部署时需要统一处理。

## Presentation / State Sync

- Notification currently follows real runtime state immediately. Some action-result notifications can appear before the action presentation finishes, while the visible UI is still showing the previous displayed snapshot. Later, consider routing notifications through the same presentation/adopt timing model, or buffering action-scoped notifications until presentation completion.
