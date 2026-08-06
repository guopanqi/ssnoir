#nullable enable
using UnityEngine;
using System.Linq;

namespace SSNoir
{
    public class SSNoirCameraManager
    {
        private const float NavigationDuration = 0.42f;

        // How far the pointer must travel before a press counts as taking the camera.
        // Below it the press is a click on the world, and a click must never stop a
        // transition — clicking a card while the camera is still flying is normal.
        private const float DragThreshold = 6f;

        private readonly SSNoirGameManager _gameManager;
        private readonly float _panSpeed;

        // Mouse drag states. A press begins as a candidate (_isPressingWorld); it only
        // becomes a grab (_isDraggingCam) once the pointer clears DragThreshold.
        private bool _isPressingWorld = false;
        private bool _isDraggingCam = false;
        private Vector3 _dragStartMousePos;
        private Vector3 _dragStartCamPos;

        // Edge-beacon navigation moves the current camera without changing gameplay focus.
        // A real world drag always cancels this interpolation and takes control immediately.
        private bool _isNavigating;
        private Cinemachine.CinemachineVirtualCamera? _navigationCamera;
        private CameraDragMode _navigationMode;
        private float _navigationStartedAt;
        private Vector3 _navigationStartPosition;
        private Vector3 _navigationTargetPosition;
        private Vector3 _navigationOrbitPivot;
        private float _navigationStartYaw;
        private float _navigationTargetYaw;
        private float _navigationOrbitPitch;
        private float _navigationOrbitRadius;

        // Focus travel: every focus change moves the camera along one curve, whichever
        // drag modes the two ends have. Both ends are described the same way — an
        // interest point plus the camera's polar position around it — so the trip is
        // an arc around a moving centre. Orbit ends contribute their pivot, pan ends
        // the ground point they are looking at. Going in and coming back out are then
        // the same curve read in opposite directions, and a pan-to-pan trip degenerates
        // to the straight slide it always was.
        private bool _isFocusArcActive;
        private Cinemachine.CinemachineVirtualCamera? _focusArcCamera;
        private Cinemachine.CinemachineVirtualCamera? _lastFocusCamera;
        private Cinemachine.CinemachineBrain? _focusArcBrain;
        private Cinemachine.CinemachineBlendDefinition _focusArcSavedBlend;
        private float _focusArcStartedAt;
        private float _focusArcDuration;
        private Vector3 _focusArcStartInterest;
        private float _focusArcStartYaw;
        private float _focusArcStartPitch;
        private float _focusArcStartRadius;
        private Quaternion _focusArcStartAim;
        private Vector3 _focusArcTargetInterest;
        private float _focusArcTargetYaw;
        private float _focusArcTargetPitch;
        private float _focusArcTargetRadius;
        private Quaternion _focusArcTargetAim;
        private Quaternion _focusArcTargetRotation;
        private float _focusArcStartNearClip;
        private float _focusArcTargetNearClip;
        private float _focusArcStartFarClip;
        private float _focusArcTargetFarClip;

        // Player steering during focus travel does not cancel the authored move. Input
        // lives in the destination camera's interaction space and offsets the curve all
        // the way to its endpoint: pan destinations translate the moving shot on XZ;
        // orbit destinations add yaw/pitch around the curve's moving interest point.
        private CameraDragMode _focusArcInputMode;
        private SSNoirVirtualCameraConfig? _focusArcInputConfig;
        private Vector3 _focusArcPanOffset;
        private float _focusArcYawOffset;
        private float _focusArcPitchOffset;
        private bool _isDraggingFocusArc;
        private Vector3 _focusArcDragStartPanOffset;
        private float _focusArcDragStartYawOffset;
        private float _focusArcDragStartPitchOffset;
        private Vector3 _focusArcPanRight;
        private Vector3 _focusArcPanForward;

        // Reduce motion: the trip is replaced by a cut under a dissolve. The brain must
        // not blend on top of that — a blend and a dissolve running together reads as
        // neither. The blend is held cut for as long as the dissolve lasts, which is
        // also long enough for the cut to actually land: a focus change raised from
        // OnGUI is one frame ahead of the brain, so releasing on the next tick would
        // give the blend back before it ever cut.
        private readonly ViewCrossfade _crossfade;
        private Cinemachine.CinemachineBrain? _cutHoldBrain;
        private Cinemachine.CinemachineBlendDefinition _cutHoldSavedBlend;
        private int _cutHoldFrame;

        // Destination parked on the outgoing view for one frame while the freeze is taken.
        private Cinemachine.CinemachineVirtualCamera? _reducedParkedCamera;
        private Vector3 _reducedTargetPosition;
        private Quaternion _reducedTargetRotation;
        private float _reducedTargetNearClip;
        private float _reducedTargetFarClip;
        private int _reducedParkedFrame;

        public ViewCrossfade Crossfade => _crossfade;

