# Tripo 3D 低模生成

项目默认用 `tools/tripo/model.py` 将已确认的参考图生成候选网格。工具用 macOS Keychain 中的 API Key，通过 `Authorization: Bearer {api_key}` 请求 Tripo v3 API；Key 不写在 Skill 或资产清单中。命令与鉴权设置见工具 README。

## 选型

游戏资产默认固定 `P1-20260311`。Tripo 的[模型选型页](https://developers.tripo3d.ai/en/docs/models-and-versions)推荐 P1 用于低模、移动端游戏资产；[P 系列单图接口](https://developers.tripo3d.ai/zh/docs/generation-image-to-model/p)说明 P1 针对低面数与干净拓扑优化，`face_limit` 范围 50–20,000。先依据资产在目标镜头中的大小定面数预算；没有明确预算时省略 `face_limit`，由服务自适应，不机械套用固定数值。P1 不支持 H 系列的 `smart_low_poly`，也不支持 `quad`；需要四边面时另行评估 P2。

直接生成低模后，按 `mesh-generation.md` 审轮廓、结构、面数、表面噪声与拓扑健康。只有形体合格而拓扑不合格时，才考虑另一次重拓扑任务。生成结果是候选资产，不自动进入正式 CityBox 或 Unity 资源。
