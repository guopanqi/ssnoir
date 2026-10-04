# CityBox → Unity 实景对照

从仓库根目录运行：

```sh
python3 tools/citybox-unity-preview/preview.py
python3 tools/citybox-unity-preview/preview.py --scene 晚宴
python3 tools/citybox-unity-preview/preview.py --scene 晚宴 --bake-lights --publish-trial
python3 tools/citybox-unity-preview/preview.py --publish-trial
```

需要 Blender 和项目对应版本的 Unity；可指定 `--blender`、`--unity`。默认使用 `.cache/citybox-unity-preview` 独立工程，复用 Library；不操作正在打开的 UnityClient。指定 `--project` 时只接受空目录或本工具已标记的验证目录。

输入是四场景 review 中已生成的 `candidate.blend`，不会自动把实验提升为正式 Prefab。输出在 `city-box/prefabs/review/noir-study/unity/<场景>`：实景 FBX、`scene.visual.json` 与真实 Unity 截图。独立工程的 `Assets/CityBoxReview/<场景>/review.unity` 可以用于进一步检查。

`--publish-trial` 将生成的完整试用包发布到主工程 `UnityClient/Assets/CityBoxReview`。双击各场景目录的 `review.unity`，进入 Play Mode 查看。场景自带独立 URP 配置，在编辑和运行状态均生效，卸载试用场景时恢复此前配置；无需修改 Main。当前为固定机位的视觉试用，没有接入游戏导航、交锋或移动镜头。

## 已对齐的部分

- 同一几何与几何描线；描线采用 Unlit，不受场景灯光影响。
- 线性底色、自发光、粗糙度和金属度的显式转换，按对象材质槽分配。
- FBX 坐标基标记，统一机位、方向、比例和垂直 FOV。
- 实体投射/接收阴影，描线不投射/接收阴影。
- Linear 项目、Standard 对照输出、关闭场景后处理，2× 超采样截图。

## 仍不等价的部分

当前是差异定位工具，不能作为最终美术发布器：URP 实时 Area 不可用，当前用同方向宽角 Spot 近似，**灯光强度公式是初始估计，没有逐灯标定**。晚宴地面高光过强、阴影软硬不同，已经在实际截图中出现。环境光仅简化为 Flat ambient，没有传递 Blender 完整环境照明；雾是线性近似。Principled 和 URP Lit 的高光模型、法线导入及抗锯齿也需要比较。贴图或程序节点直接报错，不默默降级。

码头目前导入静态水材质/波纹，没有验证游戏中的动态水 Shader。独立工程禁用 Stylize Feature 与 Volume，尚未验证正式游戏的 Bloom、FilmGrain、Vignette 和相机交互，也未验证手机阴影成本。

## 正式接入建议

先以 Unity 为最终验收画面，再固定共同材质、描线和相机契约。可移动对象使用实时 Directional/Spot/Point；晚宴的静态柔光优先试验烘焙光照，并为人物配置 Light Probes。不要把 Area 的 Spot 近似直接发布。后处理作为全游戏共同配置逐项加回并对照，不在每个场景里补偿一次。

正式采用后，将经验证的材质/灯光描述接入现有 build.sh → publish.py → Unity 导入链，再检查主游戏相机和移动端效果。本工具没有覆盖正式 City.fbx、Main.unity 或 GlobalVolume。

## 本轮验证

四候选重新打开后导出，共同底色持久化核对通过；四场景在独立 Unity 2022.3.62f3c1 / URP 14.0.12 中真实导入、渲染并保存场景成功。命令行包装器另执行 `--scene 晚宴` 成功。运行时代码的实体/描线阴影区分已通过 Unity 生成的 SSNoir.Client.csproj 编译；完整游戏与手机未试跑。

2026-10-04：四场景另带持久化试用 URP 配置重新导入并渲染成功，试用包已发布到主工程；发布后逐文件 SHA-256 与生成包一致，四场景的试用组件脚本 GUID 正确。新增试用组件通过 SSNoir.Client.csproj 编译，0 warning / 0 error。Play Mode 由用户进行实测，尚未自动操作正在打开的主工程。

同日追加：按用户要求操作主工程 Unity，截图比较晚宴同一 Game 视图（1920×1080）的 Editor / Play Mode。发现原试用组件只在 Play 才切换配置：原项目每物体附加灯上限 4、附加灯阴影关闭；试用配置上限 8、阴影开启。改为 ExecuteAlways，在编辑与运行时统一应用；构建器先在非激活对象上配置组件，再激活，避免缺少引用。重新编译成功，刷新后再进入/退出 Play 截图核对，晚宴的明显灯光和阴影跳变已消除。独立对照截图仍不能替代 Play Mode 验证：本次发现单相机批量截图此前未呈现完整附加灯阴影，原 comparison.png 不能视为最终运行画面证据。柔光与阴影观感仍待调整。

## 晚宴柔光探索（尚未通过正式发布验收）

`--bake-lights` 只接受晚宴。使用独立工程的 Progressive GPU 烘焙 Area 灯，生成光照贴图和 Light Probes；钢琴、卡座和细小自发光灯饰不进入静态光照贴图。试验中漫反射强度为初始换算的 0.35，不能把这个比例视为跨场景通用校准。

