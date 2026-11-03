# 动画导入后处理对照

在工具专属 `.cache/animation-import-review` 中编译并导入指定 FBX，不修改正在使用的 Unity 主工程。首轮可从已停止的正式预览缓存 APFS clone Library；只保留被测 FBX，避免全城动画影响计时。

```sh
python3 tools/animation-import-review/benchmark.py \
  --importer artifacts/工程验证/动画导入优化/baseline/SSNoirModelImporter.cs \
  --out artifacts/工程验证/动画导入优化/baseline/run \
  --asset Assets/Resources/City/Places/联合起吊.fbx
```

`--importer` 是工程外准备的计时副本，需要输出 `ANIM_IMPORT_PROFILE|` 分段计时，并在两轮使用不同的导入版本号，确保真实导入而非命中缓存。正式导入器不需要保留计时日志，也不应仅因纯性能改动而递增序列化版本。默认被测资源为联合起吊，可重复指定 `--asset`。

`AnimationImportBenchmark.cs` 仅安装到私有缓存。输出每个动画的时长、帧率、Legacy/循环/Wrap 属性，以及所有浮点曲线的键值、左右切线、权重、weightedMode、切线模式和 broken 状态的 SHA-256；另记录对象引用曲线与事件摘要。二进制转储保留逐键原始数据，曲线清单包含其偏移。完整快照可能较大，精确比对通过后可清理生成的 `.curves.bin`，保留逐曲线摘要和比较报告。

```sh
python3 tools/animation-import-review/compare.py \
  artifacts/工程验证/动画导入优化/baseline/run \
  artifacts/工程验证/动画导入优化/optimized/run \
  --out artifacts/工程验证/动画导入优化/comparison.json
```

任一曲线、关键帧、动画属性差异都会失败。`--profile-only` 只记录真实导入耗时和动画名称，不输出完整曲线，不能当作等价性证据。比较只代表所选资源，不代表其他资源也已逐键比对；导入计时与启动、编译、快照时间分别看待。
