# 住所实验：桥头画风能否延伸到普通生活空间

运行：沿用实验室 Vite 服务，打开 http://127.0.0.1:5173/residence.html （默认室内全景）。
独立构建：`npx vite build --config vite.residence.config.js`，输出 `dist-residence/`。

入口 `residence.html`；源码 `src/residence-study/`：`scene.js` 构成，`lighting.js` 灯光，`profile.js` 材质参数，`shots.js` 四个固定机位，`main.js` 装配和交互。图片在 `captures/residence-study/`（本地生成，不进 Git）。

## 固定条件与变量

与桥头共用 `src/post/bridgePrint.js` 的亮度映射与刻线设置，保持冷灰中间调、深色实体、银白亮面。不复制第三方实现；本次没有新增外部依赖或模型。
变化的是空间与形体：砖石公寓、凸窗、门厅、消防梯；室内使用租屋的床、书桌、窗、落地灯、电话、植物和唱片机职责与相对布局。参考的是 `prefabs/src/租屋.py` 中的空间设定，不是其他世界实验。
这只是视觉候选；没有导入现有 Blender 网格，没有接入 Scheme、锚点、presence、存档与交互。植物和唱片机一直展示，不代表游戏中已购买。

## 四个机位

- `?view=street`：街角，辨认主公寓与邻屋关系。
- `?view=entrance`：门厅，检查台阶、栏杆、门和消防梯遮挡。
- `?view=room`：全景，检查床、书桌、电话和空地分工。
- `?view=desk`：近景，检查灯罩、打字机、纸与窗格投影。

三种模式：`?mode=shape` 原始受光、`?mode=ink` 默认描线与块面、`?mode=hatch` 中间调刻线。按钮可以切换。H 隐藏面板；`?capture` 也可隐藏。拖动旋转、滚轮缩放；点击机位链接会重置画面。
窗光与灯光实际参与照明和投影，没有实体光锥。室内灯光允许偏暖，但共用最终冷灰色阶仍会把输出统一为冷色，不是保留原色的调色实验。当前灯下亮度过渡仍可见环状色阶，窗框线条也偏密，留待用户审图后再决定。

## 验证与边界

四个固定机位已在浏览器实际查看并保存截图；模式按钮检查。构建结果和最终观察见 `RESEARCH_LOG.md` 的住所实验条目。
本实验不替换 CityBox/Unity 生产资产，没有进行手机性能或 Unity 行为验证。
迁移时应携带空间布局、光源职责、轮廓层级与色阶关系；Three.js 源码不会成为 Unity 依赖。
