---
name: make-scene
description: 为 SSNoir 从零做一个交锋/地点场景的 CityBox Prefab 时使用（宴会厅、后巷、办公室这类"一个空间"）：布局预览图 → 程序化灰盒 → 标志性道具交给 image-to-3D → 锚点与机位 → 放进城市发布。单件 3D 模型的生成与加工归 create-3d-assets，本文只讲场景。
---

# 做一个场景

一个场景 = 一个 `city-box/prefabs/<名>.blend`，由 `city-box/prefabs/src/<名>.py` 程序化生成；里面是净几何、语义节点和几件生成模型。它和"做一件 3D 资产"不一样：**主体是空间关系，不是形体**——墙、地、台阶、家具的摆位决定镜头里读不读得出这是什么地方，单件模型只是点缀。

五步，每步末尾是一个确认门（用户说"下一步"才过）：

1. **布局预览图**：先理解故事在这里发生时，空间需要提供哪些可见的信息，再决定怎样用布局表达；预览图既要成立为空间，也要契合具体情景。读 [references/layout-preview.md](references/layout-preview.md)。
2. **灰盒**：照着图用方块搭 `prefabs/src/<名>.py`，跑单体预览。读 [references/greybox.md](references/greybox.md)。
3. **道具**：灰盒定了之后，才看哪几件值得精细化。先看复用道具库 `city-box/props/INDEX.md`（人形、车辆…带缩略图），再翻已有场景（`prefabs/src/*.py` 的构件函数、`prefabs/review/*/props/` 的场景内道具），能用的直接用——场景内道具被第二个场景用到的那一刻 `git mv` 进 `props/`；没有的，简单的补成程序化构件，只有手搭不像又决定场景身份的才交给 `create-3d-assets` 生成。读 [references/props.md](references/props.md)。
   场景里**会动的机关**（唱片机这类有状态、要读档恢复的）另有一套：整件程序化 + `props.json` 的 `clips` +
   `pipeline/motion.py`，审片用 `preview.py --motion <道具> --gif`。读 [references/motion.md](references/motion.md)。
4. **锚点与机位**：锚点按空间位置组织（多卡可共用同一 Anchor），并把脚本动作映射到这些位置；Pan 机位配 `PanBounds_<名>`，四角极限画面都得在场景内。读 [references/anchors-camera.md](references/anchors-camera.md)。
5. **放进城市、发布**：`city.blend` 加实例 → `./build.sh --no-publish --render --focus <名> --pan <名>` → 用户看整城 → `./build.sh`。规则在 `create-3d-assets/references/delivery/citybox.md` 和 `city-box/README.md`，不在这里重复。`prefabs/review/<名>/` 保留最终布局图、`prompts.md`、每件道具的参考图 / `-geo.fbx` / `-low.fbx` / 预览 / `props.json`，有动画时另留最终 GIF、帧带与关键整帧。`build/prefabs/` 的预览供人回看；同一动画重渲成功时只清理上一版过时的预览，详见 [motion.md](references/motion.md)。

正式名以 Scheme `GameNode.Name` 为准：`prefabs/<名>.blend`、集合、`Anchor_<名>`、`Camera_<名>`、交锋脚本的根 `container` 都用同一个名字——根节点名不一致，镜头就切不过来（晚宴曾因根叫"晚宴前半"而一直停在世界机位）。

## 服务

需要生图时按 [make-images](../make-images/SKILL.md) 定义与审图，**默认**调用 `tools/gemini-image-web`（`--out` 落到 `city-box/prefabs/review/<名>/NN-<描述>`；用法看其 README / `--help`；同一 profile 不可并发）；需要生成单件模型时按 [create-3d-assets](../create-3d-assets/SKILL.md) 审核，调用 `tools/tripo/model.py`。参数和鉴权见对应工具 README，不在网页上手点。

## 做过的场景

- **晚宴**：`prefabs/src/晚宴.py`、`prefabs/review/晚宴/`（06 号布局图是最终依据，`prompts.md` 记着否决理由）。带生成道具的范例。
- **巷子里在打人**：`prefabs/src/巷子里在打人.py`，纯灰盒 + 抽象人形，嵌套在酒馆 Prefab 里。
- **勒索信**：`prefabs/src/勒索信.py` 正按 10 号布局图从零重做灰盒，尚未通过本轮用户审图；不再把旧版当成生成模型收进 Prefab 的范例。
- **租屋**：`prefabs/src/租屋.py`，世界层地点（家）门里的 Stage——不是交锋也能穿门，机制只认导航路径；
  带 `presence` 随卡显隐的家具（花盆、唱片机）。PortalIn 加在手搭的 `家.blend` 里，实例停在郊野那一排。
