# 会动的道具（状态通道）

场景里会动的东西：唱片机、风扇、门、招牌，也包括一次性的剧情演出（吊灯坠落）。**画面是终态，动画只是终态之间的过渡**
（和对白舞台指示同一条房规）。模型、clip、审片、发布这几步两类完全一样，只在第 6 步「谁来切状态」分开。

## 一条龙

1. **模型**：整件程序化，一个脚本生成 `props/<家族>/<key>-parts.fbx`（每个刚体部件一个对象，原点 = 它的 pivot）
   和 `<key>-low.fbx`（合并版）。范例 `props/家居/实验/唱片机/build_procedural.py`。
   什么走生成、什么走程序化，看**关节**：部件之间要相对运动、pivot 要精确落位的机关（唱臂/转盘/柜身）
   程序化——生成模型拆件两次都失败（部件糊在一起、pivot 不在该在的地方）；本身是一整块、只做整体位移/缩放的
   件（音符、吊灯的灯体、招牌）照样 Gemini → Hunyuan 生成，圆润的形体生成比手搭好看得多。
   生成件用 `PropLibrary()` 摆好后并进 `parts` 字典一起打关键帧，不需要 -parts.fbx。
2. **clip**：写在 `props.json` 该 key 的 `clips` 里（规格见 `pipeline/motion.py` 头注释）：
   状态 clip（`Stopped` / `Playing`，可带 `loop`）+ 过渡 clip（`from`/`to`/`keys`）。通道 x y z rx ry rz，
   值是相对摆放姿态的偏移，秒为单位。
3. **摆进场景**：Prefab 脚本里
   ```python
   parts = HOME.parts("唱片机", coll, "gramophone", (x, y), face_deg)
   for o in parts.values(): o["presence"] = "租屋-唱片机"; o["outline"] = ...
   motion.build(coll, "唱片机", parts, HOME.meta["gramophone"]["clips"])
   ```
   `motion.build` 先校验（状态存在 / 循环首尾接 / 过渡首末帧 = 两端状态），再打关键帧、把帧范围记进集合 `clips`。
4. **审**：`blender -b --python pipeline/preview.py -- <名> --game --motion <道具> [--gif]`
   帧带 + 每段首帧全分辨率 + 动图，都从贴近道具的机位。看：动作读不读得出、描线跟不跟着动、状态对不对。
5. **发布**：`./build.sh` 照常；`places/<名>.clips.json` 随 FBX 一起进 Unity，导入器切成 Legacy clip `道具__状态`。
6. **脚本侧**，两种，按机关的性质选：
   - **状态机关**（读档要恢复、可反复切换）：不新增 DSL，状态从**现有游戏状态**推出（唱片机 = 全局键 `音乐`），
     `PropMotion.SyncAll` 每次快照落地时对一遍：首次直接摆、没变不动、变了播过渡。
   - **一次性演出**（吊灯坠落，只活在一场交锋里）：剧本里 `(play-motion! "大吊灯" "Fallen" "首演之夜-吊灯")`，
     一个阻塞步骤：影幕 + 运镜到 `Camera_<机位>`（Prefab 里加一台 `drag=static` 的机位）→ 播过渡 → 回原机位，
     不需要点击；接 `play-dialogue!` 就是「动画 → 对白 → 回牌面」。道具之后停在目标状态上，不记任何游戏状态。
     范例 `prefabs/src/首演之夜.py` 的 `chandelier()`：灯体一整件 + 吊索各一件，clip 直接写在脚本里。

## 让动作读得出来

- 先想机位：动作要**横过画面**才看得见。绕竖直轴摆的东西正对机位只是抖一下（喇叭那次）；侧对机位（`face_deg`）才有幅度。
- 圆的东西转起来看不见：唱片标签上加一道横贯的印记。
- 硬边线按全城标定会砍掉 4cm 以下的边，屋里的小件（唱片圆、臂）就没了：集合上 `min_edge_px = 3`。
  圆滑面（喇叭口）用 `hull`。会动部件的线各自一份挂在部件下，构建自动处理。
- 过渡分段写：抬 → 摆 → 落，每段一对关键帧，比一段直线像机械。
- 静止状态 clip 至少 1 秒保持帧：Unity 会把太短的 clip 拉长，卷进后一段的关键帧。

## 坑

- 不是 `presence` 的会动件也各自出线挂在自己下面（outline.py 认 animation_data）；线不跟着动多半是件没打上关键帧。
- 审片 `--motion` 默认沿游戏机位推近；给了 `--cam` 就照那台机位拍，审演出机位用这个。
- 城市文件默认 24 fps，关键帧按 30 打的——`build.py` 已强制 30，别改。
- `duplicates_make_real` 不带动画，`prefab.make_real` 从库里按名字接回 action；部件对象名要唯一（`<道具>_<部件>`）。
- FBX 导出只烘"选中集合里有关键帧的对象"，否则整城每个对象都烘成常量曲线，文件翻倍。
- Unity 导入时会丢掉在 clip 范围内恒定的曲线（Stopped 这种静止 clip 一条曲线都不剩）。运行时 `PropMotion`
  在播任何 clip 前把该道具的部件复位到静止姿态（clips.json 的 `parts`，姿态 = 第 1 帧 = 第一个状态 clip），
  所以 **props.json 里第一个 clip 必须是默认状态**。不要靠状态 clip 本身把东西摆回去。
- 过渡 clip 的 wrapMode 必须是 Once：ClampForever 永远不"完成"，排在后面的目标状态永远轮不到。
