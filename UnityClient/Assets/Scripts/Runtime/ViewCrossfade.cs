#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SSNoir
{
    /// <summary>
    /// 交叉溶解：在相机切走之前把当前这一镜渲进一张贴图，切完之后让这张旧画面淡出。
    ///
    /// 一个 3D 场景里没有"两个窗口"可以互相溶解，所以旧画面得自己留下来。新机位在这张
    /// 贴图底下已经就位，贴图退掉的过程就是整个过渡，全程没有任何东西在动。
    ///
    /// 留旧画面的办法是**另开一台相机照着旧机位渲一帧**，不是去抓屏。抓后台缓冲看着更省事，
    /// 但那是一张已经编码成显示值的图，再走一遍 GUI 的采样和输出就多编码了一次，线性工程里
    /// 整张冻帧会比真实画面亮一大截——切镜时那下发白就是这么来的，而且抓屏还会把 UI 一起
    /// 冻进去、在某些图形 API 上下颠倒。相机渲 RenderTexture 是引擎的常规路径，颜色空间、
    /// 朝向、后处理都按正常管线走，这三件事一次全没了，冻的也只有世界，UI 不参与。
    ///
    /// 时序：发起方必须先把目标机位按在旧视角上停住（见
    /// <c>SSNoirCameraManager.BeginReducedFocusChange</c>）——Cinemachine 会在 LateUpdate 把
    /// 相机切走，不停这一下，抓到的就是新机位，等于拿新画面溶解新画面，什么都看不见。
    ///
    /// **停几帧不能靠数。** 抓帧相机什么时候真的渲，取决于发起当时处在一帧的哪个位置：
    /// 从 Update 发起，它当帧的渲染循环里就渲了；从 OnGUI 发起就晚一帧——OnGUI 跑在
    /// 相机渲染**之后**，这时才启用的相机要等下一帧的渲染循环。而点卡片、点面包屑这些
    /// 恰恰全是 OnGUI。所以这里不数帧，直接听 <see cref="RenderPipelineManager"/> 报告
    /// 抓帧相机渲完，收到了才开始淡出。数帧的那一版会在 OnGUI 这条路上把还没渲的抓帧
    /// 相机提前关掉，于是贴图里留着的是**上一次**溶解的旧画面（或者干脆是未初始化的
    /// 显存）——切镜时先闪一张不知哪来的画面，就是这么来的。
    /// </summary>
    public class ViewCrossfade
    {
        private const string CaptureCameraName = "SSNoir.ViewCrossfade.Capture";
        /// <summary>等抓帧等到这么多帧还没等到就认输。只是保险丝，正常路径上是 1～2 帧。</summary>
        private const int CaptureTimeoutFrames = 8;

        private static ViewCrossfade? _active;

        /// <summary>由世界全屏 pass 读取；只在冻帧真正开始淡出后出现。</summary>
        internal static RenderTexture? ActiveFrozenView => _active?.FrozenView;

        /// <summary>由世界全屏 pass 读取，与 <see cref="ActiveFrozenView"/> 属于同一次溶解。</summary>
        internal static float ActiveAlpha => _active?.Alpha ?? 0f;

        private readonly MonoBehaviour _runner;

        private RenderTexture? _frozenView;
        private Camera? _captureCamera;
        private int _captureFrame = -1;
        private bool _isCapturing;
        private bool _captureRendered;
        private bool _listening;
        private float _startedAt;
        private float _duration;
        private bool _isFading;

        public ViewCrossfade(MonoBehaviour runner)
        {
            _runner = runner;
        }

        public bool IsFading => _isFading;

        /// <summary>
        /// 冻帧还没抓到手。这段时间画面必须继续停在旧那一镜上：发起方的「停一帧」要停到
        /// 这里说完为止，长短由渲染循环说了算，不是一个能写死的帧数。
        /// </summary>
        public bool IsCapturing => _isCapturing;

        /// <summary>正在淡出的旧画面；没有溶解在跑时为 null，绘制方据此决定画不画。</summary>
        public RenderTexture? FrozenView => _isFading ? _frozenView : null;

        /// <summary>1 = 旧画面完全盖住新机位，0 = 已经完全让位。</summary>
        public float Alpha { get; private set; }

        /// <summary>
        /// 登记一次溶解，并让抓帧相机就位。必须在相机被切走**之前**调用，且调用时
        /// <paramref name="source"/> 必须还站在要留下的那一镜上。
        /// </summary>
        public void Begin(float duration, Camera source)
        {
            if (duration <= 0f)
                return;

            Finish();

            EnsureTarget(source.pixelWidth, source.pixelHeight);
            if (_frozenView == null)
                return;

            var capture = EnsureCaptureCamera();

            // CopyFrom 带走镜头、剔除、清除方式这些，但不带 transform，也不带 URP 那份
            // 附加数据——后处理没跟过来的话，冻帧会和实时画面亮度对不上。
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

            capture.targetTexture = _frozenView;
            capture.enabled = true;

            _duration = duration;
            _captureFrame = Time.frameCount;
            _captureRendered = false;
            _isCapturing = true;
            _active = this;
            Listen(true);
        }

        /// <summary>每帧推进。溶解走 unscaledTime，剧本节拍锁住输入时它照样要走完。</summary>
        public void Tick()
        {
            // 抓帧相机在登记当帧的帧末才渲，所以至少要跨过一帧才能收工。Tick 和 Begin 同在
            // Update 里跑，谁先谁后不定，用帧号卡死，别让同帧的 Tick 把还没渲的一帧收走。
            if (_isCapturing)
            {
                if (!_captureRendered)
                {
                    // 兜底：抓帧相机万一一直没渲（被别的东西关掉、管线被换掉），不能就这么
                    // 把画面停在旧机位上不动了。放弃这次溶解，退回一次硬切——难看，但比卡住
                    // 或者拿一张陈年贴图糊上去都强。
                    if (Time.frameCount > _captureFrame + CaptureTimeoutFrames)
                        Finish();
                    return;
                }

                if (_captureCamera != null)
                {
                    _captureCamera.enabled = false;
                    _captureCamera.targetTexture = null;
                }

                _isCapturing = false;
                Listen(false);
                _startedAt = Time.unscaledTime;
                Alpha = 1f;
                _isFading = true;
                return;
            }

            if (!_isFading)
                return;

            float t = Mathf.Clamp01((Time.unscaledTime - _startedAt) / _duration);
            if (t >= 1f)
            {
                Finish();
                return;
            }

            // 线性。新机位在底下是完全不透明的，所以合成结果就是一次真正的等速交叉溶解，
            // 和胶片叠化同一条曲线。缓动曲线会把变化压在中段、两头各按住一会儿不动，
            // 看上去就成了"停一下、闪过去、再停一下"。
            Alpha = 1f - t;
        }

        /// <summary>
        /// 立刻结束溶解。旧画面被丢掉时新机位早就在底下了，所以这不会留下半张画面，
        /// 只是让过渡提前结束——场景过渡要接管画面时走的就是这条。
        /// </summary>
        public void Finish()
        {
            if (_captureCamera != null)
            {
                _captureCamera.enabled = false;
                _captureCamera.targetTexture = null;
            }

            _isCapturing = false;
            Listen(false);
            _isFading = false;
            Alpha = 0f;

            if (ReferenceEquals(_active, this))
                _active = null;
        }

        /// <summary>
        /// 抓帧相机渲完这一趟没有。只认自己那台：同一帧里主相机、SceneView 都会走这条回调。
        /// </summary>
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_isCapturing && _captureCamera != null && ReferenceEquals(camera, _captureCamera))
                _captureRendered = true;
        }

        private void Listen(bool on)
        {
            if (on == _listening)
                return;

            if (on)
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            else
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            _listening = on;
        }

        private Camera EnsureCaptureCamera()
        {
            if (_captureCamera != null)
                return _captureCamera;

            // 挂在发起方身上，跟着场景一起销毁；不加 AudioListener，只是一台渲进贴图的相机。
            var go = new GameObject(CaptureCameraName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_runner.transform, worldPositionStays: false);
            _captureCamera = go.AddComponent<Camera>();
            _captureCamera.enabled = false;
            return _captureCamera;
        }

        private void EnsureTarget(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            if (_frozenView != null && _frozenView.width == width && _frozenView.height == height)
                return;

            if (_frozenView != null)
            {
                _frozenView.Release();
                Object.Destroy(_frozenView);
            }

            // 深度位必须给够，这台相机要正经渲一遍世界，不是拷贝一张图。
            _frozenView = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
            {
                name = "SSNoir.ViewCrossfade",
            };
        }
    }
}
