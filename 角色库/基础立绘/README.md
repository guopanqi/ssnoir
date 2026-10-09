# 基础立绘：全身与统一尺度

本轮范围为正式 `Portraits/Neon` 中 20 张无姿势后缀的基础人物图。保留当前身份、服装、方向和画风；补齐缺失腿脚，不扩展姿势，也不接入描线入退场。

`源图/` 保存完整全身原稿，`制作基准.json` 保存相对站立身高及头顶、鞋底标定；`成品/` 是按统一画布定位的结果，`成品/加工记录.json` 可复核源尺寸、轮廓范围和缩放。这里的相对高矮是制作基准，不是原剧情已经确定的厘米身高。

统一画布为 1024 × 1024，鞋底基线 y=1000，站立中心 x=512。以尼尔为 1.00，身体头顶至鞋底高度为 800 像素；帽子和蓬松头发通过 `crownInset` 标定排除。每个人物等比缩放，不分别撑满画布。黑底源图沿用工程现有 Alpha From Grayscale 导入，运行时黑色透明；不改动贴图 GUID。

加工命令：`node tools/portrait-neon/normalize-base.mjs`。复用已安装的 `experiments/portrait-stage/portrait-stage-lab/node_modules/sharp`，只负责等比缩放、画布定位；缺失身体必须先补画并审阅，命令拒绝触底截断的源图。审阅成品后复制同名 PNG 到 `UnityClient/Assets/Resources/Portraits/Neon`，再执行 `tools/portrait-neon/process.py` 和 `silhouette.py` 更新派生资源。

基础图的 `.neon.json` 同时记录 `ground=1000/1024`，线绘剧场按此将实际鞋底对齐传入的 y。尚未重制的姿势图使用原有画布底边；换图时接地点随当前图片更新。剧本中的同地面人物使用相同 308 × 308 画布，身高差留在资产内部。

并排审阅入口：`experiments/portrait-stage/line-stage-study/base-cast.html`。它只展示当前成品，没有重新设计人物，也不证明游戏交互或音频正确。

## 本轮验收

20 张成品与工程 PNG 一致，尺寸均为 1024 × 1024；并排页面全部加载，浏览器无警告或错误。重生成 20 套线稿、点缀、骨架和遮挡。Unity 客户端 csproj 编译成功（4 条既有 CS0649 警告）；`--test-theatre` 通过。真实 Unity GPU 绘制 20 人同尺度、四条地面基线，输出成功并审阅在场帧，见 `Unity绘制验收.png`；对照标签见 `全员对照.png`。未运行游戏 Play Mode、音频或点击验收。
