# 开场连续运镜试镜

这是现有开场的内容审核工具，不替换正式 `play-camera!` 编排。使用正式 Main、城市资源、共享材质、URP 和运行时配光/描线过渡，在已有世界预览的独立缓存中回放；不保存 Main，不控制主工程窗口。

```sh
# 首次先用上级 preview.py 建立独立缓存。
python3 tools/citybox-unity-preview/opening/review.py --out .cache/opening-inspect
python3 tools/citybox-unity-preview/opening/review.py --baseline --out .cache/opening-old --height 540
python3 tools/citybox-unity-preview/opening/review.py --route city-box/prefabs/review/开场连续运镜-20261015/route.json --out .cache/opening-take-01 --fps 24 --height 720
```

输出目录必须是空目录；同一缓存不能同时运行另一场 Unity 预览。检查时不要把失败轮次当作通过。工具只会在结果已生成、Unity 已进入 Shutdown 后处理自己启动的编辑器退出卡住，不操作其他 Unity。

路线是世界坐标 JSON：`duration` 为运动时长，`focusAt` 为开始酒馆配光/描线交接的时间，`destination` 指定终点机位。`knots.position` 定义 Cinemachine Smooth Path；全程按距离推进，只有开头加速和结尾减速。`knots.time` 是归一化的**注视时间**，`lookAt` 按独立 Hermite 曲线取样，避免弯道处摄影机路径参数快速变化时甩头。`fov` 沿摄影机路径控制点插值。终点坐标必须匹配正式酒馆机位，最终朝向对齐该机位。

候选在正式 Play 中以指定帧率离屏采样。配光/描线组件暂停自动更新，通过仅 Editor 存在的 `AdvanceEditorPreview` 按同一步长执行正式插值；播放器的普通 Update 行为保持不变。这样渲染慢也不会丢掉运镜中间帧。24 fps 视频是离屏采样率，不代表手机或游戏实时帧率。交通、水面等环境动画不是这次镜头的验收对象。

`--baseline` 调用当前正式 `PlayStoryCamera` 的酒店 .8 秒、剧院 .8 秒、酒馆 .4 秒三段，按实际时间采样并编码为可变帧率视频；用于比较停靠结构，不能比较实时性能。它没有触发开场动作或改动存档。

Agent 每轮先看完整视频与 `index.jpg`，再查 `result.json`：转头峰值、中段速度是否归零、终点是否匹配及 2 米半径的静态实体网格扫掠结果。扫掠只覆盖开始录制时可见、使用正式表面材质的网格；不把程序灯晕等 GPU 展开的退化面当作实体，也不宣称覆盖隐藏内部、近裁剪平面和所有遮挡。审阅仍须看实际画面。

默认先用低分辨率审路线，集中修改一个问题，保留上一轮好版本；路线成立后用 24 fps / 720 高完成片。候选、原版、路线 JSON、迭代原因和证据留在同一 review 目录，交用户审核后再接入正式开场。
