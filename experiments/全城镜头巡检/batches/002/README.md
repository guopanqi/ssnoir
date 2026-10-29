> 本批后续修复已完成，见 [003最终结论](../003/README.md)。本批旧主体遮挡射线存在Unity/Blender坐标不一致，旧点位遮挡统计不作为依据（机身/near路线检查不受影响）；脚本已修正，003重跑后的结果为当前有效证据。

> 最终审阅结论以 [结论.md](结论.md) 为准：布告栏、老街酒馆为明确构图问题；货运公司为待正式材质确认的构图风险。下文保留数值诊断与初步观察，不把三处都定性为已确认缺陷。

# 002：普通城市地标世界往返镜头检查

## 范围与输入快照

- 检查完整装配 `city-box/build/city/city_build.blend` 中的 17 个普通城市地点；每地点只取主机位 `Camera_<地点>`，生成 `Camera_世界 ↔ Camera_<地点>` 共 34 条方向路线。排除 Stage 和内部机位。
- `Camera_世界` 是blend中作者机位，本批没有启动Unity，因此不能证明新游戏实际第一帧渲染姿态与它完全一致；静态代码表明 `SSNoirGameManager.Start` 默认读取 `location=world` 并加载该位置。玩家拖动后的任意起点也不在本批覆盖范围内。
- City FBX importer `globalScale=1`、`useFileUnits=1`、`useFileScale=1`；Main 场景 City prefab 根节点位置/旋转为identity，未见scale override；按prefab默认scale 1。共享相机输入 `ModelScale=1`。
- 源文件SHA256：`2acf1f5d474595aca5798d3e64f1f312d489a4c5c05bf3a581f50a9113ef9d61`；共享数学代码与程序哈希见 `camera_inputs.json`。

## 执行与结果

- 路线命令：`/opt/homebrew/bin/blender -b city-box/build/city/city_build.blend --python-exit-code 1 --python experiments/全城镜头巡检/batches/002/build_routes.py -- --batch experiments/全城镜头巡检/batches/002`。脚本对每个地点只将世界与目标两相机送入既有 `CameraReview.dll`，复用 `FocusTravelMath`，每条路采样 120 段。记录到的共享程序时间 1.196 秒（整个Blender路线命令约2.5秒）。Root核对过 `routes.json` 与其渲染输入34条路线逐key相同。
- 几何命令：`/opt/homebrew/bin/blender -b city-box/build/city/city_build.blend --python-exit-code 1 --python experiments/全城镜头巡检/batches/002/audit_geometry.py -- --batch experiments/全城镜头巡检/batches/002`。单次加载全场、评估 6,658 个mesh并建立BVH；检查34条路径中心线、近裁剪面周边和角点扫掠，以及目的地点外部mesh主体投影/最多32个表面顶点遮挡采样。完整检查约42秒。
- 34条路径数值/几何检查均为 `crossfade=false`、0条中心线穿插、0条近裁剪面命中。此结论是离散几何检查，不代表渲染视觉通过。
- 根已对17地点各5进度帧（0/25/50/75/100%）进行workbench标色审图，图像索引为 [sheet-01.png](sheet-01.png) 至 [sheet-06.png](sheet-06.png)，逐帧图在 `frames-geometry/`。审图报告3个明确构图问题；其余14处未见明显穿楼黑场或主体丢失。Workbench图只作几何构图证据，不是Unity美术效果验收。

## 各地点路径与复核

所有机位FOV均为26.991°，模型缩放为1。端点偏移为 `共享路径 End - 作者 Camera 位置`；Orbit端点可能由共享策略改写。路径栏为世界→地点 / 地点→世界。

