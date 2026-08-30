# 公共构建层

这里不是第四种构建入口。用户仍从 Web Preview、Web Release 或 TapTap Release 的脚本进入；
`common.sh` 只负责独立 staging、Unity 定位、空间检查、字体子集和统一的 Unity 批处理启动，
`resource-plan.json` 则是三种构建共用的资源排除与视频发布清单。

Unity staging 内的统一资源处理实现在
`UnityClient/Assets/Editor/Build/BuildAssetPreparer.cs`。主工程资源不得由这些脚本直接裁剪。

Web Release 和 TapTap Release 在新版本完整构建成功后，会自动删除同一输出目录下旧的时间戳版本，始终只保留
本次成功构建；构建失败不会清理旧版本。

需要手动回收生成目录时使用统一入口：

```bash
./tools/build/clean.sh --old-releases  # 清理旧 Release，保留各平台最新版本
./tools/build/clean.sh --builds  # 只清理发布/预览产物
./tools/build/clean.sh --cache   # 只清理 staging 缓存
./tools/build/clean.sh --all     # 两者都清理
```
