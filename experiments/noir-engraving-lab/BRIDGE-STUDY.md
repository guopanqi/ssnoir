# 桥头视觉实验

启动沿用本实验室的 Vite 服务，访问 `/bridge.html`。当前本地地址：
http://127.0.0.1:5173/bridge.html

独立构建：`npx vite build --config vite.bridge.config.js`，结果在 `dist-bridge/`。

## 这次比较什么

- 明暗：原始冷色受光，关闭描线，观察形体与真实光照。
- 描线与块面（默认）：保留冷灰中间调，暗面形成剪影，亮面与银白轮廓共同描述空间。
- 局部刻线：仅在指定亮度范围叠加细斜线。当前是屏幕空间亮度遮罩，尚未实现按物体、法线或表面方向贴合的刻线；镜头移动时纹理不会跟随物体。

拖动旋转，滚轮缩放，H 隐藏/显示面板。`?capture` 隐藏面板用于截图。
画幅变化只改变相机视场角，不按设备类型设置版面。

场景为人工构建的桥与两岸局部研究，加入桥洞、退台钟楼、圆顶拱廊、真实街灯受光和稀疏河面反光。没有覆盖现有城市、宴会场景或 Unity 资源。

## 借鉴方向

- [Genesis Noir 官方画面](https://www.fellowtravellerpresskit.com/genesis-noir)：剪影、留白、明亮笔画与深色实体之间的关系。
- [spite/sketch](https://github.com/spite/sketch)：受光的图形化和限制刻线范围。
- [pencil-lines](https://github.com/mayacoda/pencil-lines)：线条质感的实验方向。当前版本仍是几何边线，没有接入该项目的铅笔描线 shader。

本实验代码为独立实现，没有拷贝这些仓库的实现。当前仍能看到程序化模型的简化感；它是用于判断构图、光照与描线配合的候选预览，不是最终游戏风格规范。

## 验证范围

Vite 独立构建成功；浏览器 WebGL 渲染正常，三种按钮模式实际切换检查，控制台未发现 error。未验证 Unity 接入与手机性能。
