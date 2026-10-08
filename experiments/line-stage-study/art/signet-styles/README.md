# 夜莺黑白墨绘探索

参考 Signet City 官方资料中的黑白钢笔画、实黑阴影与网点方法，保留 1930s 美国夜莺的礼服、额前发浪、卷发、修长身段和锁骨手势。参考：https://www.fellowtravellerpresskit.com/signet-city 与 https://jumpovertheage.substack.com/p/introducing-my-new-game-signet-city 。官方截图下载失败，本轮没有使用截图作为生图参考，不宣称复刻具体画面。

I1 粗墨块：极少内线与大片实黑。I2 疏排线：脸颊和肩部局部短排线。I3 局部网点：脸侧与礼服局部网点，生成结果另有浅色外轮廓。三版为文生图，保持基本姿势但脸型、发长与裙摆有变化。Tripo banana2 顺序生成，各 10 credits，总 30 credits；Gemini 本日此前已出现额度耗尽证据。完整提示词、源图和 manifest 在本目录。assets/29…31 为透明舞台图片，comparison.png 为深色背景对照。

P 系列的运行时选项、图片和制作文件全部移除，历史由 Git 保留；六格后处理比较改为 N2/C1。默认人物为 I1，尼尔仍固定 C1。旧 CAST-STUDY.md 中的 P 章节仅为之前的历史记录，其文件路径已退出当前工作树。

检查：逐张查看源图与透明对照；cast.js/main.js 语法通过，10 个画风两人资源路径存在，运行时无 noirPaint/P 系列引用。I2 黑发边缘仍有少量抠绿残留，候选定稿时需精修。未运行浏览器交互或 Unity。本轮不以静态检查证明舞台融合效果。
