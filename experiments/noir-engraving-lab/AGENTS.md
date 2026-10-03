# Noir Engraving Lab

这是 SSNoir 的长期视觉研究工程，不是生产客户端，也不是一次性 Demo。

先读 `DESIGN.md`。它定义视觉假设、工程分层、固定镜头和研究顺序。

## 边界

- 不依赖 Unity、Engine、Scheme、CityBox 正式输出。
- 不为了实验修改生产代码。
- 实验代码本身不成为 Unity 依赖；最终迁移的是结论。
- 默认使用程序化几何与 shader。只有研究问题明确需要时才加入外部资产。

## 工程纪律

- `main.js` 只装配，不承载视觉实现。
- 全局视觉参数放 `config/visualProfile.js`。
- 场景构成放 `scene/`。
- 灯光、空气等横切系统放 `systems/`。
- 最终画面处理放 `post/`。
- 固定摄影机位只在 `shots.js` 定义。
- 不写“只为了这一张截图”的特殊分支。
- 不把同一参数复制到多个文件。
- 程序生成必须使用可复现 seed，不使用不可控随机来决定静态场景。

## 实验纪律

一次实验只回答一个主要问题。

重要改动至少检查 4 个固定镜头，并记录到 `RESEARCH_LOG.md`：

1. 问题；
2. 固定条件；
3. 变量；
4. 四个镜头的观察；
5. 保留 / 放弃 / 尚不确定；
6. 原因；
7. 如何迁回 Unity。

不要用“更酷”“更高级”作为判断。优先级是：

黑色电影情绪 > 阅读层级 > 黑暗的意义 > 风格统一 > 可程序化生产 > 可迁回 Unity > 技术新奇。

## 推进顺序

按 `DESIGN.md` 的 Phase 推进：

Shape → Line → Light → Atmosphere → Print → Procedural Grammar → Narrative Image。

除非前一阶段暴露阻塞，不要跨阶段同时大量加入效果。
