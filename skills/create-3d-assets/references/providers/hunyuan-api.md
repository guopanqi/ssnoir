# Hunyuan 3D API（3.0 Direct Low Poly）

此适配器只负责低成本的单阶段候选网格生成：TokenHub `hy-3d-3.0` + `generate_type=low_poly`。它在同一次计费请求中完成生成和智能拓扑，不暴露高模中间结果。

调用项目封装：

```bash
python3 tools/hunyuan-3d-generate/generate.py --request request.json
```

完整字段见 `tools/hunyuan-3d-generate/README.md`。API Key 只从本机环境变量 `HUNYUAN_3D_API_KEY` 读取，不写入请求、日志、产物或仓库。

## 边界

- 固定 3.0 和 `low_poly`；禁止降级成 `normal + 低 face_count`，后者不是相同的拓扑语义。
- 不调用 3.1，不调用独立 Retopology，不把两阶段费用藏在一次 Agent 操作里。
- 需要“高模生成 → Agent 形体检查 → 用户用量确认 → 独立拓扑”时，改用另一个服务适配器；不要扩充本工具。
- `low_poly` 模式下官方不接受精确 `face_count`，只能选择三角形或四边形拓扑口径。
- 默认不生成 PBR。项目资产进入 Blender 后按 SSNoir 的材质与描线规则加工；只有候选审查明确需要纹理时才开启。

## 完成后的质量门

API 成功只表示文件生成，不表示资产合格。先用预览图检查轮廓、主要结构、薄片、孔洞与表面噪声，再用 `audit_mesh_quality.py` 记录面数、连通分量、非流形边和退化面。失败候选不得进入游戏化加工阶段。

生成结果和 Job ID 会保存在 `tmp/hunyuan-3d-generate/`。服务端 Job ID 有效期有限，CLI 已在完成时立即下载；不要把临时 URL 当作资产来源。
