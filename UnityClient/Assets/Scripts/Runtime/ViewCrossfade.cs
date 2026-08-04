#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SSNoir
{
    /// <summary>
    /// 静止画面之间的交叉溶解。
    ///
    /// 旧、新两个机位各由一台临时相机渲成 RenderTexture，随后在 shader 中按 sRGB
    /// （人眼看到的显示值）插值。不能用“旧画面带透明度盖在实时新画面上”：项目使用
    /// Linear 色彩空间，而这个场景又有大量白色描边，两层画面由固定管线做线性光混合时，
    /// 中段会积出一层明显的灰白亮雾。
    ///
    /// 两端都冻结还有一个好处：溶解期间世界不再继续运动，效果更接近系统开启
    /// Reduce Motion 后使用的静态 dissolve。IMGUI 仍在最终画面上实时绘制，不参与冻结。
    /// </summary>
    public class ViewCrossfade
    {
        private const string CaptureCameraName = "SSNoir.ViewCrossfade.Capture";
        private const string BlendShaderResource = "PerceptualCrossfade";

        private enum Phase
        {
            Idle,
            CapturingOutgoing,
            WaitingForIncomingPose,
            CapturingIncoming,
            Blending,
            ShowingIncoming,
        }

        private readonly MonoBehaviour _runner;

        private RenderTexture? _outgoingView;
        private RenderTexture? _incomingView;
        private RenderTexture? _blendedView;
        private Camera? _captureCamera;
        private Camera? _renderedCamera;
        private Material? _blendMaterial;
        private Phase _phase;
        private int _phaseFrame = -1;
        private float _startedAt;
        private float _duration;

        public ViewCrossfade(MonoBehaviour runner)
        {
            _runner = runner;
        }

        // CameraManager 以此判断旧机位是否已经抓完、可以放开停在旧位置的虚拟相机。
        public bool IsFading =>
            _phase == Phase.WaitingForIncomingPose ||
            _phase == Phase.CapturingIncoming ||
            _phase == Phase.Blending ||
            _phase == Phase.ShowingIncoming;

        /// <summary>
        /// 当前应当盖住实时世界的完整过渡画面。它始终是不透明画面；混合已经在 shader
        /// 中完成，绘制方不应再做一次 alpha blending。
        /// </summary>
        public RenderTexture? FrozenView => _phase switch
        {
            Phase.WaitingForIncomingPose => _outgoingView,
            Phase.CapturingIncoming => _outgoingView,
            Phase.Blending => _blendedView,
            Phase.ShowingIncoming => _blendedView,
            _ => null,
        };

        /// <summary>
        /// 开始抓取旧机位。调用时 <paramref name="source"/> 必须还站在旧画面上；发起方会
        /// 把目标虚拟相机在旧位置停到抓帧结束，避免 Cinemachine 同帧把它切走。
        /// </summary>
        public void Begin(float duration, Camera source)
        {
            if (duration <= 0f)
                return;

            Finish();
            EnsureTargets(source.pixelWidth, source.pixelHeight);
            EnsureBlendMaterial();

            if (_outgoingView == null || _incomingView == null || _blendedView == null)
                throw new InvalidOperationException("ViewCrossfade 无法创建过渡用 RenderTexture。");

            _duration = duration;
            _renderedCamera = source;
            BeginCapture(source, _outgoingView);
            _phase = Phase.CapturingOutgoing;
            _phaseFrame = Time.frameCount;
        }

        /// <summary>每帧推进。溶解走 unscaledTime，剧本节拍锁住输入时它照样会完成。</summary>
        public void Tick()
        {
            switch (_phase)
            {
                case Phase.Idle:
                    return;

                case Phase.CapturingOutgoing:
                    // Capture Camera 在 Begin 当帧的帧末才渲染，至少跨过一帧再取结果。
                    if (Time.frameCount <= _phaseFrame)
                        return;

                    StopCapture();
                    _phase = Phase.WaitingForIncomingPose;
                    _phaseFrame = Time.frameCount;
                    return;

                case Phase.WaitingForIncomingPose:
                    // 进入这个阶段后 CameraManager 才会释放停在旧位置的目标机位。再等一个完整
                    // 帧，让 Cinemachine 的 LateUpdate 和主相机渲染真正到达新机位。
                    if (Time.frameCount <= _phaseFrame)
                        return;

                    if (_renderedCamera == null || _incomingView == null)
                        throw new InvalidOperationException("ViewCrossfade 抓取新机位时相机或目标丢失。");

                    BeginCapture(_renderedCamera, _incomingView);
                    _phase = Phase.CapturingIncoming;
                    _phaseFrame = Time.frameCount;
                    return;

                case Phase.CapturingIncoming:
                    if (Time.frameCount <= _phaseFrame)
                        return;

                    StopCapture();
                    _startedAt = Time.unscaledTime;
                    _phase = Phase.Blending;
                    Composite(0f);
                    return;

                case Phase.Blending:
                    float t = Mathf.Clamp01((Time.unscaledTime - _startedAt) / _duration);
                    if (t >= 1f)
                    {
                        // 不能在跨过终点的这一帧直接撤掉过渡层。帧率低时上一帧可能还只走到
                        // 0.9，直接露出实时画面就会形成一次可见跳变。先明确画满 100% 新端点，
                        // 下一帧再无缝交还实时相机。
                        Composite(1f);
                        _phase = Phase.ShowingIncoming;
                        _phaseFrame = Time.frameCount;
                        return;
                    }

                    Composite(t);
                    return;

                case Phase.ShowingIncoming:
                    if (Time.frameCount > _phaseFrame)
                        Finish();
                    return;
            }
        }

        /// <summary>立刻结束过渡，让实时的新机位重新接管世界画面。</summary>
        public void Finish()
        {
            StopCapture();
            _phase = Phase.Idle;
            _phaseFrame = -1;
            _renderedCamera = null;
        }

        private void BeginCapture(Camera source, RenderTexture target)
        {
            var capture = EnsureCaptureCamera();

            // CopyFrom 不包含 transform，也不包含 URP 的附加数据；两者都必须显式复制，
            // 否则冻帧的后处理、抗锯齿或阴影会和实时主相机不一致。
            capture.CopyFrom(source);
            capture.transform.SetPositionAndRotation(
                source.transform.position, source.transform.rotation);

            var sourceData = source.GetUniversalAdditionalCameraData();
            var captureData = capture.GetUniversalAdditionalCameraData();
            captureData.renderPostProcessing = sourceData.renderPostProcessing;
            captureData.antialiasing = sourceData.antialiasing;
            captureData.antialiasingQuality = sourceData.antialiasingQuality;
            captureData.volumeLayerMask = sourceData.volumeLayerMask;
            captureData.renderShadows = sourceData.renderShadows;

            capture.targetTexture = target;
            capture.enabled = true;
        }

        private void StopCapture()
        {
            if (_captureCamera == null)
                return;

            _captureCamera.enabled = false;
            _captureCamera.targetTexture = null;
        }

        private void Composite(float progress)
        {
            if (_outgoingView == null || _incomingView == null ||
                _blendedView == null || _blendMaterial == null)
            {
                throw new InvalidOperationException("ViewCrossfade 混合资源未初始化。");
            }

            // 两端直接拷贝，避免色彩转换的浮点误差让覆盖层端点与原始冻帧存在细微色差。
            if (progress <= 0f)
            {
                Graphics.Blit(_outgoingView, _blendedView);
                return;
            }

            if (progress >= 1f)
            {
                Graphics.Blit(_incomingView, _blendedView);
                return;
            }

            _blendMaterial.SetTexture("_IncomingTex", _incomingView);
            _blendMaterial.SetFloat("_Progress", progress);
            Graphics.Blit(_outgoingView, _blendedView, _blendMaterial);
        }

        private Camera EnsureCaptureCamera()
        {
            if (_captureCamera != null)
                return _captureCamera;

            // 挂在发起方身上，跟着场景一起销毁；不加 AudioListener，只负责渲进贴图。
            var go = new GameObject(CaptureCameraName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_runner.transform, worldPositionStays: false);
            _captureCamera = go.AddComponent<Camera>();
            _captureCamera.enabled = false;
            return _captureCamera;
        }

        private void EnsureBlendMaterial()
        {
            if (_blendMaterial != null)
                return;

            var shader = Resources.Load<Shader>(BlendShaderResource);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"找不到 Resources/{BlendShaderResource}.shader，无法执行感知色交叉溶解。");
            }

            _blendMaterial = new Material(shader)
            {
                name = "SSNoir.ViewCrossfade.Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private void EnsureTargets(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "过渡画面的尺寸必须大于零。");

            if (_outgoingView != null &&
                _outgoingView.width == width && _outgoingView.height == height)
            {
                return;
            }

            ReleaseTarget(ref _outgoingView);
            ReleaseTarget(ref _incomingView);
            ReleaseTarget(ref _blendedView);

            // 两端需要深度缓冲来完整渲染世界；混合结果只是一张全屏颜色贴图。
            _outgoingView = CreateTarget("SSNoir.ViewCrossfade.Outgoing", width, height, 24);
            _incomingView = CreateTarget("SSNoir.ViewCrossfade.Incoming", width, height, 24);
            _blendedView = CreateTarget("SSNoir.ViewCrossfade.Blended", width, height, 0);
        }

        private static RenderTexture CreateTarget(string name, int width, int height, int depth)
        {
            return new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                name = name,
            };
        }

        private static void ReleaseTarget(ref RenderTexture? target)
        {
            if (target == null)
                return;

            target.Release();
            UnityEngine.Object.Destroy(target);
            target = null;
        }
    }
}
