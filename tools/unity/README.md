# Unity 独立工程准备

`staging.py` 供发布构建和正式场景预览共用。它同步 `Assets`、`Packages`、`ProjectSettings`，删除目标中已过期的工程资源，并恢复上轮字体子集等发布加工造成的改变；源工程不被修改。项目外的本地 Package 仍按 manifest 的路径引用。

每种用途使用自己的缓存工程。Library 存在时保持原样，由目标 Unity 增量更新；首次准备且源 Unity 已关闭时，macOS 使用 APFS clone 复用源 Library。源工程运行中则从空 Library 开始，避免复制活跃的导入数据库。

缓存归属和目录选择由调用方负责；工具拒绝源/目标相同或互相嵌套。它不裁剪资源、不生成字体、不改管线、不调用 Unity。发布资源处理留在 `tools/build/`，实际截图留在 `tools/citybox-unity-preview/`。
