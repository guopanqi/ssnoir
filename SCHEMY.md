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

**节点**
- `(container name children)` — 含子节点的容器
- `(node name :subtitle … :children … :clocks … :requires … :resolve … :disabled bool)` — 通用节点；
  `:disabled #t` 时前端灰显且引擎拒绝执行
- `(action name requires resolve)` / `(instant-action name effect)` /
  `(observe-action name text)` / `(roll-action name requires skill fail neutral success)`

**动作结算(resolve)**
- `(instant effect)`
- `(roll skill fail neutral success)`,带难度修饰:`(roll skill mod-fn fail neutral success)`
- `(observe text)`、`(clock clock-data)`
- `(outcome title subtitle effect ['light | 'heavy])` — 给效果附加结果表现
- `(modifier value reason)` — 难度修饰项

**时钟** `(make-clock label max style [note])` → 消息 `'tick!` `'reset!` `'full?` `'current` `'set!` `'render-data`。
`note` 用于解释归零/填满会发生什么；凡是持续若干回合、延迟发生或下一回合消失的状态，都必须
用可见 Clock 告知玩家，不能只藏在脚本计数器里。

**要求(requires)** `(req-die)`、`(req-item name qty)`

**规则系统** `(define-rule name cond act)` / `(define-turn-rule …)`,由 `(on-action)` / `(on-turn-end)` 统一遍历。

**状态桥**
- 纯全局键:`(get-global k)` / `(set-global! k v)`(章节、声誉、剧情 flag)
- 队伍 / 库存 / 成长:`party-health`、`damage-party!`、`item-count`、`add-item!`、
  `growth-level`、`set-growth-level!`、`actor-stress` 等(见 engine.scm)
- 轻型结算:`(outcome title subtitle effect ['light | 'heavy])`、`(result-note! text)`
- 其他表现:`(notify! text)`、`(spotlight! title subtitle)`、`(play-narration! id)`
- 对话:`(play-banter! (line ...) ...)`、`(play-dialogue! (line ...) ...)`、`(play-animation! tag)`(见 3.6)

**encounter 切换** `(start-encounter name callback)` / `(end-encounter result)`

**成长** `(growth-level)`；能力升级只由客户端人物成长面板操作，不在 Scheme 场景中提供升级动作。
`(complete-section!)` 表示一个不可重复的主线/人物小节已经结束，只负责增加一点成长并提示；
能否完成、是否已经完成由拥有该状态的单向状态机断言，不在 helper 内做去重兼容。

**同伴** `(recruit-companion! actor-id name stats-alist)` / `(has-companion? actor-id)`；招募要求四项
能力都明确给出，例如 `((violence 2) (knowledge 3) (sharpness 1) (social 2))`。重复 ID、缺失或
未知能力、超出 1–6 的数值都会直接报错。同伴在城市每天一颗骰，交锋中不掷骰；状态和能力随存档保存。

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

### 3.2 压力 / 伤害函数的调用场景

`stress-current-actor!` 依赖 action 执行上下文,只有 `ExecuteAction` 期间有效;
encounter 结束 callback 可能由回合结束规则触发,此时没有 action context。

| 场景 | 正确用法 |
|---|---|
| roll 的 fail/neutral/success 回调(action lambda 内) | `(stress-current-actor! n)` |
| `start-encounter` 的结果 callback | `(add-actor-stress! 'player n)` |

`end-turn!` 只推进通用系统状态:饱腹消耗、饥饿伤害、行动骰重掷和 turn rules。
它不再自动恢复压力。睡眠、露宿或其他休息方式必须在各自的内容动作里显式调用
`heal-stress!` / `add-actor-stress!`,这样不同住所可以有不同效果,并且变化会进入动作效果条。

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

动作 lambda 内对库存、健康、饱腹、压力、关系、成长的实际修改,会按执行顺序自动写入
`ActionReport`,前端在原卡片上显示为效果条。脚本不要再重复编写“金钱 +8”一类展示文本。

