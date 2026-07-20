# SSNoir Scheme 内容编写指南

写 `.scm` 游戏内容(场景 / 动作 / 对白 / 规则)看这里。

> **底层解释器(Schemy)支持哪些语法和内置函数,以唯一来源
> [schemy-master/AGENTS.md](schemy-master/AGENTS.md) 为准**(含能力总览与改动记录)。
> 例如 dotted rest 变参 `(define (f a . rest) …)`、`let*`、`and`/`or` 短路、
> `abs` / `eqv?` / `pair?` / `display` / `error` 等**现在都已可用**;`case` 仍不支持。
> 本文件**不再重复**这些解释器事实,只讲 SSNoir 自己的脚本层。

---

## 1. 脚本分层

| 文件 | 内容 |
|---|---|
| `Content/scripts/stdlib.scm` | SSNoir 内容脚本助手(`filter`、`member?`、`assoc-get` 等) |
| `Content/scripts/engine.scm` | SSNoir 的 DSL 与游戏框架(节点、时钟、规则、状态桥) |
| `Content/scenes/world/*.scm` | 世界地点 |
| `Content/scenes/encounters/*.scm` | encounter(交锋)场景 |

整体架构见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

---

## 2. DSL 快速参考（engine.scm）

完整定义以 [engine.scm](Content/scripts/engine.scm) 为准,这里只列常用构造。

**节点** — 一个节点只有两种合法形状。**优先用下列包装函数**,它们各自只对应一种形状,拼不出坏节点:
- 容器(可进入、铺开子节点):`(container name children)` / `(container-with-clocks name children clocks)`
- 动作 / 叶子(点击即结算或翻卡,**不带子节点**):`(action name requires resolve)` /
  `(instant-action name effect)` / `(observe-action name text)` /
  `(roll-action name requires skill fail neutral success)`

裸 `(node name :subtitle … :children … :clocks … :requires … :resolve … :disabled bool)` 是通用底层构造,
**除非清楚自己在做什么,否则别直接写**(典型正当理由:需要 `:disabled #t`——前端灰显且引擎拒绝执行,目前只有裸 `node` 暴露这个关键字)。

> ⚠️ 引擎判据 `IsContainer = (Resolve == null)`:**节点要么是容器(只认 `:children`)、要么是动作(只认 `:resolve`),二者互斥**。
> 裸 `node` 允许你同时写 `:resolve` 和 `:children`,此时 **`:children` 被静默忽略**——节点照常加载、`--validate` 也不报错,
> 但子节点永远不显示。需要"既有说明、又有子动作"时,把说明做成一张 `observe-action` 子卡放进 `container`,不要给容器加 `:resolve`。

**动作结算(resolve)**
- `(instant effect)`
- `(roll skill fail neutral success)`,带难度修饰:`(roll skill mod-fn fail neutral success)`
- `(observe text)`、`(clock clock-data)`
- `(outcome title subtitle effect ['light | 'heavy])` — 给效果附加结果表现
- `(modifier value reason)` — 难度修饰项

`roll` 判定使用全游戏唯一的数字命运骰：

```text
准备值 B = 放入骰 + 技能 + 修正
命运总和 T = B + d6 + 天然面修正
d6 天然1 时额外 −1，天然6 时额外 +1
T ≤6 坏，7–8 中，≥9 好
```

技能从 0 起，有效范围为 0–4。`modifier` 是玩家可见的数值修正，
负数代表更难、正数代表更容易。冷静不再给所有判定施加隐藏修正：低冷静的骰子降点在
每天/每个交锋回合重掷时已经结算，并直接显示在手牌中。

失败是否额外花冷静必须由内容明确决定：在对应 bad outcome 里写 `(spend-composure! 1)`。
不要把它当成 `roll` 的隐式默认效果；有些失败只推进场面时钟，有些失败才会让主角破防。
冷静已经为 0 时，这个调用会改为扣健康，并自动附上一条生理击穿说明。

恢复性判定使用 `(recovery-roll-action name requires skill fail neutral success)`。它与普通
`roll-action` 使用同一套命运结算，区别只在内容语义；恢复动作失败是否有额外代价，同样由内容显式写出。

