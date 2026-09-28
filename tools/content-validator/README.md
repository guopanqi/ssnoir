# 内容校验工具

这里保留不依赖 Unity UI 的纯逻辑验证能力，不是第二个游戏客户端。它直接编译
`Engine/Runtime/**`，并读取 `UnityClient/Assets/Resources/Content` 中的唯一一份 Scheme 内容。

从仓库根目录运行：

```bash
./run --validate
./run --test-saveload
./run --test-odds
./run --playtest <入场表达式或场景名>
./run --session world --seed 7
./run --replay /path/to/record.json
./run --session-baseline encounters/码头试运行 --seed 7 --output /tmp/baseline.json
./run --session-report /tmp/baseline.json /tmp/agent.json
```

## 玩家视角会话

`--session` 启动一个持续运行的会话。它从同一份 `PresentationSnapshot` 生成当前焦点、
可见卡片、全场时钟、角色、骰子、背包、阻塞原因与操作目录。投骰操作附准备值和
与客户端同源的六面赔率，回合阶段快照也保留全场时钟。工具不会把 Scheme 解释器暴露给
Agent。入场参数由测试操作者指定：`world` 为新局，`encounters/<名称>` 为直载交锋，
Scheme 入场表达式只在创建会话时执行，用于测试真实的世界入场流程。

标准输出每行一条 JSON，标准输入每行一条命令：

```json
{"command":"observe"}
{"command":"act","version":1,"operationId":"o1","reason":"这一手先确认地点"}
{"command":"save","path":"/tmp/ssnoir-session.json"}
{"command":"quit"}
```

启动时立即输出入场对白等 `events` 与一次 `observation`；`act` 返回本步 `events`
和下一次 `observation`。
操作 ID 只在其所属观察版本有效，过期版本和非法操作返回 `error`，会话不前进。
`reason` 是实验注释，不参与游戏结算或重放。导航、进入地点、卡片执行、随身动作和
结束回合都走同一操作目录。回合转换的每个引擎阶段单独列在 `events` 中，
并附当时的场面快照；阶段内的自动行动和对白按顺序记录。

`save` 写入初始条件、内容与工具代码指纹、逐步操作、选择理由、前后完整观察及摘要。
`--replay` 在相同指纹下重新执行每一步，并比较观察和反馈；内容或代码变化时拒绝
重放，避免把不同版本的结果混在一起。每个进程只运行一局；并发实验请使用独立进程，
因为游戏随机源是进程共享的。

`--session-baseline` 用同一协议运行确定性参照策略并写记录。它先用免费动作，
再优先选准备值较高的投骰，必要时逐层查看容器；冷静缺口不足时不浪费烟酒。
这只是比较用的固定策略，
不能当作真人玩家。`--session-report` 从一份或多份记录输出 JSON 摘要和每步理由，
供 Agent 对照分析。任何方案的体验结论都需要结合具体决策轨迹判断。

本协议只呈现焦点容器的直接子卡，导航状态由会话维护。它适合检验策略决策与
规则反馈；空间构图、动画节奏和触控手感仍需在 Unity 客户端验收。
