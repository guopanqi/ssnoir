# 勒索追逐跑步立绘

选用 `跑步双帧.png`；单帧候选未接入。生成提示词在同目录。
来源：Gemini Images / Nano Banana 2，[生成对话](https://gemini.google.com/app/e7fe21d4577a506f)。

两帧分别表现伸展与收腿，保持同图画风和人体尺度。用 `tools/theatre/prepare-chase-portraits.py` 按固定面板拆分，清除图外标签和分隔线，派生透明背景并对齐鞋底；不逐帧裁紧或放大人物。资源在 `Portraits/Chase/`，统一 308×308 显示，尼尔原图朝右，在剧场中整体镜像朝左；取信人保持无内部细节的匿名剪影。

实际演出每 0.12 秒交替一次；最终需在 Unity Play Mode 验收声音与转场。