        public SSNoirCameraManager(SSNoirGameManager gameManager, float panSpeed)
        {
            _gameManager = gameManager;
            _panSpeed = panSpeed;
            _crossfade = new ViewCrossfade(gameManager);
        }

        public void Update()
        {
            var activeCamera = GetActiveCamera();
            if (activeCamera == null) return;

            if (_isNavigating && _navigationCamera != activeCamera)
                _isNavigating = false;

            // Handle Camera Drag Panning / Orbiting.
            // Only *start* a drag when the press does not begin over the UI — a
            // press on a die/card/panel belongs to IMGUI, not the camera. Once a
            // world-space drag is underway it keeps going even over the UI.
            if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && !_gameManager.PointerOverUI)
            {
                // A press is not yet a grab: most presses are clicks on the world. The
                // camera keeps doing what it was doing until the pointer actually travels.
                _isPressingWorld = true;
                _isDraggingCam = false;
                _dragStartMousePos = Input.mousePosition;
            }

            if (_isPressingWorld)
            {
                if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                {
                    if (!_isDraggingCam
                        && (Input.mousePosition - _dragStartMousePos).magnitude >= DragThreshold)
                    {
                        if (_isFocusArcActive)
                            BeginFocusArcDrag(activeCamera);
                        else
                            BeginDrag(activeCamera);
                    }

                    if (_isDraggingCam)
                    {
                        if (_isDraggingFocusArc)
                        {
                            UpdateFocusArcDrag(activeCamera);
                        }
                        else
                        {
                            // The origin was re-taken where the grab happened, not where the
                            // press landed, so that taking over a moving camera does not jump.
                            Vector3 mouseDelta = Input.mousePosition - _dragStartMousePos;

                            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
                            if (config != null && config.dragMode == CameraDragMode.Orbit)
                            {
                                var pivot = GetOrbitPivot(activeCamera);
                                if (pivot != null)
                                {
                                    // Apply orbit rotation using cumulative drag mouseDelta
                                    config.ApplyOrbitFromDrag(pivot.position, mouseDelta.x, mouseDelta.y);
                                }
                            }
                            else
                            {
                                // Height-locked RTS/MOBA Pan (moves parallel to XZ ground plane)
                                Vector3 right = activeCamera.transform.right;
                                right.y = 0f;
                                right.Normalize();

                                Vector3 forward = activeCamera.transform.forward;
                                forward.y = 0f;
                                forward.Normalize();

                                Vector3 panTranslation = -mouseDelta.x * right * _panSpeed - mouseDelta.y * forward * _panSpeed;
                                activeCamera.transform.position = _dragStartCamPos + panTranslation;
                            }
                        }
                    }
                }
                else
                {
                    _isPressingWorld = false;
                    _isDraggingCam = false;
                    _isDraggingFocusArc = false;
                }
            }

            if (!_isDraggingCam && _isNavigating)
                UpdateNavigation(activeCamera);
        }

