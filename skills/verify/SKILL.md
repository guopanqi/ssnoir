---
name: verify
description: 修改 SSNoir 的代码或可执行内容后决定验证范围、用户要求构建或测试、为疑难 bug 选择复现手段，或准备修改 GameTester 时使用。纯说明文档、历史记录、方案文字和只读回答不使用。
---

# 验证到什么程度

选择能覆盖本次主要风险的**最小充分验证**；静态审阅已经足够时，可以不运行命令。把验证放在一次
改动的整合点，不要每改几行就重复构建。`error` / `throw` 只有在相关路径被加载或执行时才有价值，
不能把“下次运行会暴露”当成已经验证。

## 按你改了什么来选

| 改动 | 做什么 |
|---|---|
| `.scm` 纯文案、明确的局部数值调整 | 仔细审阅 diff；用户未要求时通常不运行命令 |
| `.scm` 可执行结构：节点组装、新 DSL 调用、规则、Clock、save/load、encounter，或大段重构 | `./run --validate` |
| `Content/scripts/engine.scm` | `./run --validate`；若同时改了 C# bridge，再按下一行构建 |
| `Engine/Runtime/**`、`TerminalApp/**` | `dotnet build TerminalApp/ssnoir.csproj` |
| `schemy-master/**` | `./schemy-master/build-unity-plugin.sh`（同时重建 Unity 使用的 DLL） |
| 判定分布 | `./run --test-odds` |
| 交锋的回合规则、目标结算、入场与结束路径 | `./run --playtest "<入场表达式>"`（见下节） |
| 存档契约 | `./run --test-saveload` |
| `UnityClient/**` 独有代码 | 按下节「Unity C# 的编译验证」拿真编译结果；拿不到就静态审阅并交给用户，不用 `TerminalApp` 的构建冒充 |
| 较大的非 Unity UI 布局 / 交互改动，或用户明确要求看实际效果 | 运行并检查相关客户端；构建通过不等于视觉或交互正确。Unity 交互按下节处理 |
| 追一个具体的疑难 bug | 用能复现它的**最小**手段，别顺手做全量校验 |

用户明确要求某项验证时执行它，除非环境不支持；此时说明限制，不要用不等价的检查替代后声称已验证。

## `--validate` 只覆盖「此刻真的在世界树里的东西」

它加载 `world/world` 并把渲染树建一遍，所以**被剧情门槛挡住的地点根本没走到**——老街酒馆挂在
`story-stage >= 1` 后面，新档是 0，写在那儿的内容一行都不会被解析，而它照样打印「全部通过」。
交锋同理：`--validate` 只保证载入并渲染一次，回合规则要用 `--playtest`。

**想确认某段内容真被覆盖，就临时把它改成非法再跑一次。** 比读代码猜覆盖范围可靠得多：

```bash
# 例：把一条 note 的正文清空（三项全空是非法的），validate 应当当场报错
./run --validate 2>&1 | grep -iE "error|exception"
```

报错说明这段确实在被解析，改回去即可；**没报错说明它压根没被走到**，别当成「通过」。
破坏点要选一个只有该内容会触发的约束——选错了会自己骗自己（比如一条带标题的标注，清空正文
并不违反「至少一项非空」）。

## 交锋试跑（`--playtest`）

`--validate` 只保证一场交锋**载入并渲染一次**；它碰不到回合规则、目标结算、同伴中途入场
和结束路径——那些只有真的把一场打完才会跑到。改了交锋的可执行结构，用这个：

```bash
./run --playtest "(dock-collapse 'debug-enter!)" --runs 5 --growth 1 --seed 7
./run --playtest "(dock-collapse 'debug-enter!)" --verbose      # 逐回合看牌面
./run --playtest encounters/combat                              # 还没接进城市的交锋直接载入
```

参数写 Scheme 表达式时，从世界那头真的走一遍入场，返回值会经过世界模块结算与人物写回；
写场景名则直接载入。`--seed` 固定随机数，同一局可以原样重放。

它打出的是**逐回合流水**：谁的哪颗骰放到哪张卡、判定档、效果条、对白与告示卡，最后一行是
`end-encounter` 交回来的值。读这份流水能一眼看出文案顺序、效果条措辞和结算路径对不对。

默认打法是"每颗骰投给准备值最高的卡"，**是天花板不是玩家**：它从不浪费骰、也从不做错误的
分诊。用它看"改了数值以后上限动了多少"，不要用它回答"这场难不难"——那个只有真人玩了才算数。