```scheme
(outcome "勉强完成" "货送到了,但你累得够呛。"
  (lambda ()
    (add-item! "金钱" 8)       ; 自动生成:金钱 +8
    (stress-current-actor! 1)  ; 自动生成:压力 +1
    (change-faction-relation! "劳工" 2))) ; 自动生成:劳工关系 +2
```

`roll-action` 和 `工作` 的三个结果分支都必须传 `outcome`,传裸 lambda 会直接报错。
结果描述只有 `outcome` 这一处来源,不要在 effect 中重复写通知。

无法从状态变化推导的结果用 `(result-note! text)` 增加一条中性效果,例如
`(result-note! "解锁:码头账房")`。

`notify!` 只用于动作结果之外的即时系统提示。动作结算叙事必须使用 `outcome` 或
其他专用表现接口,避免卡片结果与短暂通知重复。

资源槽投入属于成本,不会进入效果条。所有数值条目记录 clamp 后的实际变化量。

### 3.6 角色对话:banter(非阻塞)与 dialogue(阻塞)

两者共用 `(line 说话人 文本 [语音] [停留秒])` 构造台词,区别只在**阻不阻塞**(名字编码语气,不编码阻塞性,记住下表):

| API | 阻塞 | 推进 | 用途 |
|---|---|---|---|
| `(play-banter! (line ...) ...)` | 否 | 自动计时 | 失败后斗嘴、行动后插话;游戏照常,气泡在角色处自动消失 |
| `(play-dialogue! (line ...) ...)` | 是 | 点击 | 主角↔NPC 正经对话;锁输入、冻结导航,点屏幕推进,演完还控制权 |
| `(play-animation! tag)` | 是 | 占位 | 命名动画占位(v1 仅显示 tag);**只能在动作内调用** |

```scheme
(play-banter!
  (line "夜莺" "你管这叫计划?")
  (line "主角" "至少我有计划。"))

(play-dialogue!
  (line "主角" "海伦,我们得谈谈。")
  (line "海伦" "我没什么好说的。"))
```

`说话人` 解析顺序固定:**队员 Id → 队员 Name → 当前场景节点 Name → 解析不到直接报错**(不静默兜底)。

**调用时机与延迟规则**(与 `spotlight!` / `play-narration!` 一致):

- **动作内**:`play-dialogue!` / `spotlight!` / `play-animation!` 按调用顺序排入有序阻塞步骤,在动作结算后、采用新快照**之前**逐个播放;`play-banter!` 延迟到采用新快照**之后**释放(避免提前剧透)。
- **动作外**(进场脚本、规则):`play-banter!` / `play-dialogue!` 立即触发;`play-animation!` 目前不支持动作外调用(没有即时动画通道)。

**锚定约束(由"何时播放"决定,务必遵守):**

- 动作内 `play-dialogue!` 锚定**动作前**的旧视觉状态。删掉海伦节点后再让海伦说话 ✓;新建"陌生人"节点后立刻让其说话 ✗(尚未采用,无锚点 → 报错)。
- 动作内 `play-banter!` 锚定**动作后**的新视觉状态。新建角色插一句 ✓;给刚删掉的角色 banter ✗。
- 需要让"刚出现的角色"做一段阻塞对话时:先用一次节点/场景推进让其出现,再单独 `play-dialogue!`;或改锚定到已在场的角色。

**分支选择不进对话播放器**:对话播放器永远线性。需要玩家选择时用节点表达,各分支的 `instant-action` 里再调 `play-dialogue!` / `set!`。

### 3.7 可复发交锋的状态边界

事件状态归最小且明确的拥有者：只影响一个地点的事件由地点闭包保存；跨地点可见、阻塞世界日程的
公共事件由 `world.scm` 保存。两者都纳入 `world-save`，不要把公共调度寄存在某个受影响地点里。

encounter 每次新建，只读取本场需要的只读输入（例如场次、是否有盟友），结束时只通过
`end-encounter` 返回结果。拥有者传给 `start-encounter` 的 callback 负责统一结算、排下一场或收尾。
不要让 encounter 直接修改外部任务阶段。

若 encounter 必须读取地点私有状态，可在入场前镜像最小的只读值到 global；地点每次变更和
`load!` 后都要重新同步。不要把整套地点状态复制到 global。
