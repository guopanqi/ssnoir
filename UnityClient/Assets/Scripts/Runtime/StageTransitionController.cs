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

        [SerializeField] private float approachDuration = 0.45f;
        [SerializeField] private float flashDuration = 0.15f;
        [SerializeField] private float pushDuration = 0.3f;
        [SerializeField] private float pullDuration = 0.4f;
        // Reduce motion keeps the dip to black but nothing else, so the dip carries the
        // whole transition on its own and has to be a touch longer than the flash that
        // used to sit in the middle of one.
        [SerializeField] private float reducedFadeDuration = 0.22f;
        // 过夜黑场只有两个对称半程：暗下去，再亮回来。减少动画时直接采用
        // ViewCrossfade 的溶解时长，让这两种不移动镜头的过渡保持同一节奏。
        //
        // 写成常量、不挂 Inspector：完整动画下这是这个转场的手感，不是某个场景实例的配置。
        // 挂上去就多一份能和代码对不上的真相——场景里存着旧值时，改这里等于没改。
        private const float TurnDipFullMotionHalfDuration = 0.39f;
        [SerializeField] private int transitionPriority = 100;
        [SerializeField] private AnimationCurve approachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve pushCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve pullCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve portalTravelCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

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

            if (MotionSettings.ReduceMotion)
            {
                // The door's three legs — approach, push through, pull out — are the trip,
                // and reduce motion does not take trips. What is left is the cut that was
                // always hiding in the middle of one.
                bool crossesAPortal = _activePortal != null || (newContextId != null && portal != null);
                if (crossesAPortal)
                    yield return ReducedTransition();

                _activePortal = newContextId != null ? portal : null;
            }
            else if (newContextId != null)
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

        /// <summary>
        /// The reduce-motion form of a portal: black, cut, back. The intro and out cams
        /// describe a road through the door, and no road is taken here — the destination
        /// focus camera was always the end of it, so it simply receives the shot while
        /// the screen is dark. transitionVCam is never taken over, so there is nothing
        /// to hand back afterwards.
        /// </summary>
        private IEnumerator ReducedTransition()
        {
            var targetCamera = _gameManager.CurrentFocusCamera;
            Debug.Assert(targetCamera != null, "[StageTransition] Reduced transition target focus camera was not resolved.");

            FadeAlpha = 0f;
            yield return FadeTo(1f, reducedFadeDuration);

            ResetFocusCameras();
            if (targetCamera != null)
                yield return CutToVirtualCamera(targetCamera, 20);

            yield return FadeTo(0f, reducedFadeDuration);
        }

        private IEnumerator PushEnter(StagePortalConfig portal)
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            Debug.Assert(portal.IntroCam != null, "[StageTransition] StagePortalConfig.IntroCam not assigned.");
            Debug.Assert(portal.OutCam != null, "[StageTransition] StagePortalConfig.OutCam not assigned.");
            if (transitionVCam == null || portal.IntroCam == null || portal.OutCam == null) yield break;

            var introCam = portal.IntroCam;
            var outCam = portal.OutCam;
            var targetCamera = _gameManager.CurrentFocusCamera;
            Debug.Assert(targetCamera != null, "[StageTransition] Enter target focus camera was not resolved.");

            FadeAlpha = 0f;
            yield return TakeOverCurrentView();

            ResetFocusCameras();
            if (targetCamera != null)
                targetCamera.Priority = 20;

            yield return MoveTransitionCameraAlongPath(
                new[]
                {
                    introCam.transform.position,
                    introCam.transform.position + introCam.transform.forward * portal.pushDistance,
                },
                new[]
                {
                    introCam.transform.rotation,
                    introCam.transform.rotation,
                },
                approachDuration + pushDuration,
                portalTravelCurve);

            yield return FadeTo(1f, flashDuration);
            transitionVCam.transform.SetPositionAndRotation(
                outCam.transform.position + outCam.transform.forward * portal.pullDistance,
                outCam.transform.rotation);
            yield return null;

            yield return FadeTo(0f, flashDuration);
            if (targetCamera != null)
            {
                targetCamera.Priority = 20;
                yield return MoveTransitionCameraAlongPath(
                    new[]
                    {
                        outCam.transform.position,
                        targetCamera.transform.position,
                    },
                    new[]
                    {
                        outCam.transform.rotation,
                        targetCamera.transform.rotation,
                    },
                    pullDuration + approachDuration,
                    portalTravelCurve);
            }
            else
            {
                yield return MoveTransitionCameraTo(outCam.transform.position, outCam.transform.rotation, pullDuration, pullCurve);
            }

            yield return ReleaseTransitionCameraWithCut();
        }

        private IEnumerator PushExit(StagePortalConfig portal)
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            Debug.Assert(portal.IntroCam != null, "[StageTransition] StagePortalConfig.IntroCam not assigned.");
            Debug.Assert(portal.OutCam != null, "[StageTransition] StagePortalConfig.OutCam not assigned.");
            if (transitionVCam == null || portal.IntroCam == null || portal.OutCam == null) yield break;

            var introCam = portal.IntroCam;
            var outCam = portal.OutCam;
            var targetCamera = _gameManager.CurrentFocusCamera;
            Debug.Assert(targetCamera != null, "[StageTransition] Exit target focus camera was not resolved.");

            FadeAlpha = 0f;
            yield return TakeOverCurrentView();

            ResetFocusCameras();
            if (targetCamera != null)
                targetCamera.Priority = 20;

            yield return MoveTransitionCameraAlongPath(
                new[]
                {
                    outCam.transform.position,
                    outCam.transform.position + outCam.transform.forward * portal.pullDistance,
                },
                new[]
                {
                    outCam.transform.rotation,
                    outCam.transform.rotation,
                },
                approachDuration + pushDuration,
                portalTravelCurve);

            yield return FadeTo(1f, flashDuration);
            transitionVCam.transform.SetPositionAndRotation(
                introCam.transform.position + introCam.transform.forward * portal.pushDistance,
                introCam.transform.rotation);
            yield return null;

            yield return FadeTo(0f, flashDuration);
            if (targetCamera != null)
            {
                targetCamera.Priority = 20;
                yield return MoveTransitionCameraAlongPath(
                    new[]
                    {
                        introCam.transform.position,
                        targetCamera.transform.position,
                    },
                    new[]
                    {
                        introCam.transform.rotation,
                        targetCamera.transform.rotation,
                    },
                    pullDuration + approachDuration,
                    portalTravelCurve);
            }
            else
            {
                yield return MoveTransitionCameraTo(introCam.transform.position, introCam.transform.rotation, pullDuration, pullCurve);
            }

            yield return ReleaseTransitionCameraWithCut();
        }

        private IEnumerator TakeOverCurrentView()
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            if (transitionVCam == null) yield break;

            var sourceTransform = _brain != null && _brain.OutputCamera != null
                ? _brain.OutputCamera.transform
                : transitionVCam.transform;

            transitionVCam.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
            yield return CutToVirtualCamera(transitionVCam, transitionPriority);
        }

        private IEnumerator MoveTransitionCameraTo(Vector3 targetPosition, Quaternion targetRotation, float duration, AnimationCurve curve)
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            if (transitionVCam == null) yield break;

            Vector3 startPosition = transitionVCam.transform.position;
            Quaternion startRotation = transitionVCam.transform.rotation;

            if (duration <= 0f)
            {
                transitionVCam.transform.SetPositionAndRotation(targetPosition, targetRotation);
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = curve.Evaluate(Mathf.Clamp01(t / duration));
                transitionVCam.transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPosition, targetPosition, k),
                    Quaternion.SlerpUnclamped(startRotation, targetRotation, k));
                yield return null;
            }

            transitionVCam.transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        private IEnumerator MoveTransitionCameraAlongPath(Vector3[] points, Quaternion[] rotations, float duration, AnimationCurve curve)
        {
            Debug.Assert(transitionVCam != null, "[StageTransition] transitionVCam not assigned.");
            Debug.Assert(points.Length == rotations.Length, "[StageTransition] Path points/rotations length mismatch.");
            Debug.Assert(points.Length >= 1, "[StageTransition] Path must contain at least one waypoint.");
            if (transitionVCam == null || points.Length == 0 || points.Length != rotations.Length)
                yield break;

            if (points.Length == 1)
            {
                yield return MoveTransitionCameraTo(points[0], rotations[0], duration, curve);
                yield break;
            }

            var pathPoints = new Vector3[points.Length + 1];
            var pathRotations = new Quaternion[rotations.Length + 1];
            pathPoints[0] = transitionVCam.transform.position;
            pathRotations[0] = transitionVCam.transform.rotation;
            for (int i = 0; i < points.Length; i++)
            {
                pathPoints[i + 1] = points[i];
                pathRotations[i + 1] = rotations[i];
            }

            float[] segmentLengths = new float[pathPoints.Length - 1];
            float totalLength = 0f;
            for (int i = 0; i < segmentLengths.Length; i++)
            {
                float segmentLength = Vector3.Distance(pathPoints[i], pathPoints[i + 1]);
                segmentLengths[i] = segmentLength;
                totalLength += segmentLength;
            }

            if (duration <= 0f || totalLength <= 0.0001f)
            {
                transitionVCam.transform.SetPositionAndRotation(pathPoints[pathPoints.Length - 1], pathRotations[pathRotations.Length - 1]);
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float curveT = curve.Evaluate(Mathf.Clamp01(t / duration));
                float targetDistance = curveT * totalLength;
                EvaluatePath(pathPoints, pathRotations, segmentLengths, targetDistance, out var position, out var rotation);
                transitionVCam.transform.SetPositionAndRotation(position, rotation);
                yield return null;
            }

            transitionVCam.transform.SetPositionAndRotation(pathPoints[pathPoints.Length - 1], pathRotations[pathRotations.Length - 1]);
        }

        private static void EvaluatePath(
            Vector3[] points,
            Quaternion[] rotations,
            float[] segmentLengths,
            float targetDistance,
            out Vector3 position,
            out Quaternion rotation)
        {
            float traversed = 0f;
            for (int i = 0; i < segmentLengths.Length; i++)
            {
                float segmentLength = segmentLengths[i];
                if (targetDistance <= traversed + segmentLength || i == segmentLengths.Length - 1)
                {
                    float localT = segmentLength <= 0.0001f
                        ? 1f
                        : Mathf.Clamp01((targetDistance - traversed) / segmentLength);
                    position = Vector3.LerpUnclamped(points[i], points[i + 1], localT);
                    rotation = Quaternion.SlerpUnclamped(rotations[i], rotations[i + 1], localT);
                    return;
                }

                traversed += segmentLength;
            }

            position = points[points.Length - 1];
            rotation = rotations[rotations.Length - 1];
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
        /// 它和穿门的区别只在于没有镜头运动。低动画模式只是整体缩短，不取消：
        /// 这里没有任何东西在移动，黑场本身正是低动画要的那种过渡。
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

        private static float TurnDipHalfDuration => MotionSettings.ReduceMotion
            ? MotionSettings.CrossfadeDuration
            : TurnDipFullMotionHalfDuration;

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
