#nullable enable
using System.Collections;
using UnityEngine;

namespace SSNoir
{
    // transitionVCam must be configured in the Inspector with Body = "Do Nothing" and Aim = "Do Nothing"
    // so that moving its transform drives the camera directly with no Cinemachine smoothing.
    public class StageTransitionController : MonoBehaviour
    {
        [SerializeField] private Cinemachine.CinemachineVirtualCamera? transitionVCam;

        // Portal 的手感全在代码里，不挂 Inspector（理由同下面的过夜黑场）。
        //
        // 门有两端：A 是城市侧的 PortalIn（挂在 StagePortalConfig 上），B 是交锋根机位 Camera_<交锋名>
        // （StagePortalConfig 挂在同名 Anchor 上，游戏本来就要解析它的焦点相机）。
        //   进门：当前机位沿视轴出发 → 最后几米顺着 A 的视线俯冲 → 越过门线时最快，黑
        //         → 亮起时在 B 后上方（视轴反向）→ 落进 B 坐稳。
        //   出门：从 B 沿视轴向后拉到同一点，黑 → 亮起时在 A 门外 → 倒退升回城市机位。
        // 用到的空间只有两处：A 前方 PortalPushDistance、B 身后 PortalRevealRatio × |B−Anchor|。
        // 两处净空由 city-box/pipeline/export.py 在有几何的地方 ray_cast 核过，不够直接构建失败，
        // 所以运行时不做任何避障，常量改了要同步那边。
        private const float PortalHalfDuration = 0.9f;
        private const float PortalFlashDuration = 0.12f;
        // 黑场之前越过门线的距离。
        private const float PortalPushDistance = 1.5f;
        // 门轴那一头的贝塞尔手柄：最后几米必须顺着门的视线进出，门才像一扇门。
        private const float PortalDoorHandle = 6.0f;
        // 机位那一头的手柄占弦长的比例：离开 / 到达机位都沿它自己的视轴，像一次推拉。
        private const float PortalShotHandleRatio = 0.35f;
        // 揭幕起点在根机位身后多远：按机位到 Anchor 的距离取比例，远机位落得长、近机位落得短。
        private const float PortalRevealRatio = 0.35f;
        // 过夜黑场只有两个对称半程：暗下去，再亮回来。它本来就不动镜头，
        // 「减少动画」对它没话说，两种模式一个节奏。
        //
        // 写成常量、不挂 Inspector：完整动画下这是这个转场的手感，不是某个场景实例的配置。
        // 挂上去就多一份能和代码对不上的真相——场景里存着旧值时，改这里等于没改。
        private const float TurnDipHalfDuration = 0.39f;
        [SerializeField] private int transitionPriority = 100;

        private SSNoirGameManager _gameManager = null!;
        private Cinemachine.CinemachineBrain? _brain;
        private string? _currentContextId;
        private StagePortalConfig? _activePortal;
        private bool _turnDipActive;

        public bool IsTransitioning { get; private set; }
        public float FadeAlpha { get; private set; }
        public string? CurrentContextId => _currentContextId;
        public bool HasActivePortal => _activePortal != null;

        public bool OwnsStageContext(string contextId)
        {
            return _activePortal != null && string.Equals(_currentContextId, contextId, System.StringComparison.Ordinal);
        }

        public void Initialize(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
            _brain = Camera.main != null ? Camera.main.GetComponent<Cinemachine.CinemachineBrain>() : FindObjectOfType<Cinemachine.CinemachineBrain>();
            _currentContextId = gameManager.CurrentStageContextId;
            if (transitionVCam != null)
                transitionVCam.Priority = 0;
        }

        private void Update()
        {
            ReconcileContext();
        }

        /// <summary>
        /// 在表现快照落地的确定时点接管 Stage 边界。Update 仍调用它作为安全网，但 Debug
        /// 直载和正式交锋不再依赖“下一帧刚好先轮询、还是焦点系统先动”的执行顺序。
        /// </summary>
        public void ReconcileContext()
        {
            if (_gameManager == null || IsTransitioning) return;
            var newContextId = _gameManager.CurrentStageContextId;
            if (newContextId != _currentContextId)
                StartCoroutine(TransitionTo(newContextId));
        }