带薪工作默认使用 `(工作 ...)`，不会自动增加关系。只有内容语义明确偏向帮忙、经营人情或
承担额外风险时才使用 `(关系工作 ...)`；其好结果自动令所属势力关系 +1。非法行动若可能损害
其他势力，必须在 subtitle 中提前点名，例如“事败将得罪官僚”。

`工作` 与 `关系工作` 的 risk 只接受 `'低` 或 `'高`，写入其他值会立即报错。低风险的坏结果
应保持轻度，高风险的中立结果也可以有损耗、坏结果应明显更重。非法工作使用
`(非法工作 ...)`：它仍要选择低/高风险，同时额外显示「非法」标签并获得固定 −2 修正；
「非法」不是第三档风险。

具名人物节点的标题只写玩家日常称呼的名字；subtitle 必须同时说明人物身份，例如
`乔 / 码头搬运工，独自抚养孩子`。同一人物的行动节点也要保留这段身份提示，不能假设玩家
记得此前的介绍。全名只用于档案、名片、正式介绍或剧情中特意叫全名的时刻。

**时钟** `(make-clock label max style [note])` → 消息 `'tick!` `'reset!` `'full?` `'current` `'set!` `'render-data`。
`note` 用于解释归零/填满会发生什么；凡是持续若干回合、延迟发生或下一回合消失的状态，都必须
用可见 Clock 告知玩家，不能只藏在脚本计数器里。

**要求(requires)** `(req-die)`、`(req-item name qty)`

**规则系统** `(define-rule name cond act)` / `(define-turn-rule …)`,由 `(on-action)` / `(on-turn-end)` 统一遍历。

**状态桥**
- 纯全局键:`(get-global k)` / `(set-global! k v)`(章节、声誉、剧情 flag)
- 队伍 / 库存 / 成长:`party-health`、`damage-party!`、`item-count`、`add-item!`、
  `growth-level`、`set-growth-level!`、`actor-composure`、`spend-composure!` 等(见 engine.scm)
- 轻型结算:`(outcome title subtitle effect ['light | 'heavy])`、`(result-note! text)`
- 其他表现:`(notify! text)`、`(spotlight! title subtitle)`、`(play-narration! id)`
- 对话:`(play-banter! (line ...) ...)`、`(play-dialogue! (line ...) ...)`、`(play-animation! tag)`(见 3.6-3.7)

**encounter 切换** `(start-encounter name callback)` / `(end-encounter result)`

**成长** `(growth-level)`；能力升级只由客户端人物成长面板操作，不在 Scheme 场景中提供升级动作。
`(complete-section!)` 表示一个不可重复的主线/人物小节已经结束，只负责增加一点成长并提示；
能否完成、是否已经完成由拥有该状态的单向状态机断言，不在 helper 内做去重兼容。

**同伴** `(recruit-companion! actor-id name stats-alist)` / `(has-companion? actor-id)`；招募要求四项
能力都明确给出，例如 `((violence 1) (knowledge 2) (sharpness 0) (social 1))`。重复 ID、缺失或
未知能力、超出 0–4 的数值都会直接报错。同伴在城市每天一颗骰，交锋中不掷骰；状态和能力随存档保存。

**休息阻塞** `(rest-block! id reason)` / `(rest-release! id)` / `(rest-blocked?)` /
`(rest-block-reasons)`。用于已经到期、当天必须处理的关键事件；支持多个不同 `id` 同时存在。
内容应在事件变为待处理状态时注册，在完成回调中释放。读取存档时先
`(clear-rest-blockers!)`，再由各地点根据自己的持久状态重新注册；不要把注册写进节点渲染函数。
公开的 `(end-turn!)` 在仍有 blocker 时会拒绝推进，住所休息节点负责用 `:disabled` 和原因标签提前展示。

---

## 3. 编写约定（必须遵守）

### 3.1 encounter → world 状态传递:用 callback,不用 global

encounter **不要**直接写 global 通知 world 推进任务(越权、污染全局命名空间)。
`start-encounter` 传 callback,encounter 只汇报结果:

