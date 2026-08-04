# SSNoir 架构总览

SSNoir 是一个**数据驱动的卡片式叙事游戏**:游戏内容用 Scheme 脚本描述,
C# 引擎把脚本求值成一棵节点树,再由前端渲染成可交互的卡片。
核心理念:**一份引擎逻辑,内容与表现都可替换**。

---

## 1. 顶层模块与依赖方向

```
        Content/ (Scheme 数据)          schemy-master/ (fork 的解释器源码)
              │ 被加载                         │ build → Engine/Plugins/schemy.dll
              ▼                                ▼
        ┌──────────────────────────────────────────────┐
        │  Engine/  (共享 C# 核心,不依赖任何前端)        │
        │  Core: GameState / SceneManager / Team / ...   │
        │  Scripting: SchemeInterpreter / NodeConverter  │
        └──────────────────────────────────────────────┘
              ▲                                ▲
              │ 引用同一份 Engine 源码          │
        ┌───────────────┐              ┌────────────────────┐
        │ TerminalApp/  │              │ UnityClient/        │
        │ Raylib UI     │              │ IMGUI UI(主力)     │
        │ (测试/兜底)   │              │ 3D 场景 + anchor    │
        └───────────────┘              └────────────────────┘
```

| 模块 | 职责 | 关键点 |
|---|---|---|
| `Engine/Runtime/Core` | 纯游戏逻辑:状态、场景、动作结算、存档 | 不依赖任何 UI 框架;靠 `throw`/`assert` 对脏数据中断 |
| `Engine/Runtime/Scripting` | Scheme ↔ C# 桥:解释器封装、节点转换、原生函数 | `NodeConverter` 把 s-expr 翻成 `GameNode` |
| `Content/` | 游戏数据:场景、规则、对白(`.scm`) | LLM 友好;ID/名称可直接用中文 |
| `schemy-master/` | fork 的轻量 C# Scheme 解释器 | 改它后必须 `build-unity-plugin.sh` 重建 dll |
| `TerminalApp/` | Raylib 前端 + 自测/校验工具(`GameTester`) | 编译时 `Include` 整个 `Engine/Runtime/**` |
| `UnityClient/` | IMGUI 前端,投射到 3D 空间 anchor | Engine 源码 + `Plugins/schemy.dll` |

**依赖只有一个方向**:前端 → Engine → (Content 数据 / Schemy)。Engine 永远不反向依赖前端。

> 注:Engine 不是独立编译的 DLL,而是**以源码形式被两个前端各自编译进去**
> (TerminalApp 走 `<Compile Include="..\Engine\Runtime\**\*.cs" />`,Unity 直接放在工程内)。

---

## 2. 核心数据流

### 渲染:脚本 → 节点树 → UI
```
(get-render-data)            ; 当前场景解释器求值,返回 s-expr 节点列表
   → NodeConverter.Convert   ; s-expr → GameNode 树(含 Clocks / Requires / Resolve)
   → SceneManager.LatestSnapshot (PresentationSnapshot:节点树 + 队伍/库存/声誉快照)
   → 前端 Adopt 快照后绘制卡片
```
节点都是**每次重新求值**的(Scheme 里 `define` 成 lambda),所以渲染树永远反映最新状态。

### 动作:执行 → 结算 → 表现
```
前端点击节点 → SceneManager.ExecuteAction(node, slots)
   → 校验资源槽(骰子/物品) → 跑 Resolve(Instant / Roll)
   → outcome.Effect 调用 Scheme lambda 改状态
   → (on-action) 规则检查 → RebuildRenderTree
   → 返回 ActionReport(含 PresentationHints / 旁白 / 聚光)
   → 前端按 hints 播放表现,再 Adopt 新快照
```

---

## 3. 状态模型(重要:有清晰边界)

状态分三类,**各有其家,不要混用**:

| 状态 | 存放 | C# 读写 | Scheme 读写 |
|---|---|---|---|
| 纯全局键(章节、声誉、剧情 flag) | `GameState._states` 字典 | `Get/Set(key)` | `(get-global k)` / `(set-global! k v)` |
| 队伍 / 库存 / 成长 / 骰子 | `GameState.Team`、`GameState.Inventory` 类型化对象 | 直接访问对象 | `__` 原生桥 + `engine.scm` 包装(如 `party-health`、`item-count`、`growth-level`) |
| 地点本地状态(任务阶段、门是否开) | 各场景 `.scm` 里的 `define` | — | 地点自己管,存档随 `world-save` 序列化 |

约束:
- `get-global/set-global!` **只管纯全局键**,不要拿它读写队伍/库存(那条字符串魔法键的旧路已删除)。
- encounter 不直接写 global 通知 world,而是 `start-encounter` 传 callback、`end-encounter` 报结果。详见 [skills/write-scheme/SKILL.md](../skills/write-scheme/SKILL.md)。

### 场景切换
- `SceneManager`:world 是一个常驻解释器;每个 encounter 是临时新建的解释器。
- 切场景走**显式** `GoToLocation(name)`(不再由写 `location` 全局键隐式触发)。
- `world` 与 `world/world` 视为世界场景,其余皆 encounter。

### 存档
`SaveManager`(JSON):纯全局键 + 队伍 + 库存 + `(world-save)` 返回的 Scheme 世界状态。
读档强制回到 world 模式并显式重建渲染树。

---

## 4. “我想改 X,该看哪里”

| 想做的事 | 入口文件 |
|---|---|
| 加/改一个游戏地点、动作、对白 | `Content/scenes/**.scm` + [skills/write-scheme/](../skills/write-scheme/SKILL.md) |
| 加一个 Scheme 能调用的引擎能力 | `Engine/Runtime/Scripting/NativeFunctions.cs` + `Content/scripts/engine.scm` 包装 |
| 改节点的 DSL 结构(node/resolve/clock 语法) | `NodeConverter.cs` + `engine.scm` |
| 改动作结算 / 骰子 / 回合逻辑 | `Engine/Runtime/Core/SceneManager.cs`、`TeamState.cs` |
| 改存档格式 | `SaveManager.cs` + `SaveData.cs` |
| 改 Unity 界面 | `UnityClient/Assets/Scripts/Runtime/IMGUI/**` + [UnityClient/AGENTS.md](../UnityClient/AGENTS.md) |
| 改终端界面 | `TerminalApp/src/Rendering/**` |
| 改解释器本身的行为 | `schemy-master/src/**`(改完必须重建 dll)+ [schemy-master/AGENTS.md](../schemy-master/AGENTS.md) |
| 跑内容校验 / 自测 | `dotnet run --project TerminalApp -- --validate`(见 `GameTester`) |

---

## 5. 关键设计决策(沿用至今)

| 决策 | 选择 | 原因 |
|---|---|---|
| 状态存活 | Scheme 解释器持续运行 | `set!` 语义干净,地点本地状态自然保留 |
| 节点动态性 | `define` 成 lambda,渲染时才求值 | 每次都读最新状态 |
| 规则系统 | `define-rule` 声明式注册,`on-action` 统一遍历 | 规则相互独立 |
| 场景切换 | 每场景一个 `.scm`,encounter 用临时解释器 | 局部状态自然丢弃 |
| 前端可替换 | 引擎与内容不感知 UI;Terminal/Unity 并存 | 解释器和 `.scm` 完全复用 |
| 错误处理 | 数据/逻辑错误直接 assert 中断 | 最大化健壮性,尽早暴露内容配置错误 |

---

## 6. 已知限制与待办

解释器改造方向、表现/状态同步的遗留问题见 [TODO.md](../TODO.md)。
写 `.scm` 内容看 [skills/write-scheme/SKILL.md](../skills/write-scheme/SKILL.md);解释器能力总览见 [schemy-master/AGENTS.md](../schemy-master/AGENTS.md)。