驱动本身在 [TerminalApp/src/Playtest/EncounterDriver.cs](../../TerminalApp/src/Playtest/EncounterDriver.cs)：
起局、列合法投骰、执行、结束回合、读结果。**它的观测只走渲染树**，不 Eval 脚本内部变量——
某个状态如果这里读不出来，说明玩家也读不出来。要给某一场写专门的打法或断言，基于它写一个
一次性脚本，别往 `GameTester` 里塞。

## Unity C# 的编译验证

`dotnet build TerminalApp/ssnoir.csproj` 覆盖不到 `UnityClient/Assets/Scripts/**`——那些源文件
根本不在那个工程里。要拿到它们的编译结果，按顺序试：

**1. 编 Unity 生成的 csproj（首选，编辑器开着也能用）**

```bash
OUT=<scratchpad>/unitybuild
dotnet build UnityClient/SSNoir.Client.csproj -nologo -v q \
  -p:OutputPath="$OUT/bin/" -p:BaseIntermediateOutputPath="$OUT/obj/"
```

必须重定向那两个输出路径：csproj 默认写进 `UnityClient/Temp/`，那是运行中的编辑器在用的目录。

用之前先确认这份 csproj 还作数——它是 Unity 生成的产物（已被 gitignore），**不会**自己跟上
新增/删除的文件或改过的 `.asmdef`。

只是文件集合对不上（你新增或重命名了 `.cs`）时，**直接改那份 csproj 的 `<Compile Include>` 列表
就行**：它已被 gitignore，Unity 下次自己会重新生成，改它不影响仓库也不打扰编辑器。这比往下走
快得多——尤其编辑器开着时第 3 条根本用不了。csproj 不存在，或 `.asmdef` 变了，才真的往下走。

它检查的是当前构建目标那一套宏（现在是 WebGL + `UNITY_EDITOR`）。别的平台分支下的代码、
以及一切非编译问题（序列化、Inspector 引线、`.meta`、资源引用、Play Mode 行为），它都不管。

**2. 现成的编译证据**

编辑器可能已经自己编过了。`UnityClient/Library/ScriptAssemblies/SSNoir.Client.dll` 的时间戳
晚于改动的源文件，就说明编过且成功；再从 `~/Library/Logs/Unity/Editor.log` 里 grep `error CS`
确认那一轮没报错。只读，不必碰编辑器。

**3. batchmode（只在编辑器没开时）**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics \
  -projectPath "$(pwd)/UnityClient" -logFile - | grep -E "error CS|Compilation failed"
```

编辑器开着就用不了：Unity 对 `Library/` 是独占锁，会直接报 "Multiple Unity instances cannot open
the same project"。先 `pgrep -lf "Unity.app/Contents/MacOS/Unity"` 看一眼。即使没开，冷启动会跑
一次完整资源导入，几分钟起步——所以它是兜底，不是默认。

**4. 都不行**：静态审阅，明说"未编译验证"，请用户切回 Unity 触发一次编译。

## Unity 验证边界

- 默认不要为了验证而调用 `computer-use` 操作 Unity。Unity 的场景状态、Play Mode 和焦点不稳定，自动操作的成本与证据质量通常不匹配。
- 用户没有明确要求 Codex 操作 Unity 时，完成静态审阅并提供简短的人工核验路径，让用户在 Unity 中确认编译、画面和手感。
- 只有用户明确要求 Codex 使用 Unity 做实际交互验证时，才尝试用 `computer-use` 控制编辑器；操作前先保护未保存的场景和当前 Play Mode 状态。
- 编译结果按上一节取；那几条路都不必操作编辑器。编译通过不等于画面和手感对，那一头始终由用户在 Play Mode 里确认。

## GameTester

`GameTester` 一般用来测试：**稳定的底层引擎 / DSL 契约**、**高风险且容易静默损坏的基础功能**、
**用户明确要求覆盖的行为**。

不用来测试：剧情流程、内容节点名称、数值平衡、完整游玩弧线。这些跟着内容天天变，固化成测试只会让
每次改内容都得顺手改测试。如果要临时测试可以加，测试完成后删除

## 报告结果

只报告实际运行过的命令及其结果。没有运行就写“未运行”，不要把静态审阅称为“已验证”；
若验证失败，区分本次改动导致的问题与已经存在的问题。