        private IEnumerator TransitionTo(string? newContextId)
        {
            IsTransitioning = true;
            _gameManager.SetInputLocked(true);

            string? lookupId = newContextId ?? _currentContextId;
            var portal = ResolvePortal(lookupId);

            // Portal 不听「减少动画」：它走得不勤，而且推进 / 穿过 / 拉出这三段路本身就是
            // 在告诉玩家"剧院在城里的哪儿、你现在进到了里面"。砍掉它，空间关系就断了。
            // 减少动画管的是城里逛地点那种高频翻页，见 MotionSettings。
            if (newContextId != null)
            {
                if (_activePortal != null)
                {
                    yield return PushExit(_activePortal);
                    _activePortal = null;
                }

                if (portal != null)
                {
                    yield return PushEnter(portal);
                    _activePortal = portal;
                }
            }
            else if (_activePortal != null)
            {
                yield return PushExit(_activePortal);
                _activePortal = null;
            }

            _currentContextId = newContextId;
            _gameManager.SetInputLocked(false);
            IsTransitioning = false;
        }

        private IEnumerator PushEnter(StagePortalConfig portal)
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            Debug.Assert(portal.IntroCam != null, "[StageTransition] StagePortalConfig.IntroCam not assigned.");
            if (transitionVCam == null || portal.IntroCam == null) yield break;

            var introCam = portal.IntroCam.transform;
            var targetCamera = _gameManager.CurrentFocusCamera;
            Debug.Assert(targetCamera != null, "[StageTransition] Enter target focus camera was not resolved.");

            FadeAlpha = 0f;
            // 城市半程由 PortalIn 所属地点提供细节；通常玩家本来就在剧院，Debug
            // 直载则靠这一句补齐与正式入口相同的视觉状态。
            _gameManager.PresentCamera(portal.IntroCam);
            yield return TakeOverCurrentView();

            ResetFocusCameras();
            if (targetCamera != null)
                targetCamera.Priority = 20;

            // 外半程：从当前机位沿自己的视轴出发，最后几米顺着门的视线俯冲，越过门线时黑。
            var from = transitionVCam.transform;
            Vector3 doorEnd = introCam.position + introCam.forward * PortalPushDistance;
            yield return SwoopTransitionCamera(
                from.position, from.position + from.forward * ShotHandle(from.position, doorEnd),
                introCam.position - introCam.forward * PortalDoorHandle, doorEnd,
                from.rotation, introCam.rotation, LensOf(portal.IntroCam),
                PortalHalfDuration, AccelerateIntoBlack);

            yield return FadeTo(1f, PortalFlashDuration);
            if (targetCamera != null)
            {
                // 内半程的起点：根机位身后、视轴反向。机位用它此刻的位置，玩家上次 Pan 到哪儿就落回哪儿。
                var to = targetCamera.transform;
                Vector3 revealStart = to.position - to.forward * RevealDistance(portal, to);
                transitionVCam.transform.SetPositionAndRotation(revealStart, to.rotation);
                transitionVCam.m_Lens = LensOf(targetCamera);
                _gameManager.PresentCamera(targetCamera);
            }
            yield return null;

            yield return FadeTo(0f, PortalFlashDuration);
            if (targetCamera != null)
            {
                // 内半程：从远处沿视轴落进机位坐稳，一段直线。
                var to = targetCamera.transform;
                Vector3 revealStart = transitionVCam.transform.position;
                yield return SwoopTransitionCamera(
                    revealStart, Vector3.Lerp(revealStart, to.position, 0.35f),
                    Vector3.Lerp(revealStart, to.position, 0.65f), to.position,
                    to.rotation, to.rotation, LensOf(targetCamera),
                    PortalHalfDuration, DecelerateOutOfBlack);
            }

            yield return ReleaseTransitionCameraWithCut();
        }

        private IEnumerator PushExit(StagePortalConfig portal)
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            Debug.Assert(portal.IntroCam != null, "[StageTransition] StagePortalConfig.IntroCam not assigned.");
            if (transitionVCam == null || portal.IntroCam == null) yield break;

