# SSNoir 引擎与 DSL 工程规约 (Engine & DSL Contract)

本指南规定了将故事与文案落地为 `UnityClient/Assets/Resources/Content/**/*.scm` 时的底层技术契约、运行时行为与资产同步规则。

---

## 一、 节点构造、身份（Id）与 3D 空间锚点（Anchor）

### 1. 构造器优先级与禁止空容器
- 写节点优先使用语义明确的包装函数：`container` / `action` / `instant-action` / `observe-action` / `roll-action` / `note-node` / `clock-node` / `工作`。
- 现有包装函数无法表达所需的 `subtitle`、`tags`、`clocks`、`disabled`、`display` 或 `anchor` 组合时，才使用裸 `node`；若同一种裸结构反复出现，应优先补齐 `engine.scm` 接口，不要到处复制一次性包装函数。
- **禁止渲染空 Container**：Container 只表达确实存在的层级；没有子节点或可选子节点为空时，必须整组省略，绝不留下只有标题或 `:children '()` 的空壳。

### 2. Node Id 是全局唯一身份，`:name` 是可变显示名
- Scheme 中 `node` 的第一个参数是稳定的 `Id`，在**整棵渲染树内必须全局唯一**（不只要求同级唯一），重复 ID 会导致引擎直接报错中断。
- `:name` 只负责玩家可见的显示文字，允许在多个节点上重复（省略 `:name` 时默认沿用 `Id`）。遇到两个语义相同的动作（例如不同阶段的“交谈”），用稳定且唯一的 `Id` 区分身份，用 `:name "交谈"` 展示自然文字。
- 标注使用专门接口：`(note-node id title text . clock-data)` 的 `title` 是显示标题；`(clock-node id clock-data)` 的显示内容来自 Clock 数据。不要向这两个包装函数传 `:name`。

### 3. 正式内容必须显式声明 `:anchor`
- **所有正式内容的行动与 Container，必须显式声明 `:anchor`，指向当前空间中语义合适的已有 3D 锚点**（世界城市与 Scenario 均适用，裸 `node` 同样适用）。绝不能依赖引擎兜底的同名匹配或网格布局。
- **父子锚点规则**：父容器与子行动需分别声明 Anchor；发生在同一人物身上的询问、交付可共用该人物锚点，异地行动使用实际发生地点的锚点。不同阶段或互斥的多个节点可以共用同一个空间落点（如均声明 `:anchor "码头-货堆"`），`:anchor` 的值也可以是运行时算出的字符串。
- **唯一例外（Inline 容器）**：声明了 `:display 'inline` 的非空、非地点容器，整组以容器自身的 Anchor 定位；组内子行动按声明顺序纵向流式排列，**未单独声明 Anchor 的子行动会自动继承最近的 inline 组落点**。
- **如何查询可用 Anchor（严禁翻看 `Main.unity`）**：锚点来自 Blender 模型的 `Anchor_<节点名>` 空节点，由导入管线自动挂载。直接运行以下脚本列出全部可用锚点，找不到合适落点时应明确提出空间缺口，绝不编造名字：
  ```bash
  python3 skills/write-scheme/scripts/list-anchors.py
  ```
- **气泡发声位置**：场内非队员的 `play-bubble!` 必须显式用 `:at "已有Anchor"` 指定位置；尼尔与入队同伴可自动定位，旁白无需锚点。

---

## 二、 节点标签（Tags）、时钟（Clocks）与常驻标注

### 1. 受控标签白名单（`:tags`）
- 作者可手填的 `:tags` 是封闭白名单，目前**仅限 `"工作"`、`"非法"`**（未知标签直接报错）。`"工作"` 优先由 `工作` 构造器自动添加；`"非法"` 仅表示行为本身违法，不代表危险或可疑。
- 「必须处理」Tag 由 `:required-before-rest` 自动生成；「每日一次」Tag 由 `:use-limit 'daily-once` 自动生成。**严禁手写同名字符串 Tag 冒充规则。**

### 2. 时钟（`make-clock`）规范与好坏方向
- **默认一律使用 `make-clock` 创建时钟**，将格数、上限、备注、进退与存档收在闭包内。仅当遇到以下三类真实例外时，才手写 `(list 'clock 标签 当前 上限 样式 备注 ...)`：
  1. 渲染别处已有的真相（如 `(min 目标 (item-count "金钱"))` 或汇总几个 flag）。
  2. 上限会动态变化（如房租续期抬高上限）。
  3. 格数需要保留负数等越界状态或拔河式双向位置。
