# 项目验证指南

本指南集中说明项目的验证手段、适用范围与证据边界。通用原则见
[根 AGENTS.md](../AGENTS.md#测试验证)；需要选择具体检查、确认覆盖范围或修改 `GameTester` 时，按相关章节查阅。
纯说明文档、历史记录和方案文字的修改通常只需审阅 diff 与检查引用，无需运行游戏或构建。
以下命令除另有说明外，均从仓库根目录运行。

## 按你改了什么来选

| 改动 | 做什么 |
|---|---|
| `.scm` 纯文案、明确的局部数值调整 | 仔细审阅 diff；用户未要求时通常不运行命令 |
| `.scm` 可执行结构：节点组装、新 DSL 调用、规则、Clock、save/load、scenario，或大段重构 | `./run --validate` |
| `UnityClient/Assets/Resources/Content/scripts/engine.scm` | `./run --validate`；若同时改了 C# bridge，再按下一行构建 |
| `Engine/Runtime/**`、`tools/content-validator/**` | `dotnet build tools/content-validator/SSNoir.ContentValidator.csproj` |
| 节点 `Id` / 显示 `Name`、节点查找与分组钟展示契约 | `dotnet run --project tools/content-validator/SSNoir.ContentValidator.csproj -- --test-node-identity`；覆盖同名节点、重复 ID、按 ID 导航/执行和照相馆分组钟 |
| 骰池新一手、消费与回合重置版本契约 | `dotnet run --project tools/content-validator/SSNoir.ContentValidator.csproj -- --test-dice-reset`；覆盖同值重发、未花骰覆盖、伤势封锁、刷新/消费/场景切换和日终发骰 |
| `schemy-master/**` | `./schemy-master/build-unity-plugin.sh`（同时重建 Unity 使用的 DLL） |
| 判定分布 | `./run --test-odds` |
| Scenario的回合规则、目标结算、入场与结束路径 | 审阅 diff；**只有用户要求试跑时**才 `./run --playtest`（见下节） |
| 存档契约 | `./run --test-saveload` |
| `UnityClient/**` 独有代码 | 按下节「Unity C# 的编译验证」拿真编译结果；拿不到就静态审阅并交给用户，不用内容校验器的构建冒充 |
| 较大的非 Unity UI 布局 / 交互改动，或用户明确要求看实际效果 | 运行并检查相关客户端；构建通过不等于视觉或交互正确。Unity 交互按下节处理 |
| 追一个具体的疑难 bug | 用能复现它的**最小**手段，别顺手做全量校验 |

用户明确要求某项验证时执行它，除非环境不支持；此时说明限制，不要用不等价的检查替代后声称已验证。

## 必须处理节点的契约

`./run --test-required-before-rest` 检查 `:required-before-rest` 的树收集、节点出现/消失、多个事件独立解除、休息卡同步、C# 与 Scheme 换日兜底、错误声明、冷读档及Scenario回合隔离。修改这项 DSL、收集或换日入口时执行；不要用手工注册阻塞来准备测试盘面。

## 第一幕内容回归

`./run --test-first-act` 覆盖新档开场、三份工作直接开放、露西与艾迪的经历、赴约成本、第三天的酒馆口信、第五天彩排后的雨夜请求、缺席彩排仍进入第二幕、雨夜与酒馆结算回写，以及冷读档。精确结局用固定盘面执行真实结算，不把基线打法当作Scenario手感证据。Unity 画面与3～4回合节奏仍须实际试玩。

## `--validate` 只覆盖「此刻真的在世界树里的东西」

它加载 `world/world` 并把渲染树建一遍，所以**被剧情门槛挡住的地点根本没走到**——老街酒馆挂在
`story-stage >= 1` 后面，新档是 0，写在那儿的内容一行都不会被解析，而它照样打印「全部通过」。
Scenario同理：`--validate` 只保证载入并渲染一次，回合规则要靠审阅，或由用户叫你跑 `--playtest`。

确认覆盖范围时，应确认目标内容确实被加载、目标路径确实被执行；未走到的内容不能算通过。
必要时可以用临时故障注入排查覆盖范围：选择仅由目标内容触发、且确定违反约束的修改，
运行后立即恢复。它是排查手段，不是每次内容修改的默认步骤。

## Scenario试跑（`--playtest`）——**用户要求才跑**

**不要在改完Scenario后自动跑试跑。**默认只 `./run --validate`；C# 那头没动时用
`dotnet run --no-build --project tools/content-validator/SSNoir.ContentValidator.csproj -- --validate`（省掉一次构建）。
Scenario的手感最终由用户自己手玩确认，基线打法替代不了；每次顺手跑一轮模拟只是在拖慢编写循环。

同样禁止的是**为了让驱动跑通而临时改内容**：注释掉退出卡、往世界模块里塞 `debug-enter!` 钩子、
改完再改回去——这些来回比它验证到的东西贵得多。

`--validate` 只保证一场Scenario**载入并渲染一次**；它碰不到回合规则、目标结算、同伴中途入场
和结束路径。**用户明确要求试跑**，或者要看一次数值改动把上限推到哪儿时，用这个：

```bash
./run --playtest "(dock-collapse 'debug-enter!)" --runs 5 --growth 1 --seed 7
./run --playtest "(dock-collapse 'debug-enter!)" --verbose      # 逐回合看牌面
./run --playtest scenarios/combat                              # 还没接进城市的Scenario直接载入
```

参数写 Scheme 表达式时，从世界那头真的走一遍入场，返回值会经过世界模块结算与人物写回；
写场景名则直接载入。`--seed` 固定随机数，同一局可以原样重放。

它打出的是**逐回合流水**：谁的哪颗骰放到哪张卡、判定档、效果条、对白与告示卡，最后一行是
`end-scenario` 交回来的值。读这份流水能一眼看出文案顺序、效果条措辞和结算路径对不对。

默认打法是"每颗骰投给准备值最高的卡"，**是天花板不是玩家**：它从不浪费骰、也从不做错误的
分诊。用它看"改了数值以后上限动了多少"，不要用它回答"这场难不难"——那个只有真人玩了才算数。

它还会**先用光一切不吃骰的卡**。所以带「走开 / 就此收手」这类免费退出卡的Scenario，驱动必定第一手
退出、直接回传那条路径的结果。这不是内容写错了，是驱动的打法使然——看见这种流水不要去改内容。

驱动本身在 [content-validator/Playtest/ScenarioDriver.cs](content-validator/Playtest/ScenarioDriver.cs)：
起局、列合法投骰、执行、结束回合、读结果。**它的观测只走渲染树**，不 Eval 脚本内部变量——
某个状态如果这里读不出来，说明玩家也读不出来。要给某一场写专门的打法或断言，基于它写一个
一次性脚本，别往 `GameTester` 里塞。

## Unity C# 的编译验证

`dotnet build tools/content-validator/SSNoir.ContentValidator.csproj` 覆盖不到 `UnityClient/Assets/Scripts/**`——那些源文件
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

- 3D场景与动画的日常设计循环在 Blender 完成。由 Agent 审查朝向、接触、穿插、首尾姿态、描线跟随和循环；Unity 不作为每轮修改的前置检查。
- 普通内容修改默认以 Blender / 共享 C# / 内容校验器及发布物检查结束；内容成立或写入 Unity 资源目录，不自动触发 Unity 核对。独立 Stage、Portal、机位、通行空间沿用已验证契约时也遵守此规则。
- Unity 核对按证据缺口触发：新增或修改导出/导入契约（轴向、单位、FOV、组件绑定、序列化）、修改 Unity 专有渲染（Shader、URP、光照）、出现 Blender 与游戏不一致，或用户明确要求。选最小输入和代表路径核对一次；同一契约校准后，普通数值和内容调整不逐次重复。发布版本的抽样试玩可合并进行。
- 需要引擎证据时才选 [正式预览工具](citybox-unity-preview/README.md#动画固定帧)，不默认全量 `--verify-play`。开播、跳过、返回机位、恢复操作及重入等功能流程，默认提供简短人工路径；没有实际运行就说明未运行，不把它们扩成每次内容交付的强制工序。
- Blender 正确而 Unity 不一致，或预览进程启动/退出卡住，归为导出、导入、运行时或工具的功能缺陷，单独用最小复现定位。修共同管线，再复核受影响路径；不靠反复改资产、启动预览或录屏补救。用户明确要求自动交互验证时再展开。

- 默认不要为了验证而调用 `computer-use` 操作 Unity。Unity 的场景状态、Play Mode 和焦点不稳定，自动操作的成本与证据质量通常不匹配。
- 用户没有明确要求 Codex 操作 Unity 时，完成静态审阅并提供简短的人工核验路径，让用户在 Unity 中确认编译、画面和手感。
- 只有用户明确要求 Codex 使用 Unity 做实际交互验证时，才尝试用 `computer-use` 控制编辑器；操作前先保护未保存的场景和当前 Play Mode 状态。
- 编译结果按上一节取；那几条路都不必操作编辑器。编译通过不等于画面和手感对，那一头始终由用户在 Play Mode 里确认。

## 线绘舞台

新增图形、属性动画或音频生命周期时，执行 `dotnet run --project tools/content-validator/SSNoir.ContentValidator.csproj -- --test-theatre`。
它实际加载并执行路灯与雨夜来访样板，检查解析和时间语义；Unity 侧仍需客户端编译结果。
仅调整线绘演出的解析、时间语义或状态推进时，优先复用正式 Engine C# 的无界面验证；需要判断实际绘制、构图和视觉节奏时，才按需使用 [Unity 舞台预览工具](theatre/README.md) 导出 GIF、对白索引及可疑时刻原图，不因普通参数微调强制启动 Unity。
这是独立工程中的真实 GPU 绘制，不要求关闭主工程；不替代字幕 UI、实际声音和玩家点击验收。
离屏 GPU 检查、范围与限制见 [线绘舞台](../docs/制作/线绘舞台.md#试演与验证)。

## GameTester

`GameTester` 一般用来测试：**稳定的底层引擎 / DSL 契约**、**高风险且容易静默损坏的基础功能**、
**用户明确要求覆盖的行为**。

不用来测试：剧情流程、内容节点名称、数值平衡、完整游玩弧线。这些跟着内容天天变，固化成测试只会让
每次改内容都得顺手改测试。如果要临时测试可以加，测试完成后删除

## 第二幕V0.2独立Scenario与结果回归

`dotnet run --project tools/content-validator/SSNoir.ContentValidator.csproj -- --test-second-act-v02`覆盖独立双目标Scenario、Scenario回合不推进世界日、截止日、人物在场、自然情感场景、衣箱资源与旧状态归一。精确结果只证明分支和资源契约，不代表真实概率下的难度或Unity演出已验证。

## 镜头过渡与场景空间

焦点过渡算法或受限空间机位修改时，按 [镜头过渡检查](camera-review/README.md) 在 Blender 中运行共享 C# 策略并检查几何。日常不启动 Unity。路线安全与画面可读性分开判断；地面兴趣点遮挡不等于主体遮挡。

调整 `CameraTravelBounds_<地点>` 的尺寸或机位时，默认只做 Blender 几何检查；向游戏交付时走 CityBox 构建、契约及发布物检查，不因此额外启动 Unity。新增或修改绑定/换算实现、出现两端差异时，才用 `tools/camera-review/verify_unity.py` 做最小导入核对；目前覆盖巷战 8 台机位，不覆盖 Play Mode。共享同一份 C# 已证明的曲线计算不在 Unity 中重复验证；轴向、FOV 和导入绑定属于另一层证据。

可移出 Unity 的是确定性计算和数据契约：复用正式 C# 源文件运行命令行检查，或在 Blender 检查源几何。不要在 Python、测试工具里重写正式算法来冒充等价验证。GUI 实际排版、Shader/GPU 输出、Unity 导入后的曲线及播放行为不能靠纯计算证明；只有本次确实涉及它们时才付出对应检查成本。