```scheme
;; world 场景
(start-encounter "追击黑衣人"
  (lambda (result)
    (when (equal? result 'success) (set! mission-stage 2))))
;; encounter 场景
(end-encounter 'success)   ; 只说结果,不知道外部是谁
```

callback 在 `LoadScene("world")` 之前执行,world 闭包状态更新后,render tree 一次性以正确状态重建。

### 3.2 冷静 / 伤害函数的调用场景

`spend-composure!` 依赖 action 执行上下文,只有 `ExecuteAction` 期间有效;
encounter 结束 callback 可能由回合结束规则触发,此时没有 action context。

| 场景 | 正确用法 |
|---|---|
| roll 的 fail/neutral/success 回调(action lambda 内) | `(spend-composure! n)` |
| `start-encounter` 的结果 callback | `(spend-actor-composure! 'player n)` |

`end-turn!` 在交锋中会自动花主角 1 点冷静，再推进 turn rules 并重掷骰；城市中只推进规则和重掷。
睡眠、露宿或其他休息方式必须在各自的内容动作里显式调用
`restore-actor-composure!` / `spend-actor-composure!`。喝酒恢复冷静后还必须调用
`apply-hangover!`，把代价延后到下一次城市掷骰的一个骰池位置。每个行动者各自拥有冷静和骰池状态；低冷静的失态/失控状态会随机附着在其中一个位置；骰子可以自由投入行动，不能把位置误当成行动限制。

### 3.3 节点函数扁平化:避免深嵌套 append/if