| 地点 | 目标模式 | 路径 | 共享End相对作者机位 | 画面复核 |
|---|---|---|---:|---|
| 尾随取件人 | pan | AuthoredArc / AuthoredArc | 0.0 m | 根审图未见明显穿楼/丢主体 |
| 格兰德酒店 | orbit | DirectApproach / AuthoredArc | 240.0 m | 根审图未见明显穿楼/丢主体 |
| 报社 | orbit | DirectApproach / AuthoredArc | 229.1 m | 根审图未见明显穿楼/丢主体 |
| 街角咖啡馆 | orbit | DirectApproach / AuthoredArc | 38.5 m | 根审图未见明显穿楼/丢主体 |
| 码头 | pan | AuthoredArc / AuthoredArc | 0.0 m | 根审图未见明显穿楼/丢主体 |
| 三号货栈工棚 | orbit | DirectApproach / AuthoredArc | 25.4 m | 根审图未见明显穿楼/丢主体 |
| 勒索信 | pan | AuthoredArc / AuthoredArc | 0.0 m | 根审图未见明显穿楼/丢主体 |
| 货运公司 | orbit | DirectApproach / AuthoredArc | 467.9 m | 需修：终点几乎是无细节背/侧立面（根审图） |
| 剧院 | orbit | DirectApproach / DirectApproach | 31.4 m | 根审图未见明显穿楼/丢主体 |
| 警察局 | orbit | DirectApproach / AuthoredArc | 165.7 m | 根审图未见明显穿楼/丢主体 |
| 诊所 | orbit | DirectApproach / AuthoredArc | 81.0 m | 根审图未见明显穿楼/丢主体 |
| 老街酒馆 | orbit | DirectApproach / AuthoredArc | 118.2 m | 需修：右侧邻楼遮住部分主体（根审图） |
| 码头居民区 | orbit | DirectApproach / DirectApproach | 414.3 m | 根审图未见明显穿楼/丢主体 |
| 公园 | orbit | DirectApproach / AuthoredArc | 346.3 m | 根审图未见明显穿楼/丢主体 |
| 家 | orbit | DirectApproach / AuthoredArc | 118.0 m | 根审图未见明显穿楼/丢主体 |
| 布告栏 | orbit | DirectApproach / AuthoredArc | 42.4 m | 需修：到达后板面过侧，难读（根审图） |
| 雕像 | static | DirectApproach / DirectApproach | 0.0 m | 根审图未见明显穿楼/丢主体 |

## 需要处理的三处

| 地点 | authored Camera → 共享 `Plan.End` 位移 | 朝向差 | 首选排查层 |
|---|---:|---:|---|
| 布告栏 | 42.39 m | 94.65° | 机位 / 焦点：检查板面朝向与可读距离，当前World→Orbit最近侧换了约94.7°朝向；按共用策略修正端点/中心策略 |
| 货运公司 | 467.93 m | 123.80° | 机位 / 焦点：来向侧解把作者机位移至建筑另一侧（467.9m、123.8°），核对共享落点是否该保留作者端点/焦点 |
| 老街酒馆 | 118.15 m | 117.42° | 建筑与机位：根审图确认邻楼部分遮挡；先核邻楼空间/轮廓，再评估保持共享算法下的焦点/机位取景 |

三处目标相机在blend中均声明 `mode=orbit`，`Camera_世界 → 地点` 路径都被共享数学选择为 `DirectApproach`。`FocusTravelMath.Create` 的 `targetOrbit` 分支保持作者端点的半径和俯仰，把方位改为从当前源机位看向目标 pivot 的来向侧；所以异常与Orbit最近侧解一致。布告栏、货运公司、老街酒馆分别从作者Camera位置偏移42.39m、467.93m、118.15m，目标朝向差94.65°、123.80°、117.42°。路线末帧落点与作者Camera不是同一机位。

老街酒馆几何报告对32个疏采样顶点端点射线均未发现外部阻挡；这不是遮挡排除证据，采样不能稳定命中画面里邻楼遮挡的局部，故以根的实际标色图判断建筑/取景待修。

## 证据边界与后续

- `geometry_report.json` 中主体bounds来自目标root的实际外部可见后代mesh，排除 `layer=interior`，不是只投影Anchor。局部地面/边缘顶点、稀疏射线及装配静态几何有已知限制；没有处理Unity动态显隐与运行时交互偏置。
- 此批不启动Unity、不做全城构建、不改正式场景/模型/工具。首轮全程只读完整City装配，批次脚本与结果均在本目录。
- 建议下一批先修这三处作者机位/焦点或邻楼空间后重看相同34路线。再扩到城市内部镜头时，先确认新的runtime camera start和Stage Portal实际可见性，继续按两相机route输入筛选，避免全地点all-pairs。
