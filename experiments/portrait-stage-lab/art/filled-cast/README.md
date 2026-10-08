# 实心体块人物试稿

2026-10-08 按用户提出的“实质填充的体块 + 描线”重新设计，接入第三版，未替换 Unity 正式立绘。

服装的大面积实色建立人物体积，二至三层灰度面建立明暗；深色轮廓强调剪影，内部线条集中在五官、衣领与姿势。保持正常成人比例和 1940 年代服装，不继续使用镂空 Neon 或彩色剪纸风格。

- 尼尔：灰褐风衣、软呢帽，瘦长面部、收敛姿态；站立和摘帽两张。
- 夜莺：蓝灰外套、帽子与手包；侧身站立与低头收拢两张。
- 经理：宽肩灰西装、暗红领带，开放的手势；另有靠近搭肩的双人图，尼尔不回应。

`design.png` 是最初四人造型探索，其中两张是夜莺的戴帽、未戴帽稿；播放实际使用后续两组姿势图和经理稿。`*-prompt.txt` 与原图保留来源；Gemini 的生成记录位于项目 `tmp/gemini-image-web/filled-cast/`。

重建透明素材（在实验目录执行）：

```sh
node tools/prepare-filled-cast.mjs design.png neil-study nightingale-bare nightingale-study manager
node tools/prepare-filled-cast.mjs neil-poses.png neil-listen neil-hat
node tools/prepare-filled-cast.mjs nightingale-poses.png nightingale-wait nightingale-down --flip=nightingale-wait,nightingale-down
node tools/prepare-filled-cast.mjs contact.png contact
```

脚本按实际背景色去除与画面边缘连通的背景，保护人物中近似背景色的灰色面部；统一同组画幅和脚底基线。夜莺原图向左，导出时镜像为舞台约定的默认向右；运行时 `facing` 可翻转。输出为透明 PNG，搭肩属于一张关系立绘。