- **显式声明好坏方向（`:beneficial-direction`）**：时钟样式（`gauge` / `pressure` / `countdown` / `readout`）不决定颜色，必须显式指定方向以控制填充色与结算数字颜色：
  ```scheme
  ;; 减少有利（如敌人生命、愤怒、恐惧） -> 红色填充，减少时绿色反馈
  (make-clock "愤怒值" 6 'countdown "归零后冷静。" :beneficial-direction 'decrease)
  ;; 增加有利（如调查进度、剩余耐心、交租剩余天数） -> 绿色填充
  (make-clock "前进" 8 'gauge "抵达住处。" :beneficial-direction 'increase)
  ;; 中性或两端皆有风险 -> 金色填充（默认 'neutral）
  ```
- **时钟展示位置与主次优先级**：
  - 容器上的 `:clocks` 只在容器未点开时可见，点进子层即消失；需要跨层级常驻跟踪的状态必须用 `clock-node`（单根钟）或 `clock-nodes`（多根钟各自成条）独立为场景标注。**根容器禁止挂 `:clocks`。**
  - 在动作/容器卡上，使用 `(clock-priority (某某钟 'render-data) 'secondary)` 可将时钟转为紧凑的 Tag 形式（带小图形与读数，与普通 Tag 共用标签区，不显示备注）。手写 tuple 的第 8 项为主次，第 9 项为方向。

---

## 三、 状态生命周期：未读、每日限次、休息阻塞与回合结算

### 1. 未读标记与内容阶段（`:notice-version`）
- 引擎按稳定节点 `Id` 自动识别新增内容并向上递归汇总“新”标记，作者无需手动维护。
- 当同一个 `Id` 的节点进入了**值得重新提醒玩家的实质剧情新阶段**时，声明 `:notice-version "阶段名"`。切勿将倒计时、Clock 格数、库存变化或费用作为版本号。

### 2. 城市行动每日次数（`:use-limit 'daily-once`）
- 仅限城市侧的 Instant / Roll 行动使用（Scenario 禁用，且不接受其他数字或周期）。
- 引擎按稳定节点 `Id` 与真实世界日自动记录额度、生成标签并处理耗尽后的置灰原因。**不要再手写 `last-day` 变量或重复的 `:disabled` 条件。**

### 3. 休息阻塞（`:required-before-rest`）
```scheme
(node "醉客互相叫骂"
  :required-before-rest "先平息两桌争吵" ; 条件不成立时传 #f
  :resolve ...)
```
- 声明在必处理的动作、有可用动作的容器或有入场事件的地点上（不可放于 Scenario、纯标注或 disabled 路径）。
- 节点出现即阻塞休息，节点消失或传 `#f` 即自动解除；**严禁再写旧式的 `rest-block!` / `rest-release!` / `sync-blockers!`**，也严禁让该节点的存在反过来依赖 `(rest-blocked?)` 造成循环依赖。

### 4. 世界回合的两侧边界与 Scenario 入场边界
- **`define-turn-rule`（旧日结束 / 睡前）**：处理期限扣减、房租结算、未处理事件到期、临时状态清理。
- **`define-turn-start-rule`（新日开始 / 醒来黑幕亮起后）**：更新“今天开始”的事实与人物行程。早报、来信等获知消息按规范放入 arrival，不要写进 `define-turn-rule`。Scenario 内继续使用 `define-opponent-rule`。
- **Scenario 入场演出边界**：按**演出发生在哪个 3D 空间**划分——出发前/城市门外的演出写在 `start-scenario` 调用前；已经进入 Scenario 舞台空间（哪怕文字描述是舞台入口或院子）的初始化与开场阻塞演出，一律写在 Scenario 自身的 `on-scenario-enter` 中。
- **动作外扣减冷静必须指明对象**：回合规则与结算回调中没有“当前行动者”，直接调 `spend-composure!` 会报错，必须写明 `(spend-actor-composure! 'player 1)`。`req-item` 投入的物品由执行器自动扣除，outcome 中**切勿重复调用 `remove-item!`**。

---

## 四、 角色声明、配音资产同步与双语规范

### 1. 普通配角就近声明
- 普通配角在所属故事文件内就近声明：`(speaker "稳定身份" :name "显示名" :appearance '成年男性)`，再传给 `line`。有专属肖像时自动优先，无专属肖像时按 `:appearance` 取剪影，不维护中央人名册。

