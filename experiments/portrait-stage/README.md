# portrait-stage — 立绘舞台表演实验组

共同目的：研究 2D 立绘人物在舞台上的表演力——
对白节奏、走位、停顿、姿势切换、灯光道具，以及人物画风与舞台的融合。
人物素材是各实验独立的舞台素材，不替换正式游戏立绘。

| 实验 | 一句话 |
| --- | --- |
| [portrait-stage-lab/](portrait-stage-lab/) | 主实验：七组 A/B 表达力短戏 + 素台六幕 baselines + third 合并试演 + world-stage 人物比较 |
| [line-stage-study/](line-stage-study/) | 线构舞台：Claude 酒店门前 / 路灯线绘剧场、码头仓库新场景、N2/C1 人物画风融合 |
| [portrait-theatre-lab/](portrait-theatre-lab/) | 各模型单文件剧场合集（Claude / kimi / musespark 原型与归档），`portrait-theatre-story-claude-sonnet-5-5.html` 为基准剧本对照源 |
| [portrait-theatre-mimo-v2.6-flash-free/](portrait-theatre-mimo-v2.6-flash-free/) | MiMo 模型编排的六幕纯色舞台版本，对白与基准剧本逐字一致 |
| [portrait-theatre-claude/](portrait-theatre-claude/) | Claude Sonnet 5.5 噪点黑色线绘世界：19 章故事剧场（地图选点 → 线绘舞台），src 模块化 + `node build.mjs` 构建单文件，接手见其 AGENTS.md |
| [sc02-tavern-debut/](sc02-tavern-debut/) | **SC-A02 固定开场**（酒保与夜莺初次亮相）：线绘场景逐笔成形 + 灯光逐点亮起，人偶姿势**定格切换**（立绘式，无慢速关节补间），节奏收紧的单场实验 |
| [neon-rig-three/](neon-rig-three/) | **本次独立 Three.js 实验**：正式 Neon 描线路径的自动关节变形；醉酒出门与街角追逐，对比关键立绘切换与加权骨骼驱动 |
| [neon-cutout-puppet/](neon-cutout-puppet/) | **独立分层人物实验**：13 块硬质 Cutout 描线部件、醉步/扶墙/奔跑与生成关键姿势对比、静态及运动截帧验证 |
| [sc-a02-bar-nightingale/](sc-a02-bar-nightingale/) | 线绘酒馆固定开场：酒保擦杯、夜莺登台；合成爵士、麦克风噪声与掌声，约 70 秒 |
| [sc-a04-cafe-lucille/](sc-a04-cafe-lucille/) | 线绘咖啡馆固定短戏：露西初识，杯子交接，按是否做过码头搬运切换一句观察台词，约 45 秒 |
