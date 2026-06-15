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
        [SerializeField] private int transitionPriority = 100;
        [SerializeField] private AnimationCurve approachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve pushCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve pullCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private SSNoirGameManager _gameManager = null!;
        private Cinemachine.CinemachineBrain? _brain;
        private string? _currentContextId;
        private StagePortalConfig? _activePortal;

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

            yield return MoveTransitionCameraTo(introCam.transform.position, introCam.transform.rotation, approachDuration, approachCurve);
            yield return MoveTransitionCameraTo(
                introCam.transform.position + introCam.transform.forward * portal.pushDistance,
                introCam.transform.rotation,
                pushDuration,
                pushCurve);

            yield return FadeTo(1f, flashDuration);
            transitionVCam.transform.SetPositionAndRotation(
                outCam.transform.position + outCam.transform.forward * portal.pullDistance,
                outCam.transform.rotation);
            yield return null;

            yield return FadeTo(0f, flashDuration);
            yield return MoveTransitionCameraTo(outCam.transform.position, outCam.transform.rotation, pullDuration, pullCurve);

            if (targetCamera != null)
            {
                targetCamera.Priority = 20;
                yield return MoveTransitionCameraTo(targetCamera.transform.position, targetCamera.transform.rotation, approachDuration, approachCurve);
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

            yield return MoveTransitionCameraTo(outCam.transform.position, outCam.transform.rotation, approachDuration, approachCurve);
            yield return MoveTransitionCameraTo(
                outCam.transform.position + outCam.transform.forward * portal.pullDistance,
                outCam.transform.rotation,
                pushDuration,
                pushCurve);

            yield return FadeTo(1f, flashDuration);
            transitionVCam.transform.SetPositionAndRotation(
                introCam.transform.position + introCam.transform.forward * portal.pushDistance,
                introCam.transform.rotation);
            yield return null;

            yield return FadeTo(0f, flashDuration);
            yield return MoveTransitionCameraTo(introCam.transform.position, introCam.transform.rotation, pullDuration, pullCurve);

            if (targetCamera != null)
            {
                targetCamera.Priority = 20;
                yield return MoveTransitionCameraTo(targetCamera.transform.position, targetCamera.transform.rotation, approachDuration, approachCurve);
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
            var anchor = _gameManager.SceneDirectory?.GetAnchor(contextId);
            return anchor?.GetComponent<StagePortalConfig>();
        }
    }
}
