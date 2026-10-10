# 勒索追逐跑步立绘

当前选用 `跑步双帧-openai.png`，来源为内置 OpenAI 生图服务，透明底双帧；提示词在同目录 `跑步双帧-openai-prompt.txt`。此前 Gemini 候选未接入。

两帧表现接地与收腿，均朝左。用 `tools/theatre/prepare-chase-portraits.py` 按等宽面板拆分，保留源 alpha、统一画布尺度并对齐鞋底，不逐帧裁紧或放大。资源在 `Portraits/Chase/`，统一 308×308 显示。尼尔保留鸭舌帽与短夹克，取信人使用无内部细节的匿名剪影。

每 0.115 秒交替一帧，脚步间隔 0.23 秒，沿用 Claude 第四场。完整舞台预览覆盖两帧交替与入巷；声音及游戏交锋衔接仍需 Unity Play Mode 验收。
