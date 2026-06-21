# TODO

## Schemy 的改造

Schemy 已 fork(`schemy-master/`)。能力总览、改动记录与开发约定都在
[schemy-master/AGENTS.md](schemy-master/AGENTS.md)。

**已解决**(原先列在这里的限制):named let、dotted rest 变参、`let*`、`and`/`or` 短路、
空 `(let)` 作用域泄漏、常用内置 / 库函数(`list-ref`、`filter`、`assoc` 等)、Unity 初始化
(`Assembly.Location` try/catch,不再需要反射 hack)。

**仍开放**:

- **`load-file` 与 loader 抽象**:游戏内容加载走 SSNoir 自己的 `IScriptLoader`,绕开 Schemy
  原生 `load` 的文件系统模型。方向是对的;但若 Schemy 未来要独立成可复用库,需提供通用的
  loader abstraction,而不是耦合具体游戏项目的路径模型。

## Presentation / State Sync

- Notification currently follows real runtime state immediately. Some action-result notifications can appear before the action presentation finishes, while the visible UI is still showing the previous displayed snapshot. Later, consider routing notifications through the same presentation/adopt timing model, or buffering action-scoped notifications until presentation completion.
- **Terminal/fallback grid 的 light outcome 残留位置问题**：动作执行后真实 render tree 会立刻刷新；如果执行节点消失，当前实现可能把结果残留作为 orphan card 排到 grid 末尾，视觉上会像“卡片跑了”。更完整的方案需要让残留保留旧布局信息、或改成最近结果条，但这会增加表现层复杂度。由于最终 Unity 场景里的节点应主要投射到 3D 空间 anchor 上，grid 只是测试/临时兜底，此问题暂不修复，后续根据实际使用频率再决定是否处理。
