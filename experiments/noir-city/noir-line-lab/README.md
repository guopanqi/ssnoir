# Noir 描线设计工具

独立 Three.js 美术设计工具。用于比较真实建筑/室内样本的白线疏密、线宽、作者选线、手绘笔触和体积填充，不参与正式游戏构建，也不会修改游戏全部亮、暗描线。

## 启动

在仓库根目录执行：

```sh
python3 experiments/noir-city/noir-line-lab/build.py
python3 -m http.server 8770 --directory experiments/noir-city/noir-line-lab
```

打开 <http://localhost:8770>。也可以直接打开生成的 `index.html`。Three.js 固定使用 CDN 的 0.160.0，需要网络；其他样本数据内嵌在页面中。

## 日常使用

选择诊所或咖啡馆室内剖切；拖动旋转、Shift＋拖动平移、滚轮缩放。预设可继续调整，点击“固定线条参数为 A”后开启同步左右比较。“复制当前参数”导出 JSON，剪贴板不可用时页面提供手动复制文本。重新落笔改变稳定随机种子，静止画面没有持续抖动。

用户选定参数保存于 `presets/诊所选定.json`，也已作为页面首个预设。其他长期保留的参数可在 presets 下保存 JSON，并按 template.html 的 presets 数组添加对应预设后重新打包。页面中的临时改动不会自动写回磁盘。

## 文件与样本维护

- `template.html`：界面、预设和 WebGL 笔触实现，唯一页面源。
- `models.json`：实际模型的几何与候选边快照，日常使用无需 Blender。
- `export.py`：从工程正式 Prefab 更新快照；在仓库根运行 `blender -b --python experiments/noir-city/noir-line-lab/export.py`，然后再次运行 build.py。
- `build.py`：打包生成 index.html（忽略生成文件，不维护第二份页面源）。

样本来自 `city-box/prefabs/诊所.blend` 和 `街角咖啡馆.blend`。咖啡馆为室内观察剖切；作者选线结合原 outline 声明及构件命名，诊所重复檐带/窗中梃等有明确筛选，不能直接推断任意新模型的语义。新增模型需要检查 export.py 中的分类与筛选，并维护模型选择项。

## 与 Unity 的关系

此工具以屏幕空间笔触探索视觉方向。角度筛边、投影短线过滤、动态剪影、断笔、复描、颗粒与明暗在 Three.js 中运行；不是游戏现行描线后处理。参数不能原样直接控制 Unity 的全部亮暗描线。

本次诊所离线应用候选、参数映射限制和 Unity 对照见 [诊所审阅记录](../../../city-box/prefabs/review/诊所/参数应用-20261024/README.md)。烘焙几何可近似抖动/断笔，但线宽随镜头变化，颗粒与透明度不是逐像素复现；正式采纳仍需独立审阅。
