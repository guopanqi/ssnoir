# 人物路径与原路退场小样

入口 `path-study.html`，主页面有链接。22 秒独立试演，用夜莺低头姿势的既有 `.neon.json` 转为 35 条 SVG 路径，26 条布景路径；按各条累计长度描出，退场倒转同一构建时间轴，最后画出的部分最先收回。轮廓由对应 silhouette alpha 离线追踪成闭合路径，播放时仅填充 SVG，不加载人物 PNG；路径轮廓仍需人工审阅，不宣称从灯管路径自动推断人体。

`python3 experiments/line-stage-study/prepare-path-study.py` 重建试验 JSON。点缀色通过原图沿骨架采样决定，未人工整理五官、头发与碎线，当前是技术可行性小样。只接入实验页，尚未迁移 Unity 的正式剧场。

浏览器检查：入场 8 秒与退场 15.6 秒的 61 条路径 dash offset 一致；22 秒所有路径隐藏、轮廓 opacity 为 0。完整定场视觉审阅及浏览器无 error/warn；支持播放、重播、拖动进度，遮挡和辉光可切换。记录 `screenshots/path-study.png`。