            var introCam = portal.IntroCam.transform;
            var targetCamera = _gameManager.CurrentFocusCamera;
            Debug.Assert(targetCamera != null, "[StageTransition] Exit target focus camera was not resolved.");

            FadeAlpha = 0f;
            yield return TakeOverCurrentView();

            ResetFocusCameras();
            if (targetCamera != null)
                targetCamera.Priority = 20;

            // 内半程：从交锋机位沿视轴向后拉，拉到进门时的落点处黑——进门那段的倒放。
            var from = transitionVCam.transform;
            Vector3 pushEnd = from.position - from.forward * RevealDistance(portal, from);
            yield return SwoopTransitionCamera(
                from.position, Vector3.Lerp(from.position, pushEnd, 0.35f),
                Vector3.Lerp(from.position, pushEnd, 0.65f), pushEnd,
                from.rotation, from.rotation, transitionVCam.m_Lens,
                PortalHalfDuration, AccelerateIntoBlack);

            yield return FadeTo(1f, PortalFlashDuration);
            // PortalIn 站在门外朝门内看：黑场后从这儿揭幕，正好是"刚退出门、回头还看着门"。
            transitionVCam.transform.SetPositionAndRotation(introCam.position, introCam.rotation);
            transitionVCam.m_Lens = LensOf(portal.IntroCam);
            _gameManager.PresentCamera(portal.IntroCam);
            yield return null;

            yield return FadeTo(0f, PortalFlashDuration);
            if (targetCamera != null)
            {
                // 外半程：倒退离开门，升回城市机位，沿它的视轴退进去坐稳。
                var to = targetCamera.transform;
                yield return SwoopTransitionCamera(
                    introCam.position, introCam.position - introCam.forward * PortalDoorHandle,
                    to.position + to.forward * ShotHandle(introCam.position, to.position), to.position,
                    introCam.rotation, to.rotation, LensOf(targetCamera),
                    PortalHalfDuration, DecelerateOutOfBlack);
            }

