# Neon 人物离线加工

本工具处理 `UnityClient/Assets/Resources/Portraits/Neon/` 下的描线人物，生成线条蒙版、点缀蒙版与管线骨架。普通插画、实心填色人物及场景图不适用这套像素审查与派生算法。

## 源图审查

```bash
python3 tools/portrait-neon/audit.py path/to/portrait.png
```

`audit.py` 检查非黑背景、大块灰色填充和白描线核心亮度跨度。失败时修复源图，不靠运行时调暗或缩小预览掩盖；具体阈值以脚本为准。它无法证明身份、服装、人体比例、姿势或接地正确，这些按 [make-images 人物知识](../../skills/make-images/references/portraits.md)审阅。

## 派生资源

通过源图审查后按人物名处理：

```bash
python3 tools/portrait-neon/process.py 夜莺 尼尔_逼近
```

不传名字会处理目录内所有源图；派生文件较新时跳过，`--force` 强制重做。依赖 Pillow。输出 `<名>_lines.png`、`<名>_accent.png` 和 `<名>.neon.json`；图层含义、骨架算法与参数见 `process.py`。派生资源保持源图亮白描线核心，不把演出中的调暗、淡入淡出写回资产。

需要遮挡轮廓时读取 [SILHOUETTE.md](SILHOUETTE.md)。最终仍需审阅实际透明资源与同尺度舞台效果；文件放入 Neon 目录本身不会配置 Unity 导入器。
