# CityBox 城市地点资产

## 唯一源与发布物

- 正式源：`city-box/models/<地点>.blend`
- 城市布局：`city-box/city/build_city.py` 中的 `HERO_SLOTS`
- 候选：`city-box/models/review/`
- 构建场景：`city-box/city/city_build.blend`
- 导出与报告：`city-box/city/unity/City.fbx`、`city_report.json`
- Unity 固定发布物：`UnityClient/Assets/Resources/Models/Environment/City.fbx`

地点建筑不再直接交付 `Resources/Models/Buildings`，也不单独发布 FBX。Unity 中同一时刻只有一个正式整城 `City.fbx`。

## 接入顺序

1. 在 `city-box/models/review/` 完成候选模型和预览；用户确认后再提升为 `models/<地点>.blend`。
2. 令文件名、建筑根、`Anchor_<地点>`、`Camera_<地点>` 精确匹配 Scheme 地点名；需要 Orbit 时加入 `orbit pivot`。
3. 在 `HERO_SLOTS` 设计位置、朝向和等比 footprint。先渲染整城预览确认布局，再发布 Unity。
4. 执行完整发布：

```bash
cd city-box
./tools/publish_city.sh --render
python3 tools/unity_doctor.py
```

`publish_city.sh` 固定执行 build → export → publish，并在失败时停止。只有确认 `city_build.blend` 未过期时才可用 `--no-build`。

## 描线

城市源模型只维护主体几何和语义节点。`build_city.py` 在装配时为每栋重要建筑生成：

- `描线_<地点>_Low`：全城视野；
- `描线_<地点>_High`：地点聚焦视野。

不要把单体手绘描线带入正式城市源，也不要在 Unity 外置或运行时加载另一份高精描线。Low/High 必须与建筑同属一个根节点，并一起进入唯一 `City.fbx`。

### 逐模型描边调参

全局默认参数只负责统一基线；某栋建筑的几何粒度不同，需要保留窗框、消防梯或线脚时，在
`city-box/city/hero_outline_config.py` 按正式地点名覆盖，不要修改 `.blend` 保存最终描线，也不要为
单栋建筑复制一套生成脚本。

High 常用覆盖项：

- `line_px`：聚焦相机中的线宽；
- `min_edge_px`：最短边阈值，越低保留越多窗框等短边；
- `angle_deg`：硬边夹角阈值，越低保留越多浅转折；
- `min_edges`：模型细碎时的保底边数；
- `bevel_resolution`：线条截面圆滑度。

Low 可覆盖同名基础项；使用结构勾勒时，把 `plane_dist_px`、`min_region_px`、`simplify_px`、
`min_line_px`、`min_component_px`、`weld_px` 放进 `structure` 子字典。所有长度按目标相机中的屏幕像素
定义，避免模型缩放后观感漂移。

调参时只生成该地点的运行时聚焦状态预览（该栋开 High、关 Low，其余建筑维持 Low）：

```bash
cd city-box
blender -b --factory-startup --python city/build_city.py -- --hero-preview 家
```

可传多个地点，或用 `--hero-preview all` 一次输出全部重要建筑。每张图都会重置 Low/High 状态，避免
前一栋的 High 混入后一张图。输出为 `city/hero_outline_preview_<地点>.png`。审阅时同时判断细节完整度、发光糊线、悬浮/断线和
High 三角面增量；不能只凭边数选择。用户确认候选图后，才运行完整的
`./tools/publish_city.sh --render`。单栋预览会更新 `city_build.blend`，但不会 export 或覆盖 Unity 的
`City.fbx`；不要把“预览已生成”报告成“已发布”。

## 发布后验证

1. `city_report.json` 的 `problems` 为空，地点、Anchor、Camera 和 Low/High 成对完整。
2. CityBox 与 Unity 两份 `City.fbx` 的 SHA-256 相同，`.meta` GUID 未被替换。
3. Unity 重导入无契约错误；分别进入全城和几个受影响地点，确认相机、主体和 High 描线属于同一建筑。
4. `Main.unity` 的 City PrefabInstance 只允许根节点位置、旋转、统一缩放。禁止对子对象保存材质、名称、激活状态、Renderer 或 VCam 覆盖，因为 City 重建后的内部 fileID 不稳定。

如果地点错配，先检查场景 Prefab overrides 和导入日志，再检查 FBX 层级与包围盒；不要反复发布碰运气。