            if (targetCamera != null)
                _gameManager.PresentCamera(targetCamera);
            yield return ReleaseTransitionCameraWithCut();
        }

        /// <summary>
        /// 落点离根机位多远。Portal 配置挂在 Stage 自己的 Anchor 上，机位到它的距离就是
        /// "机位到主体"的距离；按比例取，大厅落得长、地下室落得短。同一公式在导出端核净空。
        /// </summary>
        private static float RevealDistance(StagePortalConfig portal, Transform shot)
            => Vector3.Distance(shot.position, portal.transform.position) * PortalRevealRatio;

        private static float ShotHandle(Vector3 a, Vector3 b)
            => Vector3.Distance(a, b) * PortalShotHandleRatio;

        // 黑场前：起步慢、越过门线时最快。
        private static float AccelerateIntoBlack(float t) => t * t;

        // 黑场后：从门口带着速度出来，落进机位时归零。
        private static float DecelerateOutOfBlack(float t) => 1f - (1f - t) * (1f - t);

        private IEnumerator TakeOverCurrentView()
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            if (transitionVCam == null) yield break;

            var sourceTransform = _brain != null && _brain.OutputCamera != null
                ? _brain.OutputCamera.transform
                : transitionVCam.transform;

            transitionVCam.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
            // 镜头参数也要一起接过来。过渡相机自己的远裁剪只有 100m，直接顶上去的话，城市机位
            // 眼前的整座城都在裁剪面外——画面先是一片底色，再随着俯冲一点点"长"出来。
            if (_brain != null && _brain.OutputCamera != null)
            {
                var output = _brain.OutputCamera;
                SetLens(output.fieldOfView, output.nearClipPlane, output.farClipPlane);
            }
            yield return CutToVirtualCamera(transitionVCam, transitionPriority);
        }

        private void SetLens(float fieldOfView, float nearClip, float farClip)
        {
            if (transitionVCam == null) return;
            var lens = transitionVCam.m_Lens;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = nearClip;
            lens.FarClipPlane = farClip;
            transitionVCam.m_Lens = lens;
        }

        private static Cinemachine.LensSettings LensOf(Cinemachine.CinemachineVirtualCamera vcam)
            => vcam.m_Lens;

        /// <summary>
        /// 沿三次贝塞尔把过渡相机从 p0 送到 p3。按弧长走，速度只由 <paramref name="ease"/> 决定，
        /// 手柄长短只改路的形状不改节奏；姿态沿同一进度球面插值。
        /// </summary>
        private IEnumerator SwoopTransitionCamera(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
            Quaternion r0, Quaternion r3, Cinemachine.LensSettings lens3,
            float duration, System.Func<float, float> ease)
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            if (transitionVCam == null) yield break;

            var lens0 = transitionVCam.m_Lens;

            const int Samples = 48;
            var arc = new float[Samples + 1];
            Vector3 prev = p0;
            for (int i = 1; i <= Samples; i++)
            {
                Vector3 pt = Bezier(p0, p1, p2, p3, (float)i / Samples);
                arc[i] = arc[i - 1] + Vector3.Distance(prev, pt);
                prev = pt;
            }
            float total = arc[Samples];

            if (duration <= 0f || total <= 0.0001f)
            {
                transitionVCam.transform.SetPositionAndRotation(p3, r3);
                SetLens(lens3.FieldOfView, lens3.NearClipPlane, lens3.FarClipPlane);
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float progress = ease(Mathf.Clamp01(t / duration));
                float u = ParameterAtArcLength(arc, progress * total);
                transitionVCam.transform.SetPositionAndRotation(
                    Bezier(p0, p1, p2, p3, u),
                    Quaternion.SlerpUnclamped(r0, r3, progress));
                SetLens(
                    Mathf.Lerp(lens0.FieldOfView, lens3.FieldOfView, progress),
                    Mathf.Lerp(lens0.NearClipPlane, lens3.NearClipPlane, progress),
                    Mathf.Lerp(lens0.FarClipPlane, lens3.FarClipPlane, progress));
                yield return null;
            }

            transitionVCam.transform.SetPositionAndRotation(p3, r3);
            SetLens(lens3.FieldOfView, lens3.NearClipPlane, lens3.FarClipPlane);
        }

        private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float s = 1f - t;
            return s * s * s * p0 + 3f * s * s * t * p1 + 3f * s * t * t * p2 + t * t * t * p3;
        }

        private static float ParameterAtArcLength(float[] arc, float length)
        {
            int n = arc.Length - 1;
            for (int i = 1; i <= n; i++)
            {
                if (length <= arc[i])
                {
                    float seg = arc[i] - arc[i - 1];
                    float local = seg <= 0.0001f ? 1f : (length - arc[i - 1]) / seg;
                    return (i - 1 + local) / n;
                }
            }
            return 1f;
        }

        private IEnumerator CutToVirtualCamera(Cinemachine.CinemachineVirtualCamera camera, int priority)
        {
            var originalBlend = _brain != null ? _brain.m_DefaultBlend : default;
            bool hasBrain = _brain != null;
            if (hasBrain)
                _brain!.m_DefaultBlend = new Cinemachine.CinemachineBlendDefinition(Cinemachine.CinemachineBlendDefinition.Style.Cut, 0f);

            camera.Priority = priority;
            yield return null;

            if (hasBrain)
                _brain!.m_DefaultBlend = originalBlend;
        }

        private IEnumerator ReleaseTransitionCameraWithCut()
        {
            var originalBlend = _brain != null ? _brain.m_DefaultBlend : default;
            bool hasBrain = _brain != null;
            if (hasBrain)
                _brain!.m_DefaultBlend = new Cinemachine.CinemachineBlendDefinition(Cinemachine.CinemachineBlendDefinition.Style.Cut, 0f);

            if (transitionVCam != null)
                transitionVCam.Priority = 0;
            yield return null;

            if (hasBrain)
                _brain!.m_DefaultBlend = originalBlend;
        }

        /// <summary>
        /// 过夜黑场：柔和地渐暗 → 在触底的一刻把世界翻到下一回合 → 柔和地渐亮。
        ///
        /// 时间翻页和走进一扇门是两件事，但看起来该是同一种东西——都是"这一镜结束了"。
        /// 所以它借的是 portal 那块同一张黑幕（<see cref="FadeAlpha"/>，由 IMGUI 全屏画在
        /// 最上层，连 UI 一起盖住），玩家不会觉得多了一种新特效。
        ///
        /// 它和穿门的区别只在于没有镜头运动。「减少动画」对它没话说：
        /// 这里没有任何东西在移动，两种模式一个节奏。
        ///
        /// <paramref name="atBlack"/> 在最黑的那一帧调用，世界的变化都藏在它里面。
        /// </summary>
        public IEnumerator PlayTurnDip(System.Action? atBlack)
        {
            // 正在穿门的时候不抢黑幕：那边已经在放一次过渡了，两层黑叠起来只会闪。
            if (IsTransitioning)
            {
                atBlack?.Invoke();
                yield break;
            }

            yield return FadeOutForTurn();
            yield return FinishTurnDip(atBlack);
        }

        /// <summary>
        /// 只走"闭眼"这半程。给的是**按下去就开始黑**的用法：演出（那根进度条）和渐黑
        /// 同时跑，玩家按完手就已经在往下沉，而不是等结算播完才想起来要睡。
        /// 另一半由 <see cref="FinishTurnDip"/> 收尾，两者必须成对。
        /// </summary>
        public IEnumerator FadeOutForTurn()
        {
            if (IsTransitioning)
                yield break;

            _turnDipActive = true;
            _gameManager.SetInputLocked(true);
            yield return FadeLinear(1f, TurnDipHalfDuration);
        }

        /// <summary>
        /// "睁眼"这半程：等黑透（演出可能比渐黑还短），在触底处翻页，然后立刻亮回来。
        /// </summary>
        public IEnumerator FinishTurnDip(System.Action? atBlack)
        {
            if (!_turnDipActive)
            {
                atBlack?.Invoke();
                yield break;
            }

            // 演出比渐黑短的时候，这里补上剩下的那截黑：翻页永远发生在全黑里。
            while (FadeAlpha < 1f)
                yield return null;

            atBlack?.Invoke();
            // 不额外等待：翻页发生在 FadeAlpha == 1 的这一刻，下一步直接进入对称的亮起半程。
            yield return FadeLinear(0f, TurnDipHalfDuration);
            _turnDipActive = false;
            _gameManager.SetInputLocked(false);
        }

        /// <summary>
        /// 出事时把黑幕收掉。早黑是在结算**之前**起的，如果演出那头抛了异常，
        /// 收尾的那一半永远不会跑——没有这条，玩家就留在一块黑屏里。
        /// </summary>
        public void AbortTurnDip()
        {
            if (!_turnDipActive)
                return;
            _turnDipActive = false;
            FadeAlpha = 0f;
            _gameManager.SetInputLocked(false);
        }

        /// <summary>
        /// 闭眼睁眼用的线性溶解，节奏与 <see cref="ViewCrossfade"/> 保持一致。
        ///
        /// 交叉溶解的底层画面是不透明的，因此线性 alpha 就是等速的画面替换。
        /// 缓动反而会让两头停住、中间集中变化，和减少动画模式的观感不一致。
        ///
        /// 时间走 unscaled：黑场不该被任何慢放或暂停影响。
        /// </summary>
        private IEnumerator FadeLinear(float target, float duration)
        {
            if (duration <= 0f)
            {
                FadeAlpha = target;
                yield break;
            }

            float start = FadeAlpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                FadeAlpha = Mathf.Lerp(start, target, k);
                yield return null;
            }
            FadeAlpha = target;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = FadeAlpha;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                FadeAlpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            FadeAlpha = target;
        }

        private void ResetFocusCameras()
        {
            var dir = _gameManager.SceneDirectory;
            if (dir == null) return;
            foreach (var anchor in dir.AllAnchors)
            {
                if (anchor.FocusVirtualCamera != null)
                    anchor.FocusVirtualCamera.Priority = 5;
            }
        }

        private StagePortalConfig? ResolvePortal(string? contextId)
        {
            if (contextId == null) return null;
            var anchor = _gameManager.ResolveAnchor(contextId);
            return anchor?.GetComponent<StagePortalConfig>();
        }
    }
}
