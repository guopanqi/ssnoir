# Web 共同执行路径：性能与语义对照

基线：`a57ff5d8`，最新基线 Web CI [41806759784](https://github.com/guopanqi/ssnoir/actions/runs/41806759784) 成功。
日期：2026-11-04（新加坡时间）。本轮不修改 Unity 或任何原始 Scheme 内容。

## 根因与取舍

LIPS 的解释式求值在每次函数调用中创建 continuation/env，宏处理、Promise 检查与循环检测反复遍历 Pair。默认发布包还将 generator、对象展开等转换为高成本兼容代码。CPU profile 的热点包括 `_objectSpread`、列表 clone、has_cycle 的 visit、resolve_promises 的 traverse 和求值循环。已有 get-kwarg 优化把同机完整建树从约 7.0 秒降至 5.4 秒，仍无法支撑交互。

试验过直接使用现代 JS 源、浅列表拼接和原生 node 构造；改善均不足，未将这些尝试加入正式运行时。最终保留原始所有 Scheme 构造器和规则，使用编译式 VM：宏展开和闭包分析在载入时完成，调用执行字节码。不添加世界树缓存，不跳过入场或读档模块。

BiwaScheme 0.8.3 原候选的 TopEnv 串扰被 factory 级隔离解决：每个世界/Scenario/候选存档均独立拥有全局环境、核心环境、编译器、宏和执行栈；跨边界只统一值表示。固定依赖源 SHA-256，构建时自动生成内核，保留 MIT 许可，不执行其浏览器自动启动部分。

适配局部定义与参数求值顺序的契约见 `Game/tests/compiled-session.ts-test.mjs`。局部初始化保持作者原顺序；操作符先求值，参数从左至右；尾调用和异步拒绝不丢副作用、不重复执行。GameScriptSession 的动态 load-file 使用独立求值栈暂停/恢复，不再正则拆分 Scheme 文件。旧 LIPS 只供历史底座探针，不在 Web 游戏维护另一套规则。

## 同环境端到端

Chromium 140.0.7339.16，Node 24.22.1，1440×850，seed 42；单次配对。基线通过 `git archive a57ff5d8 Game` 获取，使用同一依赖和原始内容。计时从发起按钮点击前开始，到 busy=false 且两帧绘制后结束，包含 Playwright 的点击等待。首次进酒馆保留两次权威建树和原入场副作用。

| 操作 | 基线 ms | 本轮 ms |
|---|---:|---:|
| 首次进家 | 3780 | 115 |
| 返回世界 | 3640 | 82 |
| 重入家 | 3623 | 98 |
| 首次进酒馆 | 14018 | 161 |
| 酒馆返回世界 | 8753 | 99 |
| 重入酒馆 | 8414 | 100 |
| 保存 | 1377 | 80 |
| 读取基线旧档 | 17849 | 311 |

[before.json](before.json) / [after.json](after.json) 包含环境、每项耗时、8 份可见状态、完整存档和各阶段日志。两版 snapshots 与 saved 经 `assert.deepEqual` 完全相同，Scheme datum 逐字相同。本轮页面实际读入的是基线生成的旧存档。截图仅保留布局证据；当前容器缺少中文系统字库，截图中的方框不作为中文排版质量证据。

## 验证范围

- `npm run verify`：类型检查、生产构建、69 项 Web/VM 契约。
- `npm run smoke:web`：新档→清晨→酒馆工作/花骰→存档→合法睡觉→损坏存档拒绝且旧世界不变→有效恢复。
- `npm run test:route`：整合最新 main 后新档至第十五天；第八天彩排及失败收场；Scenario 中途撤销、原骰池恢复；第十三天追踪失败；第十四天继续接案、追车中拒绝坏档仍可继续、失败送医至第十五天重试并恢复原城市档。
- 扩大运行时检查：121 项中 113 通过，8 项失败。相同 8 项均在未修改的 a57ff5d8 基线第二/三幕内容测试复现（旧测试的名称/剧情条件与现行内容不符），不声称全量旧测试通过。
- 尚未证明所有后续章节分支、所有设备性能或完整 P3 演出。

## 复现

在 Game 目录运行 `npm ci`（自动生成隔离 VM），然后 `npm run verify`、`npm run smoke:web`、`npm run test:route`、`npm run bench:runtime`、`npm run bench:web`。

对基线归档运行同一个 `bench-web-interactions.mjs`，设置 `SSNOIR_BENCH_ROOT` 指向归档的 Game 目录；设置 `SSNOIR_BENCH_OUTPUT` 选择 JSON 输出。对新版本设置 `SSNOIR_BENCH_LEGACY_SAVE` 指向基线 JSON，可以要求相同 envelope 并通过 UI 读取旧档。`SSNOIR_BENCH_ASSERT_BUDGET=1` 启用 CI 宽松上限（恢复 4 秒、其余 2 秒），用于阻止多秒阻塞回归，不作为设备性能承诺。
