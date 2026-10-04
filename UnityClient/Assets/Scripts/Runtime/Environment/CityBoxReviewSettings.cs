using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir
{
    /// <summary>独立视觉试用场景使用相同的编辑与运行渲染配置。</summary>
    [ExecuteAlways]
    public sealed class CityBoxReviewSettings : MonoBehaviour
    {
        public RenderPipelineAsset Pipeline;
        private RenderPipelineAsset _previous;
        private bool _applied;

        private void OnEnable()
        {
            if (Pipeline == null)
                throw new System.InvalidOperationException("视觉试用场景缺少渲染配置");
            _previous = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = Pipeline;
            _applied = true;
        }

        private void OnDisable()
        {
            if (_applied && QualitySettings.renderPipeline == Pipeline)
                QualitySettings.renderPipeline = _previous;
            _applied = false;
        }
    }
}