### 2. 带配音对白（`line` 第三参数）的资产同步要求
`line` 的第三个参数是稳定的配音 ID。**只要新增、修改或删除带配音 ID 的对白，必须同步处理以下三处资产**（除非用户明确指示跳过）：
1. `角色库/配音/清单/*.json`（修改台词时保留 ID，更新 `text` 使其逐字匹配新台词）。
2. `角色库/配音/候选/`（重新生成并验收 `.wav`，旧音频不得冒充新台词成品）。
3. `UnityClient/Assets/Resources/Voices/`（同步替换或删除 `.wav` 及其 `.meta` 文件，并用配音 ID 全局搜索仓库确认无遗漏引用）。

### 3. 双语 `(tr 中文 英文)` 与 Schemy 语法边界
- **不要在顶层缓存玩家可见翻译结果**：`(define x (tr ...))` 只在脚本加载时求值一次。按现有渲染求值和语言切换机制组织文本，不把所有翻译调用机械地限制为某一种 lambda 写法。另外，`:button-text` 直接写中文受控词（如 `"交谈"`），由客户端统一翻译，不要包 `(tr ...)`。
- **Schemy 语法限制**：不支持 `case`、`call/cc` 以及方括号 `[...]`（一律使用圆括号 `(...)`）。完整语言边界见 `schemy-master/AGENTS.md`。
## 条件互动（不用于主线阶段推进）

`node` 可声明 `:interaction-activity "码头搬运"`；`工作` 等包装函数可用 `(with-interaction "码头搬运" 行动数据)` 附加活动。活动标识是稳定语义，不按显示标题匹配。行动只声明活动，不直接枚举人物。公共调度按模块清单中“互动”角色汇总人物的 `'interactions`，按声明顺序一次选择一段；主线阻塞表现与送医优先，不补播被跳过的工作反应。

人物模块在加载时创建 `(make-triggered-event id timing activity eligible? effect cooldown limit chance)`。`timing` 使用“行动结算”“进入地点”“房间睡前”“新日开始”；`activity` 为空字符串表示不过滤活动。谓词接收上下文关联表：`timing`、`activity`、`result`、`place`（键为字符串，按时机取需要的字段）。判定结果为“好／中／坏”，Instant 为“即时”。判断只读状态；`effect` 返回 `#t` 表示确实发生，返回 `#f` 表示没有合适片段。冷却按世界日计算，次数上限0表示不限，概率为0—100。概率只在实际时机检查，不在渲染或节点构造时抽取。

同一个人物的互动对象必须稳定复用，不能每次渲染重新创建。人物的 `save/load!` 通过 `interaction-save`、`interaction-load!` 保存和恢复自身对象，不另建存档。模块间互动 Id 唯一。此接口处理结算后的回应，不回改判定概率；也不会因为满足日期条件自动进入 Encounter。需要延期见面的后续使用下面的待发生互动，不把持续条件互动反复删除和重建。


### 触发事件的创建与结束

固定的条件回应和某次经历产生的后续，统一使用 `make-triggered-event`。默认持续检查条件；达到非零次数上限或结束条件成立后永久结束，暂时不满足 `eligible?` 只是等待。构造器末尾可附加 `(list expires? locations factory)`：结束谓词、允许显示入口的地点、入口工厂。`factory(place, finish!)` 返回行动节点，结算通过 `(finish! effect)` 执行；入口不会自动播放。多地点节点 Id 必须按地点区分，Anchor 明确声明。

动态后续使用 `(make-event-pool id definitions)` 管理，定义表为 `(("事件名" factory) ...)`；定义工厂接收实例 Id 与可存档数据，返回同一种触发事件。来源结算时调用 `(集合 'schedule! "事件名" "来源场次" 数据 概率)`，只有抽取成功才真正创建实例，未中也记录来源以免重抽。不同场次可以各自产生实例，不是修改一个预先存在的占位对象。

实例的数据保存最早日期、来源场次等事实；条件与结束谓词从这些事实推导。留酒应明确在下一场聚餐开始或期限届满时结束，不能仅用“当前不方便见面”的条件代替过期。完成或过期实例从集合移除，来源去重历史单独保留。集合与持续事件都通过人物模块原有的 `interaction-save/load!` 进入同一存档；动态实例读档时按定义和数据重建，不序列化过程闭包。
