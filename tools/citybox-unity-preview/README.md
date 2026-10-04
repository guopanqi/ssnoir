# 正式 Unity 世界预览

在仓库根目录运行：

```sh
python3 tools/citybox-unity-preview/preview.py --scene 晚宴
python3 tools/citybox-unity-preview/preview.py --verify-play
```

需要安装项目版本 Unity（含 WebGL 模块），后台进程显式选择 WebGL。CLI 把正式 `UnityClient/Assets`、`Packages`、`ProjectSettings` 通过与打包共用的 `tools/unity/staging.py` 增量复制到自己拥有的独立工程，复用其 Library。它在后台启动可渲染的 Unity，不操作正在打开的主工程，不需要 Blender、接收窗口或临时试用场景。源工程关闭时可通过共用同步层以 APFS 快照建立初始 Library；源工程打开时首次重新导入，后续复用缓存。

输入是正式 `Main.unity` 与已发布资产。Editor 使用 `CityOutlineState`；Play 启动正式游戏并通过 `SSNoirGameManager.PresentCamera` 聚焦同一地点。两者使用同一套模型、共享 `.mat`、光照贴图、WorldPalette、URP 和相机换算，保留正式后处理。不会根据候选 `.blend` 另建 Unity 材质或灯光。

默认输出四场景；`--scene` 可重复，支持 `世界` 或其它已发布正式机位的地点名，未知机位直接报错。`--height` 控制 16:9 出图高度。`--project` 指定独立缓存目录，只接受空目录或此工具的标记目录；旧对齐实验工程不可复用。`--out` 指定输出根目录，每轮创建新目录，包含 PNG、配置 JSON、日志；成功后才生成 `result.json`。不要把失败轮次的图片当成完整验收结果。

`--verify-play` 额外比较同机位的 Editor/Play 相机、可见网格、材质 GUID、灯光、环境与管线配置。配置不同或运行时报错时命令失败。截图供 Agent 自我观察与迭代；固定机位不包含 UI、存档产生的家具、交锋人物、拖动镜头和动画时序，仍由正式游戏验收这些行为。河水与动画可能导致两张图像素不同。

修改世界材质应直接修改共享 Unity 资产；几何、描线角色与灯光声明通过 CityBox 的正式 build/publish 链更新。晚宴 FBX 更新后需用 `CityWorldLighting.BakeBanquet` 在独立工程重新烘焙、发布光照资源。缺失或过期光照会直接报错，预览不会绕开。

Blender 个性化探索仍保留在 `city-box/prefabs/review/`，可用于发现新的视觉元素。旧的 Unity/Blender 对齐转换器、独立试用场景、复制材质和试用 URP 已移除；它们不能代表正式游戏。

夜城预览沿用 HDR 截图目标，Bloom 与调色完成后才转成 sRGB PNG。`nightEffects` 记录共用 HDR 调色链是否启用，世界与聚焦地点均为 true。`worldEffectsWeight` 区分世界雾／配光权重，`bloomMultiplier` 记录近景辉光比例，`locationFog` 记录地点距离雾；固定预览完成视觉过渡后再出图，实际游戏按色盘中的过渡时间执行。批处理使用两个 Job worker，并在资源清理后等待正常编辑器帧再退出，避免当前 macOS/Unity 的关闭卡住。
