# 标志性道具

顺序：先看 **复用道具库** `city-box/props/INDEX.md`（缩略图、key、尺寸；规则在 `props/README.md`），再翻已有场景的私有道具（`prefabs/review/*/props/`，INDEX 里标"场景内"），能用就用；场景内道具被第二个场景用到时 `git mv` 进 `props/<家族>/`（不复制）；简单但没有的补成程序化构件；只有**手搭不像**且**决定场景身份**的才生成。同一座城里的椅子、壁灯、圆桌反复重新生成，城市会像几个游戏拼起来的。晚宴生成了 5 件，最后吊灯还删了：等轴俯视下悬空物遮住焦点、没有玩法价值。

生成、拓扑、描线的做法和经验全在 `create-3d-assets`（参考图要单件、无背景、3/4 视角、正面朝观者）。这里只讲怎么摆：

```python
from props import PropLibrary
crowd = PropLibrary(os.path.join(ROOT, "props", "人形"))          # 共享库：人群、警察
crowd("弗兰克的人_1", coll, "canon_leanin", (x, y), face_deg, height=1.75)
prop = PropLibrary(os.path.join(ROOT, "prefabs", "review", NAME, "props"))   # 场景私有
prop("钢琴", coll, "piano", (-1.8, stage_y + 0.4, 1.0), 20)   # 台上给 z；尺寸取 props.json
prop("左卡座_1", coll, "booth", (x, y), 90)                    # 正面 -Y 转到 +X
```

- 共享的放 `props/<家族>/<key>-low.fbx`，场景私有的放 `prefabs/review/<名>/props/<key>-low.fbx`，脚本只认 `<key>-low.fbx` 这个名；换更高档位时拷成同名，旧的改 `-low-rejected.fbx`。
- 人群不用 greybox 的几何人：从 `props/人形` 取，`outline="hull"`，按身份选型（工人=鸭舌帽/卷袖/宽背；礼帽长外套只给侦探/记者；警察 `police_*`），前排只用前倾/站定/侧身。范例 `prefabs/src/别给他们想要的.py` 的 `person()`。
- 真实尺寸记在 `props.json`（`size_m` + `size_axis`），归资产不归场景；只有确实要变形才传 `height=` / `width=`。
- 生成模型脚底归零，放在台上要自己给 z。
