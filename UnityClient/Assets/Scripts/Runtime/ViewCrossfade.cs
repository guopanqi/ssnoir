#nullable enable
using System.Collections;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 交叉溶解：在相机切走之前把当前画面冻成一张贴图，切完之后让这张旧画面淡出。
    ///
    /// 一个 3D 场景里没有"两个窗口"可以互相溶解，所以旧画面得自己留下来。新机位在这张
    /// 贴图底下已经就位，贴图退掉的过程就是整个过渡，全程没有任何东西在动。
    ///
    /// 冻的是**整屏**（世界 + UI），和 macOS / iOS 减少动画里整扇窗一起淡出淡入是同一种
    /// 处理。只单独冻世界需要按需渲染一次相机，而 URP 不接受在渲染当中再渲一次，
    /// 2022.3 也没有 render request 那套 API——整屏抓帧只用最老的接口，没有版本风险。
    /// 代价是这零点几秒里变化过的面板会跟着交叉溶解一下；没变的部分溶解在自己身上，看不出来。
    ///
    /// 抓帧必须等到帧末（WaitForEndOfFrame），那时这一帧的世界和 IMGUI 都已经合成完毕。
    /// 焦点切换几乎都从 OnGUI 发起，而 Cinemachine 在 LateUpdate 才摆相机，所以发起当帧
    /// 屏幕上仍然是旧机位——抓到的正是要留下的那一张，下一帧 brain 才切过去。
    /// </summary>
    public class ViewCrossfade
    {
        private readonly MonoBehaviour _runner;

        private RenderTexture? _frozenView;
        private Coroutine? _captureRoutine;
        private float _startedAt;
        private float _duration;
        private bool _isFading;

        public ViewCrossfade(MonoBehaviour runner)
        {
            _runner = runner;
        }

        public bool IsFading => _isFading;

        /// <summary>正在淡出的旧画面；没有溶解在跑时为 null，绘制方据此决定画不画。</summary>
        public RenderTexture? FrozenView => _isFading ? _frozenView : null;

        /// <summary>1 = 旧画面完全盖住新机位，0 = 已经完全让位。</summary>
        public float Alpha { get; private set; }

        /// <summary>
        /// 登记一次溶解。必须在相机被切走**之前**调用；真正的抓帧发生在本帧帧末。
        /// </summary>
        public void Begin(float duration)
        {
            if (duration <= 0f)
                return;

            Finish();
            _captureRoutine = _runner.StartCoroutine(CaptureThenFade(duration));
        }

        /// <summary>每帧推进。溶解走 unscaledTime，剧本节拍锁住输入时它照样要走完。</summary>
        public void Tick()
        {
            if (!_isFading)
                return;

            float t = Mathf.Clamp01((Time.unscaledTime - _startedAt) / _duration);
            if (t >= 1f)
            {
                Finish();
                return;
            }

            Alpha = 1f - t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 立刻结束溶解。旧画面被丢掉时新机位早就在底下了，所以这不会留下半张画面，
        /// 只是让过渡提前结束——场景过渡要接管画面时走的就是这条。
        /// </summary>
        public void Finish()
        {
            if (_captureRoutine != null)
            {
                _runner.StopCoroutine(_captureRoutine);
                _captureRoutine = null;
            }

            _isFading = false;
            Alpha = 0f;
        }

        private IEnumerator CaptureThenFade(float duration)
        {
            yield return new WaitForEndOfFrame();

            _captureRoutine = null;

            EnsureTarget(Screen.width, Screen.height);
            if (_frozenView == null)
                yield break;

            ScreenCapture.CaptureScreenshotIntoRenderTexture(_frozenView);

            _startedAt = Time.unscaledTime;
            _duration = duration;
            Alpha = 1f;
            _isFading = true;
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

            // 必须和屏幕同尺寸，CaptureScreenshotIntoRenderTexture 按这个前提写。
            // 淡出靠 GUI.color 的 alpha 调制，贴图自身的 alpha 由不透明画面写成 1。
            _frozenView = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "SSNoir.ViewCrossfade",
            };
        }
    }
}