        /// <summary>
        /// The pointer has travelled far enough to mean it: the player takes the camera
        /// from the edge-beacon interpolation. The drag origin is captured here rather
        /// than at the press, because the camera may have moved in between — measuring
        /// from the press would jump it by however far it went.
        /// </summary>
        private void BeginDrag(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            _isNavigating = false;

            _isDraggingCam = true;
            _isDraggingFocusArc = false;
            _dragStartMousePos = Input.mousePosition;
            _dragStartCamPos = activeCamera.transform.position;

            // Save starting极坐标 (polar coordinates) state on virtual camera config if in Orbit mode
            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config != null && config.dragMode == CameraDragMode.Orbit)
            {
                var pivot = GetOrbitPivot(activeCamera);
                if (pivot != null)
                {
                    config.SaveDragStartState(pivot.position);
                }
            }
        }

        private void BeginFocusArcDrag(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            if (!_isFocusArcActive || _focusArcCamera == null)
                return;

            if (!ReferenceEquals(activeCamera, _focusArcCamera))
            {
                string message =
                    $"[SSNoir] Focus travel camera '{_focusArcCamera.name}' does not match " +
                    $"the active focus camera '{activeCamera.name}' while beginning a drag.";
                Debug.LogError(message);
                UnityEngine.Assertions.Assert.IsTrue(false, message);
                throw new System.InvalidOperationException(message);
            }

            _isNavigating = false;
            _isDraggingCam = true;
            _isDraggingFocusArc = true;
            _dragStartMousePos = Input.mousePosition;
            _focusArcDragStartPanOffset = _focusArcPanOffset;
            _focusArcDragStartYawOffset = _focusArcYawOffset;
            _focusArcDragStartPitchOffset = _focusArcPitchOffset;

            _focusArcPanRight = activeCamera.transform.right;
            _focusArcPanRight.y = 0f;
            _focusArcPanRight.Normalize();

            _focusArcPanForward = activeCamera.transform.forward;
            _focusArcPanForward.y = 0f;
            _focusArcPanForward.Normalize();
        }

        private void UpdateFocusArcDrag(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            if (!_isFocusArcActive || !ReferenceEquals(activeCamera, _focusArcCamera))
                return;

            Vector3 mouseDelta = Input.mousePosition - _dragStartMousePos;
            if (_focusArcInputMode == CameraDragMode.Orbit)
            {
                if (_focusArcInputConfig == null)
                {
                    string message =
                        $"[SSNoir] Orbit focus travel camera '{activeCamera.name}' has no camera config.";
                    Debug.LogError(message);
                    UnityEngine.Assertions.Assert.IsTrue(false, message);
                    throw new System.InvalidOperationException(message);
                }

                _focusArcYawOffset =
                    _focusArcDragStartYawOffset + mouseDelta.x * _focusArcInputConfig.orbitSpeedX;
                _focusArcPitchOffset = Mathf.Clamp(
                    _focusArcDragStartPitchOffset - mouseDelta.y * _focusArcInputConfig.orbitSpeedY,
                    Mathf.Min(0f, _focusArcInputConfig.minPitch - _focusArcTargetPitch),
                    Mathf.Max(0f, _focusArcInputConfig.maxPitch - _focusArcTargetPitch));
            }
            else
            {
                _focusArcPanOffset = _focusArcDragStartPanOffset
                    - mouseDelta.x * _focusArcPanRight * _panSpeed
                    - mouseDelta.y * _focusArcPanForward * _panSpeed;
            }

            ApplyFocusArcPose(FocusArcEasedProgress());
        }

        public void NavigateToNode(string nodeName)
        {
            var anchor = _gameManager.SceneDirectory?.GetAnchor(nodeName);
            if (anchor == null)
            {
                string errorMsg = $"[SSNoir] Cannot navigate camera to node '{nodeName}': no scene anchor was found.";
                Debug.LogError(errorMsg);
                UnityEngine.Assertions.Assert.IsTrue(false, errorMsg);
                throw new System.InvalidOperationException(errorMsg);
            }

            NavigateToWorldPoint(anchor.transform.position);
        }

        /// <summary>
        /// Hands a focus change over to a hand-driven curve instead of Cinemachine's
        /// straight blend — for every focus camera, orbit or pan, so a trip and its
        /// return are the same curve rather than two different ones.
        ///
        /// Without player input, an orbit building lands on its authored pose (the
        /// framing the scene was designed with), while a pan camera lands where it
        /// currently stands. A world drag may steer that destination while the original
        /// travel continues: orbit input offsets yaw/pitch and pan input offsets the
        /// ground-plane destination. The camera therefore still completes a composed
        /// shot instead of being abandoned at an arbitrary point along the way.
        ///
        /// Returns true when the travel took over; the caller may then raise priority as
        /// usual — the brain is cut for the duration, so this camera is the shot.
        /// </summary>
        public bool BeginFocusTravel(Cinemachine.CinemachineVirtualCamera focusCamera)
        {
            // A repeated refresh of the focus that is already travelling must be a
            // no-op. Finishing first would snap the running arc to t=1 before the
            // "already active" check below gets a chance to reject the duplicate.
            if (_isFocusArcActive && ReferenceEquals(_focusArcCamera, focusCamera))
                return true;

            // A genuinely different focus change lands the previous travel in flight —
            // and gives the brain its real blend back before this one reads it as the
            // travel time.
            FinishFocusTravel();

            var previousFocus = _lastFocusCamera;
            _lastFocusCamera = focusCamera;

            var renderedCamera = Camera.main;
            var brain = renderedCamera != null ? renderedCamera.GetComponent<Cinemachine.CinemachineBrain>() : null;
            if (renderedCamera == null || brain == null)
                return false;

            // Nothing is live yet (first focus of a scene): Cinemachine snaps to the
            // shot, and there is no view to travel from. No opening swoop.
            if (brain.ActiveVirtualCamera == null)
                return false;

            // Already the live shot. Leave the view where it is — a manual orbit or pan
            // must not be yanked back by a focus change that does not move the camera.
            if (ReferenceEquals(brain.ActiveVirtualCamera, focusCamera) && !brain.IsBlending)
                return false;

            // Debug hard-cut is a zero-duration test path, not an accessibility effect:
            // no arc and no dissolve, just place the destination and let the brain cut.
            if (MotionSettings.DebugInstantCameraCuts)
                return BeginInstantFocusChange(focusCamera, brain);

            // Reduce motion takes the same fork every time, whatever the two ends are:
            // no road at all, just a dissolve over a cut.
            if (MotionSettings.ReduceMotion)
                return BeginReducedFocusChange(focusCamera, brain, renderedCamera);

            float duration = brain.m_DefaultBlend.m_Time;
            if (duration <= 0.01f)
                return false;

            var config = focusCamera.GetComponent<SSNoirVirtualCameraConfig>();
            bool destinationOrbits = config != null && config.dragMode == CameraDragMode.Orbit;
            Vector3 targetPosition = destinationOrbits ? config!.AuthoredPosition : focusCamera.transform.position;
            Quaternion targetRotation = destinationOrbits ? config!.AuthoredRotation : focusCamera.transform.rotation;
            Vector3 startPosition = renderedCamera.transform.position;
            Quaternion startRotation = renderedCamera.transform.rotation;
            float startNearClip = renderedCamera.nearClipPlane;
            float targetNearClip = focusCamera.m_Lens.NearClipPlane;
            float startFarClip = renderedCamera.farClipPlane;
            float targetFarClip = focusCamera.m_Lens.FarClipPlane;

            // The shot being left only speaks for the view when it is the one actually
            // on screen. After a stage transition drove its own cameras, the last focus
            // is stale and the rendered view has to speak for itself.
            var sourceCamera = ReferenceEquals(brain.ActiveVirtualCamera, previousFocus) ? previousFocus : null;

            if (!TryResolveInterestPoints(
                    focusCamera, targetPosition, targetRotation,
                    sourceCamera, startPosition, startRotation,
                    out Vector3 startInterest, out Vector3 targetInterest))
                return false;

            if ((targetPosition - targetInterest).sqrMagnitude < 0.0001f
                || (startPosition - startInterest).sqrMagnitude < 0.0001f)
                return false;

            _isNavigating = false;
            _isDraggingCam = false;

            ToPolar(startPosition - startInterest, out _focusArcStartYaw, out _focusArcStartPitch, out _focusArcStartRadius);
            ToPolar(targetPosition - targetInterest, out _focusArcTargetYaw, out _focusArcTargetPitch, out _focusArcTargetRadius);
            _focusArcStartAim = AimOffset(startPosition, startRotation, startInterest);
            _focusArcTargetAim = AimOffset(targetPosition, targetRotation, targetInterest);

            _focusArcCamera = focusCamera;
            _focusArcBrain = brain;
            _focusArcStartInterest = startInterest;
            _focusArcTargetInterest = targetInterest;
            _focusArcTargetRotation = targetRotation;
            _focusArcStartNearClip = startNearClip;
            _focusArcTargetNearClip = targetNearClip;
            _focusArcStartFarClip = startFarClip;
            _focusArcTargetFarClip = targetFarClip;
            _focusArcInputMode = destinationOrbits ? CameraDragMode.Orbit : CameraDragMode.Pan;
            _focusArcInputConfig = config;
            _focusArcPanOffset = Vector3.zero;
            _focusArcYawOffset = 0f;
            _focusArcPitchOffset = 0f;
            _isDraggingFocusArc = false;
            _focusArcStartedAt = Time.unscaledTime;
            _focusArcDuration = duration;

            // The arc *is* the transition, so the brain must not blend on top of it.
            // Cutting is invisible: the camera starts exactly where the rendered view is.
            _focusArcSavedBlend = brain.m_DefaultBlend;
            brain.m_DefaultBlend = new Cinemachine.CinemachineBlendDefinition(
                Cinemachine.CinemachineBlendDefinition.Style.Cut, 0f);
            focusCamera.transform.SetPositionAndRotation(startPosition, startRotation);
            SetClipPlanes(focusCamera, startNearClip, startFarClip);

            _isFocusArcActive = true;
            return true;
        }

        /// <summary>
        /// Debug-only zero-duration focus change. Orbit shots still return to their authored
        /// framing, but there is no travel and no dissolve around the Cinemachine cut.
        /// </summary>
        private bool BeginInstantFocusChange(
            Cinemachine.CinemachineVirtualCamera focusCamera,
            Cinemachine.CinemachineBrain brain)
        {
            _isNavigating = false;
            _isDraggingCam = false;
            ReleaseReducedPark();

            var config = focusCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config != null && config.dragMode == CameraDragMode.Orbit)
            {
                focusCamera.transform.SetPositionAndRotation(
                    config.AuthoredPosition, config.AuthoredRotation);
            }

            _cutHoldSavedBlend = brain.m_DefaultBlend;
            brain.m_DefaultBlend = new Cinemachine.CinemachineBlendDefinition(
                Cinemachine.CinemachineBlendDefinition.Style.Cut, 0f);
            _cutHoldBrain = brain;
            _cutHoldFrame = Time.frameCount;
            return true;
        }

        /// <summary>
        /// Hands the focus change to a cut hidden under a dissolve — the reduce-motion
        /// form of <see cref="BeginFocusTravel"/>. Nothing travels, so nothing sweeps
        /// past the player; the old shot simply dissolves off the new one.
        ///
        /// The whole thing turns on getting the freeze *before* the cut, and the caller
        /// runs in Update — the brain reaches LateUpdate of this same frame and would
        /// have already snapped by the time the frame is captured, leaving us dissolving
        /// the new shot onto itself. So the destination is parked on the outgoing view
        /// for exactly one frame: the brain cuts to it and nothing changes on screen,
        /// the freeze takes its copy at the end of that frame, and only then is the
        /// camera released to its real pose, underneath the frozen frame.
        /// </summary>
        private bool BeginReducedFocusChange(
            Cinemachine.CinemachineVirtualCamera focusCamera,
            Cinemachine.CinemachineBrain brain,
            Camera renderedCamera)
        {
            _isNavigating = false;
            _isDraggingCam = false;

            // A focus change landing on top of a parked one puts the previous destination
            // back where it belongs first; otherwise it stays stranded on a stale view.
            ReleaseReducedPark();

            var config = focusCamera.GetComponent<SSNoirVirtualCameraConfig>();
            bool destinationOrbits = config != null && config.dragMode == CameraDragMode.Orbit;

            // Read the destination before parking overwrites it.
            _reducedParkedCamera = focusCamera;
            _reducedTargetPosition = destinationOrbits ? config!.AuthoredPosition : focusCamera.transform.position;
            _reducedTargetRotation = destinationOrbits ? config!.AuthoredRotation : focusCamera.transform.rotation;
            _reducedTargetNearClip = focusCamera.m_Lens.NearClipPlane;
            _reducedTargetFarClip = focusCamera.m_Lens.FarClipPlane;
            _reducedParkedFrame = Time.frameCount;

            focusCamera.transform.SetPositionAndRotation(
                renderedCamera.transform.position, renderedCamera.transform.rotation);
            SetClipPlanes(
                focusCamera, renderedCamera.nearClipPlane, renderedCamera.farClipPlane);

            // 抓帧相机照抄此刻的 renderedCamera——brain 要到 LateUpdate 才动它，所以它
            // 现在还站在要留下的那一镜上。
            _crossfade.Begin(MotionSettings.CrossfadeDuration, renderedCamera);

            _cutHoldSavedBlend = brain.m_DefaultBlend;
            brain.m_DefaultBlend = new Cinemachine.CinemachineBlendDefinition(
                Cinemachine.CinemachineBlendDefinition.Style.Cut, 0f);
            _cutHoldBrain = brain;
            _cutHoldFrame = Time.frameCount;
            return true;
        }

        /// <summary>
        /// Puts a parked destination back on its real shot. Safe to call when nothing is
        /// parked, and safe to call when no freeze ever arrived — the camera must never
        /// be left standing on a view it does not own.
        /// </summary>
        private void ReleaseReducedPark()
        {
            if (_reducedParkedCamera == null)
                return;

            _reducedParkedCamera.transform.SetPositionAndRotation(
                _reducedTargetPosition, _reducedTargetRotation);
            SetClipPlanes(
                _reducedParkedCamera, _reducedTargetNearClip, _reducedTargetFarClip);
            _reducedParkedCamera = null;
        }

        /// <summary>
        /// Advances a running focus travel — the arc, or the reduce-motion dissolve that
        /// stands in for it. Driven every frame, including while gameplay input is
        /// locked: a focus change during a scripted beat still has to land.
        /// </summary>
        public void TickFocusTravel()
        {
            _crossfade.Tick();

            // Release the parked destination the moment the freeze exists — that frame is
            // now holding the old shot on screen, so the camera underneath is free to be
            // where it really belongs. The frame guard is the safety net for the case
            // where no freeze ever arrives: the park must not outlive its one frame.
            if (_reducedParkedCamera != null
                && (_crossfade.IsFading || Time.frameCount > _reducedParkedFrame + 1))
                ReleaseReducedPark();

            // The brain gets its blend back once the dissolve is over, not before: until
            // then any blend it ran would be a second transition underneath the first.
            // The frame guard is what makes the cut land at all — the brain only reads
            // the blend in LateUpdate, so a hold taken during OnGUI has to outlive the
            // Update that follows it, even in the case where no dissolve ever started.
            if (_cutHoldBrain != null && !_crossfade.IsFading && Time.frameCount > _cutHoldFrame + 1)
                ReleaseCutHold();

            if (!_isFocusArcActive || _focusArcCamera == null)
                return;

            float t = Mathf.Clamp01((Time.unscaledTime - _focusArcStartedAt) / _focusArcDuration);
            if (t >= 1f)
            {
                FinishFocusTravel(continueHeldDrag: true);
                return;
            }

            ApplyFocusArcPose(t * t * (3f - 2f * t));
        }

        private float FocusArcEasedProgress()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _focusArcStartedAt) / _focusArcDuration);
            return t * t * (3f - 2f * t);
        }

        private void ApplyFocusArcPose(float eased)
        {
            if (_focusArcCamera == null)
                return;

            // The centre of the arc slides from what the old shot was looking at to what
            // the new one looks at; the camera holds a polar position around that moving
            // centre. Between two orbit buildings the centre walks from one pivot to the
            // other; between two pan shots both centres are ground points and the whole
            // thing collapses back into a straight slide.
            Vector3 interest = Vector3.Lerp(_focusArcStartInterest, _focusArcTargetInterest, eased);
            float yaw = Mathf.LerpAngle(_focusArcStartYaw, _focusArcTargetYaw, eased);
            float pitch = Mathf.Lerp(_focusArcStartPitch, _focusArcTargetPitch, eased);
            float radius = Mathf.Lerp(_focusArcStartRadius, _focusArcTargetRadius, eased);

            if (_focusArcInputMode == CameraDragMode.Orbit)
            {
                yaw += _focusArcYawOffset;
                pitch += _focusArcPitchOffset;
            }
            else
            {
                // Moving both the camera and its interest point preserves the authored
                // framing while the whole travelling shot slides across the ground plane.
                interest += _focusArcPanOffset;
            }

            Vector3 position = interest + FromPolar(yaw, pitch, radius);

            // Rotation is rebuilt from "look at the interest point" plus the framing
            // offset each shot holds against that look direction. Interpolating the
            // offset rather than the world rotation is what keeps the subject parked in
            // the frame for the whole sweep instead of drifting out and swinging back in.
            Quaternion aim = Quaternion.Slerp(_focusArcStartAim, _focusArcTargetAim, eased);
            Vector3 toInterest = interest - position;
            Quaternion rotation = toInterest.sqrMagnitude < 0.0001f
                ? Quaternion.Slerp(_focusArcCamera.transform.rotation, _focusArcTargetRotation, eased)
                : Quaternion.LookRotation(toInterest, Vector3.up) * aim;

            _focusArcCamera.transform.SetPositionAndRotation(position, rotation);
            SetClipPlanes(
                _focusArcCamera,
                Mathf.Lerp(_focusArcStartNearClip, _focusArcTargetNearClip, eased),
                Mathf.Lerp(_focusArcStartFarClip, _focusArcTargetFarClip, eased));
        }

        /// <summary>
        /// Ends a running travel at its destination and gives the brain its blend back.
        /// A travel always ends here — on arrival, when a new focus supersedes it, or
        /// when another system needs the brain (a stage transition drives its own cut).
        /// It is never dropped part-way: the shot it is delivering is the point of it,
        /// and nothing may leave a camera stranded mid-flight. The reduce-motion form
        /// ends here too — its dissolve is dropped and the brain handed back, since the
        /// destination shot is already live underneath the frozen frame.
        /// </summary>
        public void FinishFocusTravel()
        {
            FinishFocusTravel(continueHeldDrag: false);
        }

        private void FinishFocusTravel(bool continueHeldDrag)
        {
            _crossfade.Finish();
            ReleaseReducedPark();
            ReleaseCutHold();

            if (!_isFocusArcActive)
                return;

            var completedCamera = _focusArcCamera;
            if (completedCamera != null)
                ApplyFocusArcPose(1f);

            if (_focusArcBrain != null)
                _focusArcBrain.m_DefaultBlend = _focusArcSavedBlend;

            bool continueDragging = continueHeldDrag
                && _isDraggingFocusArc
                && (Input.GetMouseButton(0) || Input.GetMouseButton(1));

            _isFocusArcActive = false;
            _focusArcCamera = null;
            _focusArcBrain = null;
            _focusArcInputConfig = null;
            _isDraggingFocusArc = false;

            if (continueDragging && completedCamera != null)
                BeginDrag(completedCamera);
            else if (_isDraggingCam)
                _isDraggingCam = false;
        }

        private void ReleaseCutHold()
        {
            if (_cutHoldBrain == null)
                return;

            _cutHoldBrain.m_DefaultBlend = _cutHoldSavedBlend;
            _cutHoldBrain = null;
        }

        private static void SetClipPlanes(
            Cinemachine.CinemachineVirtualCamera camera,
            float nearClipPlane,
            float farClipPlane)
        {
            if (nearClipPlane <= 0f || farClipPlane <= nearClipPlane)
            {
                string message =
                    $"[SSNoir] Camera '{camera.name}' has an invalid clip range " +
                    $"{nearClipPlane:F3}..{farClipPlane:F3}.";
                Debug.LogError(message);
                UnityEngine.Assertions.Assert.IsTrue(false, message);
                throw new System.InvalidOperationException(message);
            }

            var lens = camera.m_Lens;
            lens.NearClipPlane = nearClipPlane;
            lens.FarClipPlane = farClipPlane;
            camera.m_Lens = lens;
        }

        /// <summary>
        /// Finds what each end of the trip is looking at. An orbit shot says so itself —
        /// its pivot. A pan shot is looking at the ground, so its interest point is where
        /// its centre ray lands on the plane the trip is being framed against (the orbit
        /// end's pivot height, or the world floor when neither end orbits). A ray that
        /// never reaches that plane borrows the other end's point, which turns the trip
        /// into a plain single-centre arc.
        /// </summary>
        private bool TryResolveInterestPoints(
            Cinemachine.CinemachineVirtualCamera destination,
            Vector3 destinationPosition,
            Quaternion destinationRotation,
            Cinemachine.CinemachineVirtualCamera? source,
            Vector3 sourcePosition,
            Quaternion sourceRotation,
            out Vector3 sourceInterest,
            out Vector3 destinationInterest)
        {
            sourceInterest = Vector3.zero;
            bool destinationOrbits = TryGetOrbitPivotPoint(destination, out destinationInterest);
            bool sourceOrbits = source != null && TryGetOrbitPivotPoint(source, out sourceInterest);

            float planeHeight = destinationOrbits
                ? destinationInterest.y
                : (sourceOrbits ? sourceInterest.y : 0f);

            if (!destinationOrbits
                && !TryGroundInterest(destinationPosition, destinationRotation * Vector3.forward, planeHeight, out destinationInterest))
            {
                if (!sourceOrbits)
                    return false;
                destinationInterest = sourceInterest;
            }

            if (!sourceOrbits
                && !TryGroundInterest(sourcePosition, sourceRotation * Vector3.forward, planeHeight, out sourceInterest))
            {
                sourceInterest = destinationInterest;
            }

            return true;
        }

        private bool TryGetOrbitPivotPoint(Cinemachine.CinemachineVirtualCamera camera, out Vector3 point)
        {
            point = Vector3.zero;
            var config = camera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || config.dragMode != CameraDragMode.Orbit)
                return false;

            var pivot = GetOrbitPivot(camera);
            if (pivot == null)
                return false;

            point = pivot.position;
            return true;
        }

        private static bool TryGroundInterest(Vector3 position, Vector3 forward, float planeHeight, out Vector3 point)
        {
            const float MaxInterestDistance = 500f;

            point = Vector3.zero;
            var plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
            if (!plane.Raycast(new Ray(position, forward), out float distance) || distance > MaxInterestDistance)
                return false;

            point = position + forward.normalized * distance;
            return true;
        }

        private static void ToPolar(Vector3 offset, out float yaw, out float pitch, out float radius)
        {
            radius = offset.magnitude;
            Vector3 horizontal = new Vector3(offset.x, 0f, offset.z);
            yaw = horizontal.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Atan2(offset.y, horizontal.magnitude) * Mathf.Rad2Deg;
        }

        private static Vector3 FromPolar(float yaw, float pitch, float radius)
        {
            float yawRadians = yaw * Mathf.Deg2Rad;
            float pitchRadians = pitch * Mathf.Deg2Rad;
            float horizontalRadius = radius * Mathf.Cos(pitchRadians);
            return new Vector3(
                horizontalRadius * Mathf.Sin(yawRadians),
                radius * Mathf.Sin(pitchRadians),
                horizontalRadius * Mathf.Cos(yawRadians));
        }

        private static Quaternion AimOffset(Vector3 position, Quaternion rotation, Vector3 pivot)
        {
            Vector3 toPivot = pivot - position;
            if (toPivot.sqrMagnitude < 0.0001f)
                return Quaternion.identity;

            return Quaternion.Inverse(Quaternion.LookRotation(toPivot, Vector3.up)) * rotation;
        }

        private void NavigateToWorldPoint(Vector3 targetWorldPosition)
        {
            var activeCamera = GetActiveCamera();
            if (activeCamera == null)
                return;

            _isDraggingCam = false;

            // A beacon click during a transition waits for the shot to land, then walks
            // on from there — the two must not drive the same camera at once.
            FinishFocusTravel();

            _navigationCamera = activeCamera;
            _navigationStartedAt = Time.unscaledTime;
            _navigationStartPosition = activeCamera.transform.position;

            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            _navigationMode = config != null ? config.dragMode : CameraDragMode.Pan;
            if (_navigationMode == CameraDragMode.Orbit)
            {
                var pivot = GetOrbitPivot(activeCamera);
                if (pivot == null)
                    return;

                Vector3 cameraOffset = activeCamera.transform.position - pivot.position;
                Vector3 targetOffset = targetWorldPosition - pivot.position;
                targetOffset.y = 0f;
                if (cameraOffset.sqrMagnitude < 0.0001f || targetOffset.sqrMagnitude < 0.0001f)
                    return;

                _navigationOrbitPivot = pivot.position;
                _navigationOrbitRadius = cameraOffset.magnitude;
                _navigationOrbitPitch = Mathf.Asin(cameraOffset.y / _navigationOrbitRadius) * Mathf.Rad2Deg;
                _navigationStartYaw = Mathf.Atan2(cameraOffset.x, cameraOffset.z) * Mathf.Rad2Deg;
                _navigationTargetYaw = Mathf.Atan2(targetOffset.x, targetOffset.z) * Mathf.Rad2Deg;
            }
            else
            {
                // Match manual pan semantics: preserve camera height and orientation, and translate
                // on XZ until the target lies under the centre ray at the target's ground height.
                var targetPlane = new Plane(Vector3.up, targetWorldPosition);
                var centreRay = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
                if (!targetPlane.Raycast(centreRay, out float distance))
                {
                    string errorMsg = $"[SSNoir] Cannot pan camera '{activeCamera.name}' to '{targetWorldPosition}': its centre ray does not intersect the target plane.";
                    Debug.LogError(errorMsg);
                    UnityEngine.Assertions.Assert.IsTrue(false, errorMsg);
                    throw new System.InvalidOperationException(errorMsg);
                }

                Vector3 translation = targetWorldPosition - centreRay.GetPoint(distance);
                translation.y = 0f;
                _navigationTargetPosition = _navigationStartPosition + translation;
            }

            _isNavigating = true;

            if (MotionSettings.DebugInstantCameraCuts)
            {
                ApplyNavigationPose(activeCamera, 1f);
                _isNavigating = false;
                return;
            }

            // Reduce motion draws the line at rotation, not at movement: a slide across
            // the city keeps the player oriented and costs nothing, so it stays (just
            // shorter). Swinging around a pivot is the part that makes people ill — that
            // one is cut and dissolved instead of turned.
            if (MotionSettings.ReduceMotion && _navigationMode == CameraDragMode.Orbit)
            {
                var renderedCamera = Camera.main;
                if (renderedCamera != null)
                {
                    // Freeze first: the camera is still standing where the player left it.
                    _crossfade.Begin(MotionSettings.CrossfadeDuration, renderedCamera);

                    // Then park it back there for the one frame the freeze needs. Landing
                    // on the destination in this same frame would put the new shot on
                    // screen before the frozen frame exists to cover it — one frame of
                    // the swing's endpoint, which is exactly the flicker being avoided.
                    Vector3 parkPosition = activeCamera.transform.position;
                    Quaternion parkRotation = activeCamera.transform.rotation;

                    ApplyNavigationPose(activeCamera, 1f);
                    _reducedParkedCamera = activeCamera;
                    _reducedTargetPosition = activeCamera.transform.position;
                    _reducedTargetRotation = activeCamera.transform.rotation;
                    _reducedTargetNearClip = activeCamera.m_Lens.NearClipPlane;
                    _reducedTargetFarClip = activeCamera.m_Lens.FarClipPlane;
                    _reducedParkedFrame = Time.frameCount;

                    activeCamera.transform.SetPositionAndRotation(parkPosition, parkRotation);
                }

                _isNavigating = false;
            }
        }

        private void UpdateNavigation(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            float duration = MotionSettings.ReduceMotion
                ? MotionSettings.ReducedNavigationDuration
                : NavigationDuration;
            float t = Mathf.Clamp01((Time.unscaledTime - _navigationStartedAt) / duration);
            ApplyNavigationPose(activeCamera, t * t * (3f - 2f * t));

            if (t >= 1f)
                _isNavigating = false;
        }

        /// <summary>
        /// Places the camera at a point along the beacon walk. Split out so the
        /// reduce-motion path can jump straight to the far end (eased = 1) under a
        /// dissolve instead of turning through it.
        /// </summary>
        private void ApplyNavigationPose(Cinemachine.CinemachineVirtualCamera activeCamera, float eased)
        {
            if (_navigationMode == CameraDragMode.Orbit)
            {
                float yaw = Mathf.LerpAngle(_navigationStartYaw, _navigationTargetYaw, eased) * Mathf.Deg2Rad;
                float pitch = _navigationOrbitPitch * Mathf.Deg2Rad;
                float horizontalRadius = _navigationOrbitRadius * Mathf.Cos(pitch);
                var offset = new Vector3(
                    horizontalRadius * Mathf.Sin(yaw),
                    _navigationOrbitRadius * Mathf.Sin(pitch),
                    horizontalRadius * Mathf.Cos(yaw));
                activeCamera.transform.position = _navigationOrbitPivot + offset;
                activeCamera.transform.LookAt(_navigationOrbitPivot);
            }
            else
            {
                activeCamera.transform.position = Vector3.Lerp(
                    _navigationStartPosition,
                    _navigationTargetPosition,
                    eased);
            }
        }

        public Cinemachine.CinemachineVirtualCamera? GetActiveCamera()
        {
            return _gameManager.CurrentFocusCamera;
        }

        private Transform? GetOrbitPivot(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || config.dragMode != CameraDragMode.Orbit)
                return null;

            if (config.orbitPivot != null)
            {
                return config.orbitPivot;
            }

            string errorMsg = $"[SSNoir] Camera configuration error: Virtual Camera '{activeCamera.name}' is set to Orbit mode, but no orbitPivot was configured on its SSNoirVirtualCameraConfig.";
            Debug.LogError(errorMsg);
            UnityEngine.Assertions.Assert.IsTrue(false, errorMsg);
            throw new System.InvalidOperationException(errorMsg);
        }
    }
}
