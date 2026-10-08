# 原版两人对照

新增原版立绘、原版 Neon 两个选项，选中时同时替换尼尔和夜莺，其余人物方向继续固定尼尔 C1。原版立绘使用已有 assets/neil.png 与 nightingale.png；原版 Neon 从 Unity Portraits/Neon 的基础 PNG 导入，通过最大颜色通道转换黑底为透明并裁去外围空白，保留原画发光与道具。导入脚本 import-original-cast.mjs。夜莺原版 Neon 源图脚部截断，本次未生成补画。夜莺原版两种图朝右，舞台默认镜像朝左；其他候选已朝左。

模块语法和 12 个选项两人资源路径检查通过。未运行浏览器交互或 Unity。

夜莺基础 Neon 已去掉麦克风与整根支架。使用 Tripo banana2 单图编辑，10 credits；提示词要求只删除麦克风、保留人物及原画幅。人工审阅黑底结果确认道具消失、人物造型与白蓝线条保留。新源图与 manifest 位于 art/night-neon-no-microphone/，导入脚本改用此图并重新生成透明 PNG。修改实验资源，未覆盖 Unity 或角色库。JS 语法检查通过；未运行浏览器或 Unity。
