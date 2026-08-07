# TODO

## Schemy 的改造

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

## 思想，而非代码
我读了那篇Control the idea, not the code，其实是指我们应该掌握代码中的思想，不应该再审查代码或者说逐行去看代码，我们应该把精力放在代码上的思想、质量测试、你用代码来干的这种设计目标等等其他东西上面。所以我觉得我们这个项目其实我需要梳理一下它的思想。

我们之前做了一个Python的项目，应该说它完全是许愿式的编程，我们对于结构也没有任何的掌控，它最终的结构变得非常的糟糕。在这个项目里面，多亏了claude的一些设计方案，当然GPT也提了一点贡献，但是claude我认为概念更清晰，整个项目的结构我是有清晰的把握的。在许多细节上我仍然不了解，比如说一个raylib render它是如何渲染那些东西的，我不太清楚。

我觉得现在我在代码上仍然不够自动化，它应该还是有更大的潜力。但是就这个项目而言，我认为我们应该总结一下其中的思想。
一个是数据和表现层面的分离，表现层面跟数据的变化甚至是不是同步的。我们同一个数据层有两个渲染层去表示它。我们采用了IMGUI的方式去为一个节点数据呈现世界。我们引入了一个Scheme的解释器。我们之前做了一个，就是游戏状态跟渲染完全分离的东西，能够实现代码自己去构造状态，模拟一些东西。但是我觉得那个不是特别有必要，因为这种游戏本质上逻辑并不特别复杂，状态变化也没那么多。这种复杂的能够通过代码去模拟的东西，对我们来说是小题大做。而他强制把这些这个这个逻辑拆开的时候，要求他可构造可验证，其实也带来了一些不必要的负担。

我学到的最大的一个东西就是限制其实是好的，无论是IMGUI还是游戏交互方式、呈现方式上的限制，它的本身的简洁能让我们很快把注意力集中在更上层的东西上。

我学到另一个东西就是还是要买这些大模型的服务。模型和产品都非常重要。一个可靠的模型非常重要，一个快速的模型也很重要。因为你的时间非常宝贵，你的注意力需要放在最重要的事情上。