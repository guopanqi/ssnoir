# 勒索追逐冲刺立绘

当前选用 `冲刺-openai.png`，由内置 OpenAI 生图服务生成；完整提示词见 `冲刺-openai-prompt.txt`。旧单帧、双帧文件仅为候选，不接入运行时。

采用单帧腾空冲刺定姿，参考 [AnimSchool 的跑步关键姿势讲解](https://blog.animschool.edu/2024/04/10/the-key-poses-of-a-run-cycle/) 中的 Push / Peak 阶段：前膝抬起、后腿回收、双臂反向摆动，避免长直腿跨步和两帧跳变。尼尔保留鸭舌帽与短夹克，取信人保持纯黑内部、克制外轮廓。

`tools/theatre/prepare-chase-portraits.py` 保留透明 alpha，两名跑者共用固定画布尺度，不按包围盒裁紧放大。正式资源为 `Portraits/Chase/尼尔_冲刺` 和 `Portraits/Chase/取信人_冲刺`，显示仍为 308×308。脚底略高于地面，与腾空姿势一致。

跑者保持定姿，沿用既有背景卷动、脚步、擦身黑影和脚后扬尘；本次不修改对白、走位、各段时长或入巷时序。舞台预览为无声，不能替代游戏声音与交互验收。
