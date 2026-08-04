# Unity 交付

## 目录分类

正式资源位于 `UnityClient/Assets/Resources/Models/`：

- `Buildings/`：单体建筑或建筑立面。
- `Environment/`：完整空间、地形、水体、天际线、公园、城市块等环境组合。
- `Props/`：可独立摆放的局部物件。
- `Vehicles/`：车辆及明确依附于车辆系统的资产。

按资产在游戏中的职责分类，不按画面中“是否有建筑”分类。完整公园属于 `Environment`，其中有亭子也不会变成 `Building`。

文件名使用稳定、可读的资源名；中文可用。不要用 `final2`、`new`、`修复版` 等过程状态命名正式资产。

## 保存与导入

1. 用户确认 Blender 最终预览后，才保存到正式目录。
2. 优先保存 `.blend`，除非目标运行链明确要求 `.fbx`。
3. **Codex 的交付在正式 `.blend` 写入该目录时结束。**不手工伪造、等待或检查 `.meta`；也不启动、刷新或操作 Unity 来检查导入器、Console、VCam、Anchor、orbit pivot 或 Play Mode。以上 Unity 侧操作一律由用户手动完成。

CityBox 整城管线是第 2 条的明确例外：独立建筑仍以同名 `.blend` 作为源资产，`city/export_unity.py` 生成的 `City.fbx` 是经过合并、三角化和语义契约校验的发布产物。它覆盖 Unity 中已有固定路径的整城 FBX，但仍不由 Codex 启动 Unity 验证导入结果。

## 验证边界

后台 Blender 验证证明文件可读、几何统计与命名契约；它不试图证明 Unity 导入器和运行时行为。Codex 不报告或等待 Unity 导入/Play Mode 验证，因为这属于用户手动步骤。

## 交付记录

最终报告至少包含：

- 正式资产绝对路径。
- 分类理由。
- 面数与拓扑摘要。
- Camera、Anchor、orbit pivot、Actor 标记和专用节点清单。
- 描边脚本及关键参数是否采用默认值。
- 实际执行过的 Blender/Unity 验证和未执行部分。
- 一张最终预览图。