已试过单个房间 Reflection Probe 和三个局部 Probe。它们保留部分环境反射，但没有还原原预览的面光反射亮斑。目前使用 `shaders/AreaFloorHighlight.shader`：根据原灯位置、方向、圆盘尺寸、功率和地面粗糙度，在原 URP Lit 地面上加一个视角相关高光层。这是有限近似，未处理灯到地面的遮挡，不是完整 LTC 面光实现，也没有验证手机成本。灯饰的自发光必须同时设置 GI 标记；仅写颜色和启用 keyword 会被 Unity 材质导入校验关闭。

本实验的 Probe 范围、光照探针网格和地面对象选择仍是晚宴专用，未接入正式 build/publish 链。所有实体如何区分静态与可移动、光照资源如何与 City/Places 拆分共同加载、最终后处理和移动镜头仍需验证。质量条件没有满足前，不提升为正式游戏配置。

最终试用记录：独立 Unity 编译、烘焙和 Shader 渲染成功。将钢琴、卡座和细小自发光对象改为探针照明后，本轮日志不再报告 UV 重叠。面光高光校准强度为 0.08；地面禁用 Reflection Probe 的镜面贡献，避免与该层重复叠加。材质 HDR 自发光使用源线性值，GI 标记与 keyword 一并设置。

试用包重载主工程后，通过 Unity 原生界面截图比较同一 1920×1080 Game 视图的 Editor 和 Play：三盏吊灯自发光、地面柔和高光、静态阴影没有观察到明显跳变。`comparison-baked-area.png` 左为 Blender 原预览，右为本轮独立 Unity 截图。高光位置/扩散形状、描线的辉光和装饰的局部明暗仍有差异，尚未通过正式发布验收；Shader 不处理面光遮挡、移动端未验证。

## 晚宴色块清理

用户接受当前反光近似后，继续处理金色、象牙色和地面显灰杂的问题。原候选 EEVEE 没有烘焙间接光；Unity 之前却新增了自发光装饰的 GI 贡献和房间反射探针，导致对照多了额外光照层。本轮移除反射探针生成，材质关闭环境镜面贡献；自发光颜色和 keyword 保留，GI 标记改为 None，关闭间接光输出。直接光强度初始换算比例为 0.35，样本增至 512，关闭光照贴图压缩，明确使用 NonDirectional 光照贴图。

地面面光层由平顶圆盘近似改为连续 Gaussian 卷积近似，校准强度 0.12，避免多个灰色亮斑带来污渍观感。这些参数仍属于试用阶段的光照校准，不能当作所有场景共同基准。未修改 Blender 原候选的底色色盘或几何。

清理版验证：使用 Unity 2022.3 的高采样、Gaussian 轻量过滤（无 OIDN）、无压缩 NonDirectional 光照贴图成功烘焙，日志未报告 UV 重叠或 Shader 错误。独立试用配置提高到 4× MSAA。新版发布后重载主工程，原生截图检查 Editor / Play，未观察到明显模式跳变；恢复用户原来的 Play 状态。`comparison-clean.png` 保留本轮调整前后对照。地面过渡有所改善，金色/象牙色与 Blender 的局部高光层次仍不等价，不能据此宣称全部视觉差距已消失。


## CLI 参数迭代（2026-10-04）

不需要 Blender 插件、Unity 面板或手动接收。首次用上面的完整预览建立独立工程；之后从仓库根目录执行：

```sh
python3 tools/citybox-unity-preview/preview.py --scene 晚宴 \
  --parameters tools/citybox-unity-preview/examples/晚宴-参数.json
```

本机已有缓存的验证工程在 `/tmp/ssnoir-unity-visual-review`，可追加 `--project /tmp/ssnoir-unity-visual-review` 复用。输出默认 `.cache/citybox-parameters/晚宴/preview.png`，可用 `--out <目录>` 指定；`result.json` 记录真实渲染器及是否重烘焙，`parameters.visual.json` 保存本次完整参数，日志在同目录。**成功退出并生成本轮 result.json 才表示预览完成**；失败时不要把输出目录已有的 PNG 当成本轮结果。

`--parameters` 的 JSON 按名称覆盖 `materials`、`lights`、`cameras` 中的部分参数；材质颜色使用线性 RGBA，空间参数沿用 `scene.visual.json` 坐标及单位。未指定字段保持独立缓存场景的当前值。示例改变象牙色和金色粗糙度，只用于演示参数链路，不代表新色盘。未知对象、字段或非法数值直接失败。

该模式复用几何、描线、光照贴图和 Library，不运行 Blender、不重新导出 FBX。仍需要已安装的项目版本 Unity，命令在后台启动有渲染能力的 batchmode 进程；不能使用 `-nographics`。修改烘焙灯光后必须加 `--bake-lights`，否则在修改缓存资产前失败，避免输出过期光照。当前只有晚宴验证了烘焙。相机或材质参数同步仍使用完整导入的同一转换逻辑。

每次成功后将参数及场景保存到独立缓存工程，便于继续迭代；不覆盖候选 .blend 或主 UnityClient，也不发布正式游戏。几何和描线粗细调整使用完整预览重建。缓存放在临时目录时，系统清理后需重新初始化。MCP 若需要只需包装此 CLI，无需另做接收服务。

本轮实测：Blender 5.2.2 候选导出参数、Unity 2022.3.62f3c1 / URP 14.0.12 后台参数渲染成功；首轮 CLI 热缓存材质修改耗时约 9.4 秒。这个数字只代表本机本轮，重新烘焙和首次导入另计。尚未接入正式游戏发布链。