Schemy 在深嵌套 `(append (if …) (if … (append …) '()))` 中,某些分支会求值成 `None`
而非 `'()`,导致 `append` 报 `Cannot convert Schemy.None to type List`1`。

**规则:每个 `define` 函数只构造一个节点或一段节点列表,单个表达式里 `if/append` 不超过两层。**

```scheme
;; 正确:每段逻辑抽成独立 helper
(define (node-check-gate) (instant-action "查看大门" …))
(define (node-gate)       (container "大门" …))
(define (stage-one-nodes)
  (append
    (if (not gate-revealed) (list (node-check-gate)) '())
    (if gate-revealed (list (node-gate)) '())))
```

### 3.4 地点状态分层

| 状态类型 | 存放位置 |
|---|---|
| 任务阶段 / 跨场景进度 | 地点本地 `define` + save/load(不泄露到 global) |
| 地点内部 UI 状态(门是否开、NPC 是否说过话) | 同上 |
| 真正全局共享(声誉、跨地点资源) | `set-global!` / `get-global` |

### 3.5 轻型动作结果:叙事与效果条

动作 lambda 内对库存、健康、冷静、关系、成长的实际修改,会按执行顺序自动写入
`ActionReport`,前端在原卡片上显示为效果条。脚本不要再重复编写“金钱 +8”一类展示文本。

```scheme
(outcome "勉强完成" "货送到了,但你累得够呛。"
  (lambda ()
    (add-item! "金钱" 8)       ; 自动生成:金钱 +8
    (spend-composure! 1)       ; 自动生成:冷静 -1
    (change-faction-relation! "劳工" 2))) ; 自动生成:劳工关系 +2
```

`roll-action` 和 `工作` 的三个结果分支都必须传 `outcome`,传裸 lambda 会直接报错。
结果描述只有 `outcome` 这一处来源,不要在 effect 中重复写通知。

无法从状态变化推导的结果用 `(result-note! text)` 增加一条中性效果,例如
`(result-note! "解锁:码头账房")`。

`notify!` 只用于动作结果之外的即时系统提示。动作结算叙事必须使用 `outcome` 或
其他专用表现接口,避免卡片结果与短暂通知重复。

资源槽投入属于成本,不会进入效果条。所有数值条目记录 clamp 后的实际变化量。

### 3.6 表现方式总览:notify / spotlight / narration / banter / dialogue / outcome

六种表现接口容易记混,尤其是 `spotlight!`——**它是一张"告示/公告卡":只有标题和一段说明文字,
没有说话人、没有台词、不是对话**,用来宣布一个结果或转折(比如某个阶段性事件发生了),
读者读到的是叙述而不是两个人的交谈。要有人物真正开口说话,必须用 `banter` 或 `dialogue`。

| API | 阻塞? | 结构 | 谁来推进/关闭 | 典型用途 |
|---|---|---|---|---|
| `(notify! text)` | 否,定时消失 | 一行文字 | 自动淡出(toast) | 系统提示、背景事件播报;不代表"这次行动的结果" |
| `(spotlight! title subtitle)` | 是,需要玩家点击关闭 | 标题 + 一段说明文字(**无说话人、无多行台词**) | 玩家点击 | 告示/公告卡:宣布一个转折或阶段性结果,是叙述,不是对话 |
| `(play-narration! id)` | 否,定时消失 | 一行文字(按 `id` 展示,Terminal 端显示为 `[旁白] id`) | 自动淡出 | 环境/场景旁白字幕;**目前内容脚本里还没有实际用例**,可用可不用 |
| `(play-banter! (line ...) ...)` | 否 | 多行台词(说话人+文本) | 自动逐条计时的气泡 | 失败后斗嘴、行动后随口一句;不打断游戏 |
| `(play-remote-banter! (line ...) ...)` | 否 | 多行台词(说话人+文本) | 自动逐条计时的气泡 | 电话、门外插话、场外事件;未在场者显示为侧边临时卡 |
| `(play-dialogue! (line ...) ...)` | 是,点击推进 | 多行台词(说话人+文本),线性无分支 | 玩家点击推进 | 主角↔NPC 的正式对话 |
| `(outcome title subtitle effect [mode])` | 是,跟随动作结果卡 | 标题+副标题+效果,附着在判定/instant 效果上 | 玩家确认动作结果卡 | 判定/即时动作的结果呈现;本体是"这次行动的结果",**只能用在动作 resolve 里**,不是对话 |

`outcome` 见 §3.5;下面详细展开 `banter`/`dialogue` 的调用规则与阻塞语义。

### 3.7 角色对话:banter(非阻塞)与 dialogue(阻塞)

两者共用 `(line 说话人 文本 [语音] [停留秒])` 构造台词,区别只在**阻不阻塞**(名字编码语气,不编码阻塞性,记住下表):

| API | 阻塞 | 推进 | 用途 |
|---|---|---|---|
| `(play-banter! (line ...) ...)` | 否 | 自动计时 | 失败后斗嘴、行动后插话;游戏照常,气泡在角色处自动消失 |
| `(play-remote-banter! (line ...) ...)` | 否 | 自动计时 | 明确来自场外的插话;未在场者显示在不可交互的侧边临时卡上 |
| `(play-dialogue! (line ...) ...)` | 是 | 点击 | 主角↔NPC 正经对话;锁输入、冻结导航,点屏幕推进,演完还控制权 |
| `(play-animation! tag)` | 是 | 占位 | 命名动画占位(v1 仅显示 tag);**只能在动作内调用** |

```scheme
(play-banter!
  (line "夜莺" "你管这叫计划?")
  (line "主角" "至少我有计划。"))

;; 明确写为场外,而不是让普通 banter 因找不到锚点静默退化。
(play-remote-banter!
  (line "门外的人" "里面还好吗?"))

(play-dialogue!
  (line "主角" "海伦,我们得谈谈。")
  (line "海伦" "我没什么好说的。"))
```

`说话人` 解析顺序固定:**队员 Id → 队员 Name → 当前场景节点 Name**。普通 `play-banter!` 和动作内 `play-dialogue!` 解析不到会直接报错；这能尽早暴露拼写或节点配置错误。只有显式的 `play-remote-banter!`，以及动作外即时 `play-dialogue!`，会以不可交互的侧边临时卡承接未在场说话人，表达电话、回忆或场外事件，而非自动导航到某地点。

**调用时机与延迟规则**(与 `spotlight!` / `play-narration!` 一致):

- **动作内**:`play-dialogue!` / `spotlight!` / `play-animation!` 按调用顺序排入有序阻塞步骤,在动作结算后、采用新快照**之前**逐个播放;`play-banter!` / `play-remote-banter!` 延迟到采用新快照**之后**释放(避免提前剧透)。
- **动作外**(进场脚本、规则):`play-banter!` / `play-remote-banter!` / `play-dialogue!` 立即触发;`play-animation!` 目前不支持动作外调用(没有即时动画通道)。

**锚定约束(由"何时播放"决定,务必遵守):**

- 动作内 `play-dialogue!` 锚定**动作前**的旧视觉状态。删掉海伦节点后再让海伦说话 ✓;新建"陌生人"节点后立刻让其说话 ✗(尚未采用,无锚点 → 报错)。
- 动作内 `play-banter!` 锚定**动作后**的新视觉状态。新建角色插一句 ✓;给刚删掉的角色 banter ✗。
- 动作内 `play-remote-banter!` 同样在**动作后**播放，但未在场说话人会显示为侧边临时卡。它必须由作者明确选择，不能用来掩盖普通 banter 的拼写或节点配置错误。
- 需要让"刚出现的角色"做一段阻塞对话时:先用一次节点/场景推进让其出现,再单独 `play-dialogue!`;或改锚定到已在场的角色。
- 动作外 `play-dialogue!` 优先锚定仍在屏幕上的队员或节点；说话人不在场时会显示为侧边临时卡。用它表达来电、转述、回忆等不要求玩家已抵达现场的事件，不要借此掩盖动作内对白的拼写或节点配置错误。

**分支选择不进对话播放器**:对话播放器永远线性。需要玩家选择时用节点表达,各分支的 `instant-action` 里再调 `play-dialogue!` / `set!`。

### 3.8 交锋入场与场内转场

交锋是一段已经开始的行动场景。玩家进入 encounter 后应当**立刻看到可执行的交锋动作**，
不能再要求点击一次“开始交锋”“确认身份”“继续靠近”等预备节点。

- **入场剧情由调用方播放。** 交锋发生前若需要对白、交代地点或说明玩家采用了什么身份，
  应在城市 / 故事脚本调用 `start-encounter` 之前完成。交锋脚本只把这些内容当作既定前提。
- **既定前提写在题面上。** 假身份、保护方案、谁在场等信息放在容器标题、subtitle、标签和
  Clock 备注中；不要做成必须先点击的 `observe-action` 或无实际选择的 `instant-action`。
- **场内转折使用阻塞式重对话。** 通过一段路线、敌人正式出面、局面换幕等真正改变题面的事件，
  使用 `play-dialogue!` 交代过程，然后更新阶段状态、让下一份 render tree 接管场面。
  转场对话仍遵守上面的锚定约束：动作内只能让旧画面已有的角色说话；刚进入下一幕的角色若尚无锚点，
  先用“世界”叙述其出现，或拆成下一幕中的独立对话，不得靠拼写容错蒙混过去。
- **`spotlight!` 只宣告结果。** 它适合交锋成功、失败、阶段结算等告示卡，不承担人物对白，
  也不拿来替代换幕过程。只要场面中的人物正在行动或说话，就使用 `play-dialogue!`。
- **说明卡永远可选。** `observe-action` 可以提供额外背景，但不得挡在核心动作之前；删除它后，
  玩家仍须能够从标题、动作和 Clock 看懂“现在要做什么、失败会怎样”。

### 3.9 可复发交锋的状态边界

事件状态归最小且明确的拥有者：只影响一个地点的事件由地点闭包保存；跨地点可见、阻塞世界日程的
公共事件由 `world.scm` 保存。两者都纳入 `world-save`，不要把公共调度寄存在某个受影响地点里。

encounter 每次新建，只读取本场需要的只读输入（例如场次、是否有盟友），结束时只通过
`end-encounter` 返回结果。拥有者传给 `start-encounter` 的 callback 负责统一结算、排下一场或收尾。
不要让 encounter 直接修改外部任务阶段。

若 encounter 必须读取地点私有状态，可在入场前镜像最小的只读值到 global；地点每次变更和
`load!` 后都要重新同步。不要把整套地点状态复制到 global。
