# Unity 局部导入依赖验证

修复入口：`UnityClient/Assets/Editor/Pipeline/CitySharedMaterialImporter.cs`。模型不再直接依赖整份 World.visual.json；角色契约单独登记，灯光按 profile 登记，各模型仅依赖自己实际使用的灯光组。地点 FBX 哈希、环境参数和数组顺序不影响导入依赖。共享 WorldPalette 依赖保留。

2026-10-30 在独立工程 `.cache/import-dependency-check` 实跑，结果见 [验证结果](验证结果.txt)。使用正式导入器、色盘与运行时代码，仅将模型替换成三个微型真实 FBX：City（包含契约要求的河面及 A 组灯）、ProbeA（A 组灯）、ProbeB（无灯）。验证源 FBX 更新、清单更新和原生自定义依赖的传播，不测正式全城的导入耗时或游戏交互。Editor csproj 编译通过，零错误；有四条既有 CityNightEnvironment 字段警告。

`ImportDependencyCheck.cs` 只用于上述隔离工程，必须有 `.import-dependency-check` 标记且以 batchmode 运行，禁止放进主工程。验证时将主工程通过 `tools/unity/staging.py` 同步至独立缓存；先移除缓存中的正式 FBX 和 clips 清单，保留正式脚本、色盘及材质，再放入探针。World.visual.json 的角色列表沿用正式清单，灯光换为 ProbeALight / ProbeCityLight（profile 均为 ProbeA），profile 与 placeHashes 声明 ProbeA / ProbeB。`make-probes.py` 在标记工程中生成初始 FBX 和几何更新版本。

复制测试 C# 到隔离工程 Assets/Editor 后，以 Unity `-batchmode -nographics -quit -executeMethod SSNoir.Editor.ImportDependencyCheck.Run` 运行。每次重跑前重新生成初始探针；同秒生成的 FBX 可能同大小、同扫描时间戳，因此测试对实际变更的 A 显式调用 ImportAsset，再检查依赖传播。

本次没有重建或发布正式场景资源。导入器版本从 10 升至 11，现有模型需要一次重导以迁移旧依赖，此后局部更新按新依赖工作。
