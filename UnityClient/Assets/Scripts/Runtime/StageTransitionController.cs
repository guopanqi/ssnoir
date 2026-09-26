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
        // 门有两端：A 是城市侧的 PortalIn（挂在 StagePortalConfig 上），B 是 Stage 根机位 Camera_<名>
        // （StagePortalConfig 挂在同名 Anchor 上，游戏本来就要解析它的焦点相机）。
        //   进门：城市半程走**和城里点地点一样的焦点弧线**（SSNoirCameraManager.BeginFocusTravel）
        //         从当前机位绕到 A——看着楼推进去，而不是另一套沿视轴的贝塞尔；最后一截边走边黑
        //         → 亮起时在 B 后上方（视轴反向）→ 落进 B 坐稳。
        //   出门：从 B 沿视轴向后拉，黑 → 亮起时在 A 门外 → 同一条焦点弧线升回城市机位。
        // 弧线解不出来（两头都找不到兴趣点）才退回旧的贝塞尔俯冲。
        // 用到的空间只有两处：A 前方 PortalPushDistance、B 身后 PortalRevealRatio × |B−Anchor|。
        // 两处净空由 city-box/pipeline/export.py 在有几何的地方 ray_cast 核过，不够直接构建失败，
        // 所以运行时不做任何避障，常量改了要同步那边。
        //
        // 减少动画：进出交锋（根节点换了）不听它——那段路在交代"剧院在城里哪儿、你进到了里面"；
        // 世界里点进一扇门（家 → 租屋，根节点没换）是玩家自己翻页，一局要走很多次，听它：
        // 和城里点地点一样，硬切盖一层溶解（SSNoirCameraManager 的减少动画分支），不黑场——
        // 黑场留给"时间过去了 / 进了一场戏"。
        private const float PortalHalfDuration = 0.9f;
        // 黑场压在运动上，不是运动停了再黑：外半程最后这一段边冲边暗、抵达门线那一帧正好全黑；
        // 内半程从黑里边亮边落，亮透时机位还在往前走。切换本身被黑场吃掉，两头都看不见"停"。
        // 之前是冲到门口停住 → 0.12s 暗 → 换机位 → 0.12s 亮 → 再起步，门前那一顿正是停下来在等黑场。
        private const float PortalFadeFraction = 0.3f;
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
        public bool TurnDipActive => _turnDipActive;
        /// <summary>穿门的城市半程正骑在焦点弧线上：主循环这时要继续 Tick 弧线，而不是把它掐断。</summary>
        public bool RidesFocusArc { get; private set; }
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
            // 安全网轮询接到的只可能是玩家自己翻页（点进门、返回）：换根节点的快照一律在
            // 落地那一刻就已经 Reconcile 过了。
            ReconcileContext(storyDriven: false);
        }

        /// <summary>
        /// 在表现快照落地的确定时点接管 Stage 边界。Update 仍调用它作为安全网，但 Debug
        /// 直载和正式交锋不再依赖“下一帧刚好先轮询、还是焦点系统先动”的执行顺序。
        /// </summary>
        /// <param name="storyDriven">这次边界变化是不是剧情带来的（进出交锋，快照换了根节点）。
        /// 玩家在世界里点进一扇门不落新快照，走的是 Update 的轮询，永远是 false。</param>
        public void ReconcileContext(bool storyDriven)
        {
            if (_gameManager == null || IsTransitioning) return;
            var newContextId = _gameManager.CurrentStageContextId;
            if (newContextId != _currentContextId)
            {
                // 先同步占住过渡权，再启动协程。StartCoroutine 的第一段代码要到
                // Unity 调度时才执行；如果这里不先置位，落地回调后紧接着的
                // EnterForcedPlace/UpdateCameraFocus 会误以为没有 Portal 在接管，
                // 另起一条焦点运镜，随后两条 travel 互相重定向，表现为镜头突然加速。
                IsTransitioning = true;
                StartCoroutine(TransitionTo(newContextId, storyDriven));
            }
        }

        private IEnumerator TransitionTo(string? newContextId, bool storyDriven)
        {
            try
            {
                string? lookupId = newContextId ?? _currentContextId;
                var portal = ResolvePortal(lookupId);

                bool reduced = !storyDriven && MotionSettings.ReduceMotion;
                if (newContextId != null)
                {
                    if (_activePortal != null)
                    {
                        yield return reduced ? DissolveThrough() : PushExit(_activePortal);
                        _activePortal = null;
                    }

                    if (portal != null)
                    {
                        yield return reduced ? DissolveThrough() : PushEnter(portal);
                        _activePortal = portal;
                    }
                }
                else if (_activePortal != null)
                {
                    yield return reduced ? DissolveThrough() : PushExit(_activePortal);
                    _activePortal = null;
                }

                _currentContextId = newContextId;
            }
            finally
            {
                IsTransitioning = false;
            }
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

            // 外半程：和城里点一张地点卡同一条焦点弧线，绕到门口的 PortalIn；最后一截边走边黑。
            bool rode = false;
            yield return RideFocusTravelTo(portal.IntroCam, null, FadeIntoBlack, v => rode = v);
            yield return TakeOverCurrentView();
            portal.IntroCam.Priority = 5;

            ResetFocusCameras();
            if (targetCamera != null)
                targetCamera.Priority = 20;

            if (!rode)
            {
                // 弧线解不出来：退回沿视轴出发、最后几米顺着门的视线俯冲的贝塞尔。
                var from = transitionVCam.transform;
                Vector3 doorEnd = introCam.position + introCam.forward * PortalPushDistance;
                yield return SwoopTransitionCamera(
                    from.position, from.position + from.forward * ShotHandle(from.position, doorEnd),
                    introCam.position - introCam.forward * PortalDoorHandle, doorEnd,
                    from.rotation, introCam.rotation, LensOf(portal.IntroCam),
                    PortalHalfDuration, AccelerateIntoBlack, FadeIntoBlack);
            }
            FadeAlpha = 1f;

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

            if (targetCamera != null)
            {
                // 内半程：从远处沿视轴落进机位坐稳，一段直线；亮起来时已经在动。
                var to = targetCamera.transform;
                Vector3 revealStart = transitionVCam.transform.position;
                yield return SwoopTransitionCamera(
                    revealStart, Vector3.Lerp(revealStart, to.position, 0.35f),
                    Vector3.Lerp(revealStart, to.position, 0.65f), to.position,
                    to.rotation, to.rotation, LensOf(targetCamera),
                    PortalHalfDuration, DecelerateOutOfBlack, FadeOutOfBlack);
            }
            FadeAlpha = 0f;

            yield return ReleaseTransitionCameraWithCut();
            if (targetCamera != null)
                _gameManager.CameraManager.SynchronizeAfterExternalTransition(targetCamera);
        }

        private IEnumerator PushExit(StagePortalConfig portal)
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            Debug.Assert(portal.IntroCam != null, "[StageTransition] StagePortalConfig.IntroCam not assigned.");
            if (transitionVCam == null || portal.IntroCam == null) yield break;

            var introCam = portal.IntroCam.transform;
            Cinemachine.CinemachineVirtualCamera? targetCamera = null;

            FadeAlpha = 0f;
            yield return TakeOverCurrentView();

            ResetFocusCameras();

            // 内半程：从交锋机位沿视轴向后拉，拉到进门时的落点处黑——进门那段的倒放。
            var from = transitionVCam.transform;
            Vector3 pushEnd = from.position - from.forward * RevealDistance(portal, from);
            yield return SwoopTransitionCamera(
                from.position, Vector3.Lerp(from.position, pushEnd, 0.35f),
                Vector3.Lerp(from.position, pushEnd, 0.65f), pushEnd,
                from.rotation, from.rotation, transitionVCam.m_Lens,
                PortalHalfDuration, AccelerateIntoBlack, FadeIntoBlack);

            // 退出交锋时，黑场之前世界快照刚落地，送医流程可能还要把导航落到
            // 诊所。目标机位必须在这段导航完成后再解析，否则会先取到世界根机位，
            // 下一帧又被诊所机位重定向，表现成一段突然加速的短运镜。
            targetCamera = _gameManager.CurrentFocusCamera;
            Debug.Assert(targetCamera != null, "[StageTransition] Exit target focus camera was not resolved.");

            // PortalIn 站在门外朝门内看：黑场后从这儿揭幕，正好是"刚退出门、回头还看着门"。
            transitionVCam.transform.SetPositionAndRotation(introCam.position, introCam.rotation);
            transitionVCam.m_Lens = LensOf(portal.IntroCam);
            _gameManager.PresentCamera(portal.IntroCam);
            yield return null;

            if (targetCamera != null)
            {
                // 外半程：黑里切到 PortalIn，再走焦点弧线升回城市机位；亮起来时已经在退。
                ResetFocusCameras();
                yield return CutToVirtualCamera(portal.IntroCam, 20);
                if (transitionVCam != null) transitionVCam.Priority = 0;
                yield return null;
                bool rode = false;
                yield return RideFocusTravelTo(targetCamera, portal.IntroCam, FadeOutOfBlack, v => rode = v);
                portal.IntroCam.Priority = 5;
                if (!rode)
                {
                    yield return TakeOverCurrentView();
                    var to = targetCamera.transform;
                    yield return SwoopTransitionCamera(
                        introCam.position, introCam.position - introCam.forward * PortalDoorHandle,
                        to.position + to.forward * ShotHandle(introCam.position, to.position), to.position,
                        introCam.rotation, to.rotation, LensOf(targetCamera),
                        PortalHalfDuration, DecelerateOutOfBlack, FadeOutOfBlack);
                }
                targetCamera.Priority = 20;
            }
            FadeAlpha = 0f;

            if (targetCamera != null)
                _gameManager.PresentCamera(targetCamera);
            yield return ReleaseTransitionCameraWithCut();
            if (targetCamera != null)
                _gameManager.CameraManager.SynchronizeAfterExternalTransition(targetCamera);
        }

        /// <summary>
        /// 把一段换镜交给焦点运镜（<see cref="SSNoirCameraManager.BeginFocusTravel"/>）：从此刻画面到
        /// <paramref name="destination"/>，全动画走弧线，减少动画走溶解——和城里点地点一模一样。
        /// <paramref name="fade"/> 把运镜进度映成黑幕浓度（穿门用），null 就不碰黑幕。
        /// 起不来（两头都没有兴趣点、已经在目标构图）就报 false，调用方退回贝塞尔。
        /// 运镜自己驱动 brain（切死 blend、挪目标机位），这期间过渡相机让位，主循环靠
        /// <see cref="RidesFocusArc"/> 知道要继续 Tick 它。
        /// </summary>
        private IEnumerator RideFocusTravelTo(
            Cinemachine.CinemachineVirtualCamera destination,
            Cinemachine.CinemachineVirtualCamera? leaving,
            System.Func<float, float>? fade,
            System.Action<bool> report,
            bool respectReduceMotion = false)
        {
            var cameras = _gameManager.CameraManager;
            if (!cameras.BeginFocusTravel(destination, out float duration, respectReduceMotion) || duration <= 0f)
            {
                report(false);
                yield break;
            }
            // 运镜要求目的机位当场成为活动相机：其余焦点机位一律让位，PortalIn 不在锚点名单里，得点名让。
            ResetFocusCameras();
            if (leaving != null)
                leaving.Priority = 5;
            destination.Priority = 20;
            RidesFocusArc = true;
            // 溶解的"还在路上"比它报的时长长（抓帧要等一两帧），所以两个条件都听。
            for (float t = 0f; t < duration || cameras.IsFocusTravelInFlight; t += Time.unscaledDeltaTime)
            {
                if (fade != null)
                    FadeAlpha = fade(Mathf.Clamp01(t / duration));
                yield return null;
            }
            cameras.FinishFocusTravel();
            RidesFocusArc = false;
            if (fade != null)
                FadeAlpha = fade(1f);
            report(true);
        }

        /// <summary>
        /// 减少动画下的穿门：和城里点一张地点卡一样，硬切盖一层溶解，直接落在目的机位上。
        /// 不黑场、不动镜头。溶解起不来（已经在目标构图）就直接切。
        /// </summary>
        private IEnumerator DissolveThrough()
        {
            var targetCamera = _gameManager.CurrentFocusCamera;
            Debug.Assert(targetCamera != null, "[StageTransition] Dissolve target focus camera was not resolved.");
            if (targetCamera == null) yield break;

            _gameManager.PresentCamera(targetCamera);
            bool rode = false;
            yield return RideFocusTravelTo(targetCamera, null, null, v => rode = v, respectReduceMotion: true);
            if (!rode)
            {
                ResetFocusCameras();
                yield return CutToVirtualCamera(targetCamera, 20);
            }
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

        // 黑场跟着运动走（t 是半程的时间进度）：最后 PortalFadeFraction 暗下去，到头正好全黑；
        // 开头 PortalFadeFraction 亮起来。线性——黑场底下的画面在动，缓动只会让两头发黏。
        private static float FadeIntoBlack(float t)
            => Mathf.Clamp01((t - (1f - PortalFadeFraction)) / PortalFadeFraction);

        private static float FadeOutOfBlack(float t)
            => 1f - Mathf.Clamp01(t / PortalFadeFraction);

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
        /// <paramref name="fade"/> 把时间进度映成黑场浓度，让黑场压在运动上而不是接在运动后面。
        /// </summary>
        private IEnumerator SwoopTransitionCamera(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
            Quaternion r0, Quaternion r3, Cinemachine.LensSettings lens3,
            float duration, System.Func<float, float> ease, System.Func<float, float>? fade = null)
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
                if (fade != null) FadeAlpha = fade(1f);
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                if (fade != null) FadeAlpha = fade(k);
                float progress = ease(k);
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
            if (fade != null) FadeAlpha = fade(1f);
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

        // 改优先级不是立刻生效的：vcam 在它自己的 Update 里才把新优先级报给队列（Cinemachine 2.x
        // 的 UpdateVcamPoolStatus），brain 在那之后的 LateUpdate 才换镜。协程的 yield null 恰好排在
        // 全部 Update 之后、LateUpdate 之前——只等一帧，Cut 就在 brain 真正换镜的前一刻被换回了 2 秒
        // 混合，看上去就是"亮了以后镜头还在门口，再被硬拉过去"。所以按着 Cut 直到 brain 真的换过去。
        private const int CutSettleFrames = 4;

        private IEnumerator HoldCutUntil(System.Func<bool> switched)
        {
            var originalBlend = _brain != null ? _brain.m_DefaultBlend : default;
            bool hasBrain = _brain != null;
            if (hasBrain)
                _brain!.m_DefaultBlend = new Cinemachine.CinemachineBlendDefinition(Cinemachine.CinemachineBlendDefinition.Style.Cut, 0f);

            for (int i = 0; i < CutSettleFrames; i++)
            {
                yield return null;
                if (!hasBrain || switched())
                    break;
            }
            // 换过去的那一帧还在 Cut 里；再让一帧，brain 用 Cut 把这次切换记账完。
            yield return null;

            if (hasBrain)
                _brain!.m_DefaultBlend = originalBlend;
        }

        private IEnumerator CutToVirtualCamera(Cinemachine.CinemachineVirtualCamera camera, int priority)
        {
            camera.Priority = priority;
            yield return HoldCutUntil(() => ReferenceEquals(_brain!.ActiveVirtualCamera, camera));
        }

        private IEnumerator ReleaseTransitionCameraWithCut()
        {
            if (transitionVCam != null)
                transitionVCam.Priority = 0;
            yield return HoldCutUntil(() => !ReferenceEquals(_brain!.ActiveVirtualCamera, transitionVCam));
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
            return _gameManager.ResolveStageAnchor(contextId)?.GetComponent<StagePortalConfig>();
        }
    }
}
