# 接续与自主研究协议

本实验必须能脱离聊天 compact 继续。

## 开始

1. 读 AGENTS.md、CHECKPOINT.json、DESIGN.md、RESEARCH_LOG.md。
2. 有本地环境时运行 `npm install` 和 `npm run status`。
3. 有 pendingReviews 时优先打开对应 contact sheet 和 Final。
4. 没有 acceptedBaseline 时，先生成完整四镜头四层基线并实际审阅。
5. 每次只选一个主要问题；局部试拍验证后再做完整回归。

## 一轮闭环

问题 → 可证伪假设 → 小改动 → 局部截图 → 实际读图 → 完整四镜头回归 → 保留/放弃 → 写 RESEARCH_LOG → 更新 CHECKPOINT。

Actions success 不是艺术成功。没有实际看过图片时，只能记录“待审”。

## 固定审图问题

所有镜头：
- 第一眼落在哪里？
- 缩成小图后，黑白大形是否仍然成立？
- 人物是否靠姿态、帽檐、肩线、大衣/裙摆识别，而不是靠脸？
- 白线是否只解释需要解释的东西？
- 金色是否仍像稀缺叙事信息，而不是环境装饰？
- 场景是否像“动态平面设计”，还是普通低模城市加滤镜？

专项：
- Theater Tableau：剧院是否通过留白和图形面积成为焦点，人物有没有被建筑吞掉？
- Crowd Crossing：远中近人物是否有 Narrative LOD，crowd 是否变成噪声？
- Alley Confrontation：近人物是否能在几乎无脸部信息的情况下成立？
- City Canyon：压缩透视后是否出现图形节奏，还是只剩密集盒子？

## 流程自我改进

允许优化截图速度、错误检查、指标、checkpoint 和持久证据，但不要用自动指标替代审图。指标只用于发现漂移和约束，例如 gold share 失控。

重要接受基线应保存到 `captures/reviews/<name>/`，包含 contact sheet、manifest、report 和关键 Final。
