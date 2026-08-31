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
        // 按下后是否已经用「刷新过的」UI 覆盖信息复核过一次。见 Update 里的说明。
        private bool _pressGateRechecked = false;
        // 按下发生在哪一帧。复核必须等到下一帧——同一帧里 OnGUI 还没跑，读到的仍是旧结论。
        private int _pressFrame = -1;
        private bool _isDraggingCam = false;
        private Vector3 _dragStartMousePos;
        private Vector3 _dragStartCamPos;

        // 松手后的惯性尾巴，三种模式共用一条。速度一律按「指针像素/秒」保存，到最后一步
        // 才按各自的模式换算成平移、旋转或偏移——于是"甩起来是什么手感"只有这一处可调，
        // 三种镜头不会各飘各的。
        //
        // 衰减是指数的，尾巴长度因此有个直白的读法：总位移 = 松手速度 ÷ InertiaDamping。
        // 12 的意思就是"甩得再快，也只再滑十二分之一秒的路"。
        private const float InertiaDamping = 12f;
        // 甩得再狠也就这么快。2400 像素/秒对应约 200 像素的尾巴。
        private const float MaxFlickPixelsPerSecond = 2400f;
        // 低于这个速度按"挪到位"处理，不给惯性。它和"指针有没有停住"是两道关，都要过。
        private const float MinFlickPixelsPerSecond = 200f;
        // 慢到这个程度直接归零，免得留一条肉眼看不见却永远在动的零头。
        private const float InertiaStopPixelsPerSecond = 12f;

        private Cinemachine.CinemachineVirtualCamera? _inertiaCamera;
        private CameraDragMode _inertiaMode;
        private Vector2 _inertiaVelocity;
        private readonly PointerVelocityTracker _pointer = new PointerVelocityTracker();

        // Static is a globally consistent presentation gesture, not a per-camera tuning
        // surface. These intentionally live here beside the other camera feel values.
        // 静态镜头只给一点点"我碰得到画面"的反馈，不是一个可以逛的小范围。所以这里没有
        // "自由区 + 越界才有橡皮筋"那一层：阻力从第一个像素就开始长，松手弹回作者构图。
        //
        // 曲线是对数的，**没有上限**：
        //
        //     b(x) = scale × ln(1 + x / scale)
        //
        // 原点处斜率正好是 1，所以起手完全跟手；之后每把手上的行程翻一倍，画面只多走
        // scale×ln2 ≈ 0.083 —— 一个固定的、越来越不划算的增量。拉到后面就是"再拖很远也
        // 只多一点点"，两只手交替倒着拉也一样。要的正是这个：理论上拉得动，实际上拉不动。
        //
        // 渐近线那种写法（位移趋近一个固定上限）不行：斜率会真的掉到 0，手上摸得出一堵墙，
        // 画面看起来像卡住了。对数永远还在走，只是越来越慢，所以反馈一直在。
        private const float StaticPullScale = 0.12f;
        // 起手的跟手程度：曲线在原点是 1:1，所以这就是最初每像素走多远。
        private const float StaticDragSensitivity = 0.012f;
        // 回弹：指数收敛，先快后软。
        private const float StaticReturnDamping = 14f;
        // 小到这个程度直接归位，免得留一条收不干净的尾巴。
        private const float StaticSettleOffset = 0.0005f;

        // Static cameras only permit a small screen-plane nudge around their authored
        // pose. Raw 记的是手拉了多远，Offset 是过完橡皮筋、真正落到画面上的那一份。
        //
        // 两者都是**向量**，橡皮筋作用在它的长度上——约束的是"构图允许偏离多远"，那是一个
        // 距离，没有分轴的含义。分轴各压各的会得到一个方形的边界：斜着拉能比正着拉多走
        // 41%，而且一个轴先到头之后，画面会变成只沿另一个轴动。手上摸得出那个角。
        private Cinemachine.CinemachineVirtualCamera? _staticOffsetCamera;
        private Vector2 _staticOffset;
        private Vector2 _staticRawOffset;
        private Vector2 _staticDragStartRawOffset;

        // Bounded Pan keeps an unbounded target position just like Static keeps a raw
        // offset. The camera displays the clamped position plus a rubber-band overscroll;
        // keeping the raw value makes inertia and re-grabbing during the return continuous.
        private Cinemachine.CinemachineVirtualCamera? _panBoundsCamera;
        private Vector3 _panRawPosition;
        private Vector3 _panDragStartRawPosition;

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
        private float _focusArcStartFieldOfView;
        private float _focusArcTargetFieldOfView;
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
        // 两台不同的相机架在同一个取景上时的容差。给得很紧：只想认出"作者故意让这两镜接上"
        // 这一种情况，别把真有一点点距离的运镜也吃掉。
        private const float SamePoseDistance = 0.05f;
        private const float SamePoseAngle = 0.25f;

        private Cinemachine.CinemachineBrain? _cutHoldBrain;
        private Cinemachine.CinemachineBlendDefinition _cutHoldSavedBlend;
        private int _cutHoldFrame;

        // Destination parked on the outgoing view while the freeze is being taken. How
        // many frames that is, is the crossfade's answer to give — see TickFocusTravel.
        private Cinemachine.CinemachineVirtualCamera? _reducedParkedCamera;
        private Vector3 _reducedTargetPosition;
        private Quaternion _reducedTargetRotation;
        private float _reducedTargetFieldOfView;
        private float _reducedTargetNearClip;
        private float _reducedTargetFarClip;

        public ViewCrossfade Crossfade => _crossfade;

        /// <summary>
        /// 交给 <see cref="BeginFocusTravel"/> 的那趟运镜是否还在路上——弧线本身，或者
        /// 减少动画下顶替它的那次溶解。
        ///
        /// 运镜期间 brain 是被切死的（弧线自己就是过渡），所以光问 brain「有没有在混合」
        /// 会当场得到「已经到位了」。任何要等镜头真停稳才能往下走的地方（过场等首帧对齐），
        /// 必须连这里一起问。
        /// </summary>
        public bool IsFocusTravelInFlight =>
            _isFocusArcActive || _crossfade.IsFading || _crossfade.IsCapturing
            || _reducedParkedCamera != null;

        /// <summary>
        /// 低动画换镜的整段：从画面被按在旧那一镜上等冻帧，到冻帧淡完为止。
        ///
        /// 这段时间里**相机已经在新机位上，屏幕上却还是旧画面**——世界靠冻帧盖住了，
        /// 跟着相机投影的那层 UI 没有。谁要跟着"看到的画面"走，就得问这里，别问相机。
        /// </summary>
        public bool IsReducedViewSwapping =>
            _crossfade.IsFading || _crossfade.IsCapturing || _reducedParkedCamera != null;

        /// <summary>
        /// 新那一镜在屏幕上露出了多少：0 = 还整个被旧画面盖着，1 = 已经完全是它了。
        ///
        /// 世界那半边由冻帧负责（溶解到哪儿就是多少），跟着相机投影的那层 UI 没有冻帧可用，
        /// 只能按同一条曲线自己淡进来。等冻帧的那一两帧算 0：那时相机已经在新机位上，
        /// 屏幕上却还是上一镜，这层东西一个都不该露脸。
        /// </summary>
        public float ReducedViewReveal
        {
            get
            {
                if (_crossfade.IsFading)
                    return 1f - _crossfade.Alpha;
                return _crossfade.IsCapturing || _reducedParkedCamera != null ? 0f : 1f;
            }
        }

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

            // 停在旧视角上等冻帧的那台相机，此刻并不是"当前镜头"——它只是替屏幕上那一镜
            // 站岗。这段时间里下面每一样按当前机位算的姿态维护（拖拽、惯性尾巴、平移回弹、
            // 静态回位）读到的都是一个不属于它的机位，算出来的结果就是把它甩到别处去；
            // 而这一帧恰恰还在冻帧就位之前，那一下直接露在屏幕上——切镜时先闪一帧不知
            // 哪儿的画面，再"跳回"旧画面开始溶解，就是这么来的。
            //
            // 站岗最多一两帧（见 TickFocusTravel），期间不接手势不会有感觉。
            if (_reducedParkedCamera != null)
                return;

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
                _pressGateRechecked = false;
                _pressFrame = Time.frameCount;
                _isDraggingCam = false;
                _dragStartMousePos = Input.mousePosition;

                // 按下就掐掉还在滑的尾巴——和列表滑到一半点一下就停住是同一件事。
                _inertiaVelocity = Vector2.zero;
            }

            // PointerOverUI 是上一帧 OnGUI 留下的结论。鼠标一直在移动，所以那份结论在按下的
            // 瞬间就已经是对的；手指不是——它落下之前根本没有位置可言，上一帧算的是上一次
            // 触碰的地方。于是"手指按在卡片上"会被判成"按在世界上"，一拖就同时拖动了镜头。
            //
            // 按下之后再复核一次：那时 OnGUI 已经按真正的触点跑过一遍了。放在这里而不是把
            // 按下时的判断整个挪后，是因为按压本身要立刻成立（惯性得马上停），只有"这次按压
            // 归不归镜头"可以晚一帧定。
            if (_isPressingWorld && !_pressGateRechecked && Time.frameCount != _pressFrame)
            {
                _pressGateRechecked = true;
                if (_gameManager.PointerOverUI)
                    _isPressingWorld = false;
            }

            // 骰子/物品一旦被拿在手上，这次按压就已经名花有主了。上面那两道闸门读的都是
            // OnGUI 留下的结论，早一帧晚一帧都可能错过；这一个不是推断，是 IMGUI 自己说的
            // 「我正拖着东西」，所以放在最后一道，也压得住前面两道漏掉的情况。
            // WebGL 上帧率比编辑器低且不稳，「差一帧」的窗口被拉长到几十毫秒，于是快速
            // 按下就拖的手势偶发地同时拖动了背景——正是这一条要堵的洞。
            if (_gameManager.IsDraggingResource && _isPressingWorld)
            {
                if (_isDraggingCam)
                    ReleaseDrag(activeCamera);
                _isPressingWorld = false;
                _isDraggingCam = false;
                _isDraggingFocusArc = false;
                _inertiaVelocity = Vector2.zero;
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

                            // 一帧一笔，三种模式共用同一份采样。松手时的速度就从这里读。
                            _pointer.Sample(Input.mousePosition);

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
                            else if (config != null && config.dragMode == CameraDragMode.Static)
                            {
                                UpdateStaticDrag(activeCamera, mouseDelta);
                                ApplyStaticRestPose(activeCamera, config);
                            }
                            else
                            {
                                UpdatePanDrag(activeCamera, mouseDelta);
                            }
                        }
                    }
                }
                else
                {
                    ReleaseDrag(activeCamera);
                    _isPressingWorld = false;
                    _isDraggingCam = false;
                    _isDraggingFocusArc = false;
                }
            }

            // 惯性先走，回弹后走：静态镜头的橡皮筋要能作用在这一帧刚滑出去的距离上。
            TickDragInertia(activeCamera);
            TickStaticReturn(activeCamera);
            TickPanReturn(activeCamera);

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

            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            _inertiaCamera = activeCamera;
            _inertiaMode = config != null ? config.dragMode : CameraDragMode.Pan;
            _inertiaVelocity = Vector2.zero;
            _pointer.Reset(Input.mousePosition);

            // Save starting极坐标 (polar coordinates) state on virtual camera config if in Orbit mode
            if (config != null && config.dragMode == CameraDragMode.Orbit)
            {
                var pivot = GetOrbitPivot(activeCamera);
                if (pivot != null)
                {
                    config.SaveDragStartState(pivot.position);
                }
            }
            else if (config != null && config.dragMode == CameraDragMode.Static)
            {
                BeginStaticDrag(activeCamera);
            }
            else if (config != null && config.dragMode == CameraDragMode.Pan)
            {
                BeginPanDrag(activeCamera, config);
            }
        }

        /// <summary>
        /// 松手：决定这一下留不留尾巴。
        ///
        /// 两道关都要过——指针在松手前那一小段还得在走（<c>IsParked</c>），而且走得够快。
        /// "按住挪到某个构图再松手"是定位，最后那一小段一定是静止的，不管之前拖得多快，
        /// 这种一点惯性都不该有；"滑动中甩开"才是甩。
        /// </summary>
        private void ReleaseDrag(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            if (!_isDraggingCam || _isDraggingFocusArc
                || !ReferenceEquals(_inertiaCamera, activeCamera))
            {
                _inertiaVelocity = Vector2.zero;
                return;
            }

            // 松手这一帧的位置还没进采样——它正是最有信息量的那一笔。
            _pointer.Sample(Input.mousePosition);

            Vector2 velocity = _pointer.IsParked ? Vector2.zero : _pointer.Velocity;
            _inertiaVelocity = velocity.magnitude >= MinFlickPixelsPerSecond
                ? Vector2.ClampMagnitude(velocity, MaxFlickPixelsPerSecond)
                : Vector2.zero;
        }

        /// <summary>
        /// 松手后的那条尾巴。速度按像素记，到这里才换算成各模式自己的动作，所以三种
        /// 镜头的减速曲线是同一条。
        /// </summary>
        private void TickDragInertia(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            if (_inertiaVelocity == Vector2.zero)
                return;

            // 拖拽重新接管、或者焦点运镜要用这台相机时，尾巴当场作废：两个系统同时写
            // 同一个 transform 就会打架。
            if (_isDraggingCam || _isFocusArcActive || !ReferenceEquals(_inertiaCamera, activeCamera))
            {
                _inertiaVelocity = Vector2.zero;
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            Vector2 stepPixels = _inertiaVelocity * deltaTime;
            _inertiaVelocity *= Mathf.Exp(-InertiaDamping * deltaTime);
            if (_inertiaVelocity.magnitude < InertiaStopPixelsPerSecond)
                _inertiaVelocity = Vector2.zero;

            switch (_inertiaMode)
            {
                case CameraDragMode.Orbit:
                    ApplyOrbitInertiaStep(activeCamera, stepPixels);
                    break;
                case CameraDragMode.Static:
                    ApplyStaticInertiaStep(activeCamera, stepPixels);
                    break;
                default:
                    ApplyPanInertiaStep(activeCamera, stepPixels);
                    break;
            }
        }

        // 以下三个 Apply*Step 的换算必须和各自 UpdateXxxDrag 里那一条完全一致，
        // 否则松手的瞬间画面会变速——尾巴和拖拽是同一个手势的两半。
        private void ApplyPanInertiaStep(
            Cinemachine.CinemachineVirtualCamera activeCamera, Vector2 stepPixels)
        {
            Vector3 right = activeCamera.transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 forward = activeCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 translation =
                -stepPixels.x * right * _panSpeed - stepPixels.y * forward * _panSpeed;
            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || !config.usePanBounds)
            {
                activeCamera.transform.position += translation;
                return;
            }

            EnsurePanBoundsState(activeCamera, config);
            _panRawPosition += translation;
            ApplyPanPosition(activeCamera, config, _panRawPosition);
        }

        private void ApplyOrbitInertiaStep(
            Cinemachine.CinemachineVirtualCamera activeCamera, Vector2 stepPixels)
        {
            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            var pivot = config != null ? GetOrbitPivot(activeCamera) : null;
            if (config == null || pivot == null)
            {
                _inertiaVelocity = Vector2.zero;
                return;
            }

            ToPolar(
                activeCamera.transform.position - pivot.position,
                out float yaw, out float pitch, out float radius);
            yaw += stepPixels.x * config.orbitSpeedX;
            pitch = Mathf.Clamp(
                pitch - stepPixels.y * config.orbitSpeedY, config.minPitch, config.maxPitch);
            activeCamera.transform.position = pivot.position + FromPolar(yaw, pitch, radius);
            activeCamera.transform.LookAt(pivot.position);
        }

        private void ApplyStaticInertiaStep(
            Cinemachine.CinemachineVirtualCamera activeCamera, Vector2 stepPixels)
        {
            if (!ReferenceEquals(_staticOffsetCamera, activeCamera))
            {
                _inertiaVelocity = Vector2.zero;
                return;
            }

            _staticRawOffset -= stepPixels * StaticDragSensitivity;
        }

        private void UpdatePanDrag(Cinemachine.CinemachineVirtualCamera activeCamera, Vector3 mouseDelta)
        {
            // Height-locked RTS/MOBA Pan (moves parallel to XZ ground plane)
            Vector3 right = activeCamera.transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 forward = activeCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 panTranslation = -mouseDelta.x * right * _panSpeed - mouseDelta.y * forward * _panSpeed;
            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || !config.usePanBounds)
            {
                activeCamera.transform.position = _dragStartCamPos + panTranslation;
                return;
            }

            EnsurePanBoundsState(activeCamera, config);
            _panRawPosition = _panDragStartRawPosition + panTranslation;
            ApplyPanPosition(activeCamera, config, _panRawPosition);
        }

        private void BeginPanDrag(
            Cinemachine.CinemachineVirtualCamera activeCamera,
            SSNoirVirtualCameraConfig config)
        {
            if (!config.usePanBounds)
                return;

            // Reconstruct the raw target from the visible position. This is what lets a
            // player grab the camera again while it is still springing back without a jump.
            _panBoundsCamera = activeCamera;
            _panRawPosition = RecoverPanRawPosition(activeCamera.transform.position, config);
            _panRawPosition.y = config.AuthoredPosition.y;
            _panDragStartRawPosition = _panRawPosition;
        }

        private void EnsurePanBoundsState(
            Cinemachine.CinemachineVirtualCamera activeCamera,
            SSNoirVirtualCameraConfig config)
        {
            if (ReferenceEquals(_panBoundsCamera, activeCamera))
                return;

            _panBoundsCamera = activeCamera;
            _panRawPosition = activeCamera.transform.position;
            _panRawPosition.y = config.AuthoredPosition.y;
            _panDragStartRawPosition = _panRawPosition;
        }

        private void SynchronizePanBoundsState(
            Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || config.dragMode != CameraDragMode.Pan || !config.usePanBounds)
                return;

            _panBoundsCamera = activeCamera;
            _panRawPosition = RecoverPanRawPosition(activeCamera.transform.position, config);
            _panRawPosition.y = config.AuthoredPosition.y;
            _panDragStartRawPosition = _panRawPosition;
        }

        private void TickPanReturn(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            if (_isDraggingCam || _isNavigating || _isFocusArcActive
                || !ReferenceEquals(_panBoundsCamera, activeCamera))
                return;

            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || config.dragMode != CameraDragMode.Pan || !config.usePanBounds)
                return;

            Vector2 rawPosition = new Vector2(_panRawPosition.x, _panRawPosition.z);
            Vector2 boundaryPosition = ClampPanPosition(rawPosition, config);
            Vector2 visibleOverscroll = ApplyRubberBand(rawPosition - boundaryPosition);
            if (visibleOverscroll.sqrMagnitude <= StaticSettleOffset * StaticSettleOffset)
            {
                _panRawPosition = new Vector3(
                    boundaryPosition.x, config.AuthoredPosition.y, boundaryPosition.y);
                activeCamera.transform.position = _panRawPosition;
                if (_inertiaMode == CameraDragMode.Pan)
                    _inertiaVelocity = Vector2.zero;
                return;
            }

            float returnFactor = 1f - Mathf.Exp(-StaticReturnDamping * Time.unscaledDeltaTime);
            visibleOverscroll = Vector2.Lerp(visibleOverscroll, Vector2.zero, returnFactor);
            if (visibleOverscroll.sqrMagnitude <= StaticSettleOffset * StaticSettleOffset)
                visibleOverscroll = Vector2.zero;

            Vector2 rawOverscroll = InverseRubberBand(visibleOverscroll);
            _panRawPosition = new Vector3(
                boundaryPosition.x + rawOverscroll.x,
                config.AuthoredPosition.y,
                boundaryPosition.y + rawOverscroll.y);
            ApplyPanPosition(activeCamera, config, _panRawPosition);

            if (visibleOverscroll == Vector2.zero && _inertiaMode == CameraDragMode.Pan)
                _inertiaVelocity = Vector2.zero;
        }

        private static Vector2 ClampPanPosition(
            Vector2 position,
            SSNoirVirtualCameraConfig config)
        {
            return new Vector2(
                Mathf.Clamp(position.x, config.panBoundsMinXZ.x, config.panBoundsMaxXZ.x),
                Mathf.Clamp(position.y, config.panBoundsMinXZ.y, config.panBoundsMaxXZ.y));
        }

        private static Vector3 ClampPanPosition(
            Vector3 position,
            SSNoirVirtualCameraConfig config)
        {
            Vector2 clamped = ClampPanPosition(new Vector2(position.x, position.z), config);
            return new Vector3(clamped.x, position.y, clamped.y);
        }

        private static Vector3 RecoverPanRawPosition(
            Vector3 visiblePosition,
            SSNoirVirtualCameraConfig config)
        {
            Vector2 visible = new Vector2(visiblePosition.x, visiblePosition.z);
            Vector2 boundary = ClampPanPosition(visible, config);
            Vector2 rawOverscroll = InverseRubberBand(visible - boundary);
            return new Vector3(
                boundary.x + rawOverscroll.x, visiblePosition.y, boundary.y + rawOverscroll.y);
        }

        private static void ApplyPanPosition(
            Cinemachine.CinemachineVirtualCamera activeCamera,
            SSNoirVirtualCameraConfig config,
            Vector3 rawPosition)
        {
            Vector2 raw = new Vector2(rawPosition.x, rawPosition.z);
            Vector2 boundary = ClampPanPosition(raw, config);
            Vector2 visibleOverscroll = ApplyRubberBand(raw - boundary);
            activeCamera.transform.position = new Vector3(
                boundary.x + visibleOverscroll.x, rawPosition.y,
                boundary.y + visibleOverscroll.y);
        }

        private void BeginStaticDrag(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            if (!ReferenceEquals(_staticOffsetCamera, activeCamera))
                ResetStaticOffset(activeCamera);

            _staticDragStartRawOffset = _staticRawOffset;
        }

        private void UpdateStaticDrag(
            Cinemachine.CinemachineVirtualCamera activeCamera,
            Vector3 mouseDelta)
        {
            if (!ReferenceEquals(_staticOffsetCamera, activeCamera))
                ResetStaticOffset(activeCamera);

            _staticRawOffset = _staticDragStartRawOffset
                - new Vector2(mouseDelta.x, mouseDelta.y) * StaticDragSensitivity;
            _staticOffset = ApplyRubberBand(_staticRawOffset);
        }

        private void TickStaticReturn(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            if (_isDraggingCam || !ReferenceEquals(_staticOffsetCamera, activeCamera))
                return;

            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || config.dragMode != CameraDragMode.Static)
                return;

            // 先把这一帧的惯性吃进画面里，再往回收——两者同时作用，不是二选一。甩出去的
            // 那一下先往外滑，橡皮筋一直在拉，于是它自己减速、掉头、回位。分成两段互斥的话，
            // 会先滑一段、停一下、再突然往回走。
            _staticOffset = ApplyRubberBand(_staticRawOffset);

            // 回收作用在**屏幕上看得见的位移**上，不是"手拉了多远"。橡皮筋压得越狠，同样
            // 一段 raw 对应的画面移动越小；直接收 raw 的话，拉得越远回弹越慢，正好反了。
            float returnFactor = 1f - Mathf.Exp(-StaticReturnDamping * Time.unscaledDeltaTime);
            _staticOffset = Vector2.Lerp(_staticOffset, Vector2.zero, returnFactor);

            if (_staticOffset.magnitude < StaticSettleOffset)
                _staticOffset = Vector2.zero;

            // raw 跟着回算，好让回弹半路上重新按住时接得上——手抓住的是画面，不是那个内部量。
            _staticRawOffset = InverseRubberBand(_staticOffset);

            // 回到位了就把尾巴掐掉，免得一条早就看不见的惯性还在跟橡皮筋拉锯。
            if (_staticOffset == Vector2.zero
                && _inertiaMode == CameraDragMode.Static)
                _inertiaVelocity = Vector2.zero;

            if (!_isFocusArcActive)
                ApplyStaticRestPose(activeCamera, config);
        }

        private void ResetStaticOffset(Cinemachine.CinemachineVirtualCamera activeCamera)
        {
            _staticOffsetCamera = activeCamera;
            _staticOffset = Vector2.zero;
            _staticRawOffset = Vector2.zero;
        }

        /// <summary>
        /// 指针速度采样。
        ///
        /// 松手那一瞬间"这一帧走了多远"是不能用来判断甩动的：那是十几毫秒的噪声，同一个
        /// 手势在不同帧率下会得出完全不同的答案。改成按时间取样，并且把两个问题分开问：
        ///
        ///   甩得多快     —— 最近 <see cref="VelocityWindow"/> 秒的位移除以这段时间。
        ///   松手前停没停 —— 最近 <see cref="StillWindow"/> 秒里指针一共走了几像素。
        ///
        /// 第二问才是"按住挪到某个构图再松手"和"滑动中甩开"的分界线：前者最后那一小段
        /// 一定是静止的，不管之前拖得多快。只看速度分不出这两种，因为定位手势的平均速度
        /// 也可以很高。
        /// </summary>
        private sealed class PointerVelocityTracker
        {
            private const float VelocityWindow = 0.1f;
            private const float StillWindow = 0.05f;
            private const float StillPixels = 5f;
            private const int Capacity = 24;

            private readonly float[] _times = new float[Capacity];
            private readonly Vector2[] _positions = new Vector2[Capacity];
            private int _count;
            private int _next;

            public void Reset(Vector2 position)
            {
                _count = 0;
                _next = 0;
                Sample(position);
            }

            public void Sample(Vector2 position)
            {
                _times[_next] = Time.unscaledTime;
                _positions[_next] = position;
                _next = (_next + 1) % Capacity;
                if (_count < Capacity)
                    _count++;
            }

            /// <summary>窗口内的平均速度，像素/秒。</summary>
            public Vector2 Velocity
            {
                get
                {
                    if (_count < 2)
                        return Vector2.zero;

                    Vector2 latest = At(0, out float latestTime);
                    Vector2 oldest = latest;
                    float oldestTime = latestTime;

                    // 至少吃下一笔间隔再看窗口，否则帧率一低窗口里就只剩当前这一笔，
                    // 永远算出零速度——那正是"怎么甩都没有惯性"的另一半原因。
                    for (int i = 1; i < _count; i++)
                    {
                        oldest = At(i, out oldestTime);
                        if (latestTime - oldestTime >= VelocityWindow)
                            break;
                    }

                    float elapsed = latestTime - oldestTime;
                    return elapsed <= 0.0001f ? Vector2.zero : (latest - oldest) / elapsed;
                }
            }

            /// <summary>松手前指针是不是已经停住了——停住就是定位，不是甩。</summary>
            public bool IsParked
            {
                get
                {
                    if (_count < 2)
                        return true;

                    Vector2 previous = At(0, out float latestTime);
                    float travelled = 0f;
                    for (int i = 1; i < _count; i++)
                    {
                        Vector2 position = At(i, out float time);
                        travelled += (previous - position).magnitude;
                        if (travelled > StillPixels)
                            return false;

                        previous = position;
                        if (latestTime - time >= StillWindow)
                            break;
                    }

                    return true;
                }
            }

            // 0 是最新的一笔，往后依次更旧。
            private Vector2 At(int indexFromLatest, out float time)
            {
                int index = ((_next - 1 - indexFromLatest) % Capacity + Capacity) % Capacity;
                time = _times[index];
                return _positions[index];
            }
        }

        /// <summary>
        /// 橡皮筋：拉得越远越沉，但永远还在走。
        ///
        ///     b(x) = scale × ln(1 + x / scale)
        ///
        /// 压的是**向量的长度**，方向原样保留，所以边界是圆的。分轴各压各的会压出一个方形：
        /// 斜着拉能比正着拉多走 41%，而且一个轴先到头之后画面会拐成只沿另一个轴动。约束的
        /// 本意是"构图允许偏离多远"，那是一个距离，不该有角。
        /// </summary>
        private static Vector2 ApplyRubberBand(Vector2 rawOffset)
        {
            float magnitude = rawOffset.magnitude;
            if (magnitude <= Mathf.Epsilon)
                return Vector2.zero;

            float pulled = StaticPullScale * Mathf.Log(1f + magnitude / StaticPullScale);
            return rawOffset * (pulled / magnitude);
        }

        /// <summary>把屏幕上的位移换回"手拉了多远"，<see cref="ApplyRubberBand"/> 的反函数。</summary>
        private static Vector2 InverseRubberBand(Vector2 offset)
        {
            float magnitude = offset.magnitude;
            if (magnitude <= Mathf.Epsilon)
                return Vector2.zero;

            // 指数是反函数自带的。位移本身长得极慢，指数不会真的炸；上限只是别把浮点喂坏。
            float raw = StaticPullScale
                * (Mathf.Exp(Mathf.Min(magnitude / StaticPullScale, 20f)) - 1f);
            return offset * (raw / magnitude);
        }

        private Vector3 GetStaticOffset(SSNoirVirtualCameraConfig config)
        {
            return config.AuthoredRotation * new Vector3(_staticOffset.x, _staticOffset.y, 0f);
        }

        private void ApplyStaticRestPose(
            Cinemachine.CinemachineVirtualCamera activeCamera,
            SSNoirVirtualCameraConfig config)
        {
            activeCamera.transform.SetPositionAndRotation(
                config.AuthoredPosition + GetStaticOffset(config), config.AuthoredRotation);
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

            if (_focusArcInputMode == CameraDragMode.Static)
            {
                if (_focusArcInputConfig == null)
                    throw new System.InvalidOperationException($"[SSNoir] Static focus travel camera '{activeCamera.name}' has no camera config.");
                BeginStaticDrag(activeCamera);
            }

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
            else if (_focusArcInputMode == CameraDragMode.Static)
            {
                if (_focusArcInputConfig == null)
                    throw new System.InvalidOperationException($"[SSNoir] Static focus travel camera '{activeCamera.name}' has no camera config.");
                UpdateStaticDrag(activeCamera, mouseDelta);
            }
            else
            {
                _focusArcPanOffset = _focusArcDragStartPanOffset
                    - mouseDelta.x * _focusArcPanRight * _panSpeed
                    - mouseDelta.y * _focusArcPanForward * _panSpeed;
            }

            ApplyFocusArcPose(FocusArcEasedProgress());
        }

        // [CAM] 高度守卫的容差与去重。容差要盖得住浮点误差和运镜落点的最后一点零头，
        // 又要小到任何真的"沉下去/飘起来"都逃不掉。
        private const float PanHeightTolerance = 0.05f;
        private const float PanRotationTolerance = 0.5f;
        private readonly System.Collections.Generic.HashSet<string> _panPoseOffenders =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// [CAM] Pan 相机的姿态守卫：它只允许在 XZ 平面上平移，高度和朝向都该恒等于作者构图。
        ///
        /// 拖拽（<see cref="UpdatePanDrag"/>）和信标行走都是显式锁高度的，所以高度一旦对不上，
        /// 就一定是别的东西写了这个 transform——最可能是焦点运镜的弧线（<see cref="ApplyFocusArcPose"/>
        /// 是按极坐标重建整个姿态的，只要它的兴趣点算错，落点就会连高度一起偏）。
        /// 报错时把当时正在跑的运动一并打出来，就是为了指认凶手。
        ///
        /// 每台相机只报一次，回到正确高度后重新武装——不然一帧一条会把日志刷没。
        /// </summary>
        public void CheckPanCameraPose()
        {
            // Focus travel intentionally writes a complete temporary pose: reduced-motion
            // parks the destination camera on the rendered camera, while the focus arc
            // rebuilds height and rotation on every frame. The guard runs before
            // TickFocusTravel, so checking here would report those valid transition poses
            // as Pan violations. Once the travel is over, the normal check resumes and
            // still catches a pose that was actually left behind.
            if (IsFocusTravelInFlight)
                return;

            var camera = GetActiveCamera();
            if (camera == null)
                return;

            var config = camera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config == null || config.dragMode != CameraDragMode.Pan)
                return;

            float heightDrift = camera.transform.position.y - config.AuthoredPosition.y;
            float angleDrift = Quaternion.Angle(camera.transform.rotation, config.AuthoredRotation);
            bool offending = Mathf.Abs(heightDrift) > PanHeightTolerance || angleDrift > PanRotationTolerance;

            if (!offending)
            {
                _panPoseOffenders.Remove(camera.name);
                return;
            }

            if (!_panPoseOffenders.Add(camera.name))
                return;

            string motion = _isFocusArcActive ? "焦点运镜（ApplyFocusArcPose）"
                : _isNavigating ? "信标行走（ApplyNavigationPose）"
                : _isDraggingCam ? "拖拽（UpdatePanDrag）"
                : _inertiaVelocity != Vector2.zero ? "惯性尾巴（ApplyPanInertiaStep）"
                : "无（这一帧没有任何相机运动在写它——是更早某一帧留下的）";

            Debug.LogError(
                $"[CAM] Pan 相机 '{camera.name}' 的姿态被改出了平面：高度偏 {heightDrift:F2}m、" +
                $"朝向偏 {angleDrift:F1}°。Pan 只允许在 XZ 上平移。" +
                $"当前机位 {camera.transform.position}，作者机位 {config.AuthoredPosition}。" +
                $"此刻正在写它的运动：{motion}。");
        }

        public void NavigateToNode(string nodeName)
        {
            var anchor = _gameManager.ResolveAnchor(nodeName);
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
        public bool BeginFocusTravel(
            Cinemachine.CinemachineVirtualCamera focusCamera,
            bool respectReduceMotion = true)
        {
            return BeginFocusTravel(focusCamera, out _, respectReduceMotion);
        }

        /// <summary>
        /// 和 <see cref="BeginFocusTravel(CinemachineVirtualCamera)"/> 相同，同时给出这次画面过渡
        /// 的设计时长。手写弧线、减少动画的溶解和退回 Cinemachine 的默认 blend 都会如实返回；
        /// 已在目标构图或 Debug 硬切则是零。调用方可以据此让伴随演出和运镜同起同落，而不用猜
        /// 当前到底走了哪一条相机路径。过场等需要保留完整镜头运动的调用方可以将
        /// <paramref name="respectReduceMotion"/> 设为 false。
        /// </summary>
        public bool BeginFocusTravel(
            Cinemachine.CinemachineVirtualCamera focusCamera,
            out float transitionDuration,
            bool respectReduceMotion = true)
        {
            transitionDuration = 0f;

            // A repeated refresh of the focus that is already travelling must be a
            // no-op. Finishing first would snap the running arc to t=1 before the
            // "already active" check below gets a chance to reject the duplicate.
            if (_isFocusArcActive && ReferenceEquals(_focusArcCamera, focusCamera))
            {
                transitionDuration = Mathf.Max(
                    0f, _focusArcDuration - (Time.unscaledTime - _focusArcStartedAt));
                return true;
            }

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

            var config = focusCamera.GetComponent<SSNoirVirtualCameraConfig>();
            bool destinationUsesAuthoredPose = config != null && config.dragMode != CameraDragMode.Pan;
            if (config != null && config.dragMode == CameraDragMode.Static)
                ResetStaticOffset(focusCamera);
            Vector3 targetPosition = destinationUsesAuthoredPose ? config!.AuthoredPosition : focusCamera.transform.position;
            Quaternion targetRotation = destinationUsesAuthoredPose ? config!.AuthoredRotation : focusCamera.transform.rotation;
            Vector3 startPosition = renderedCamera.transform.position;
            Quaternion startRotation = renderedCamera.transform.rotation;

            // 换的是相机，不是画面：目的地的取景和此刻屏幕上的一模一样。过场末镜架在下一场的
            // 机位上正是这种情况——两台不同的 vcam，同一个构图。上面那条早退只认「同一台相机」，
            // 认不出这个。走到这儿的任何过渡都是在原地耗时间（弧线两秒，brain 的默认混合也是
            // 两秒），画面一动不动，看上去就是卡住了。这种情况直接切。
            if ((targetPosition - startPosition).sqrMagnitude <= SamePoseDistance * SamePoseDistance
                && Quaternion.Angle(targetRotation, startRotation) <= SamePoseAngle)
                return BeginInstantFocusChange(focusCamera, brain);

            // Reduce motion takes the same fork every time, whatever the two ends are:
            // no road at all, just a dissolve over a cut.
            if (respectReduceMotion && MotionSettings.ReduceMotion)
            {
                transitionDuration = MotionSettings.CrossfadeDuration;
                return BeginReducedFocusChange(focusCamera, brain, renderedCamera);
            }

            float duration = brain.m_DefaultBlend.m_Time;
            transitionDuration = Mathf.Max(0f, duration);
            if (duration <= 0.01f)
                return false;

            float startNearClip = renderedCamera.nearClipPlane;
            float targetNearClip = focusCamera.m_Lens.NearClipPlane;
            float startFarClip = renderedCamera.farClipPlane;
            float targetFarClip = focusCamera.m_Lens.FarClipPlane;
            float startFieldOfView = renderedCamera.fieldOfView;
            float targetFieldOfView = focusCamera.m_Lens.FieldOfView;

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
            _focusArcStartFieldOfView = startFieldOfView;
            _focusArcTargetFieldOfView = targetFieldOfView;
            _focusArcStartNearClip = startNearClip;
            _focusArcTargetNearClip = targetNearClip;
            _focusArcStartFarClip = startFarClip;
            _focusArcTargetFarClip = targetFarClip;
            _focusArcInputMode = config != null ? config.dragMode : CameraDragMode.Pan;
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
            SetLens(focusCamera, startFieldOfView, startNearClip, startFarClip);

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
            if (config != null && config.dragMode != CameraDragMode.Pan)
            {
                if (config.dragMode == CameraDragMode.Static)
                    ResetStaticOffset(focusCamera);
                focusCamera.transform.SetPositionAndRotation(
                    config.AuthoredPosition, config.AuthoredRotation);
            }

            _cutHoldSavedBlend = brain.m_DefaultBlend;
            brain.m_DefaultBlend = new Cinemachine.CinemachineBlendDefinition(
                Cinemachine.CinemachineBlendDefinition.Style.Cut, 0f);
            _cutHoldBrain = brain;
            _cutHoldFrame = Time.frameCount;
            SynchronizePanBoundsState(focusCamera);
            return true;
        }

        /// <summary>
        /// Hands the focus change to a cut hidden under a dissolve — the reduce-motion
        /// form of <see cref="BeginFocusTravel"/>. Nothing travels, so nothing sweeps
        /// past the player; the old shot simply dissolves off the new one.
        ///
        /// The whole thing turns on getting the freeze *before* the cut: the brain reaches
        /// LateUpdate and would have already snapped by the time the frame is captured,
        /// leaving us dissolving the new shot onto itself. So the destination is parked on
        /// the outgoing view: the brain cuts to it and nothing changes on screen, the
        /// freeze takes its copy, and only then is the camera released to its real pose,
        /// underneath the frozen frame. 停多久由冻帧说了算（<see cref="TickFocusTravel"/>），
        /// 这里不数帧——从 OnGUI 点出来的换镜要比从 Update 发起的多等一帧。
        /// </summary>
        private bool BeginReducedFocusChange(
            Cinemachine.CinemachineVirtualCamera focusCamera,
            Cinemachine.CinemachineBrain brain,
            Camera renderedCamera)
        {
            _isNavigating = false;
            _isDraggingCam = false;
            // 甩出去的那条尾巴属于刚被换掉的那一镜。留着它，新机位一露面就会自己滑一段——
            // 低动画模式下更是无从解释的一下漂移。
            _inertiaVelocity = Vector2.zero;

            // A focus change landing on top of a parked one puts the previous destination
            // back where it belongs first; otherwise it stays stranded on a stale view.
            ReleaseReducedPark();

            var config = focusCamera.GetComponent<SSNoirVirtualCameraConfig>();
            bool destinationUsesAuthoredPose = config != null && config.dragMode != CameraDragMode.Pan;
            if (config != null && config.dragMode == CameraDragMode.Static)
                ResetStaticOffset(focusCamera);

            // Read the destination before parking overwrites it.
            _reducedParkedCamera = focusCamera;
            _reducedTargetPosition = destinationUsesAuthoredPose ? config!.AuthoredPosition : focusCamera.transform.position;
            _reducedTargetRotation = destinationUsesAuthoredPose ? config!.AuthoredRotation : focusCamera.transform.rotation;
            _reducedTargetFieldOfView = focusCamera.m_Lens.FieldOfView;
            _reducedTargetNearClip = focusCamera.m_Lens.NearClipPlane;
            _reducedTargetFarClip = focusCamera.m_Lens.FarClipPlane;

            focusCamera.transform.SetPositionAndRotation(
                renderedCamera.transform.position, renderedCamera.transform.rotation);
            SetLens(
                focusCamera, renderedCamera.fieldOfView,
                renderedCamera.nearClipPlane, renderedCamera.farClipPlane);

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
            SetLens(
                _reducedParkedCamera, _reducedTargetFieldOfView,
                _reducedTargetNearClip, _reducedTargetFarClip);
            SynchronizePanBoundsState(_reducedParkedCamera);
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
            // where it really belongs.
            //
            // 停到冻帧抓完为止，不数帧。抓帧相机是当帧就渲还是晚一帧，取决于这次换镜从
            // Update 还是从 OnGUI 发起（见 ViewCrossfade 的时序说明）；按帧数放行的话，
            // 从卡片点出来的那一类换镜会在冻帧就位前一帧就把新机位露出来——「先闪一下
            // 新画面，再退回旧画面开始溶解」正是这么来的。溶解压根没起来（IsCapturing
            // 一直是 false）时这里立刻放行，退回一次普通硬切。
            if (_reducedParkedCamera != null && !_crossfade.IsCapturing)
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

            if (_focusArcInputMode == CameraDragMode.Static && _focusArcInputConfig != null)
                position += GetStaticOffset(_focusArcInputConfig);

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
            SetLens(
                _focusArcCamera,
                Mathf.Lerp(_focusArcStartFieldOfView, _focusArcTargetFieldOfView, eased),
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
            {
                ApplyFocusArcPose(1f);
                SynchronizePanBoundsState(completedCamera);
            }

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
            SetLens(camera, camera.m_Lens.FieldOfView, nearClipPlane, farClipPlane);
        }

        private static void SetLens(
            Cinemachine.CinemachineVirtualCamera camera,
            float fieldOfView,
            float nearClipPlane,
            float farClipPlane)
        {
            if (fieldOfView <= 0f || fieldOfView >= 180f
                || nearClipPlane <= 0f || farClipPlane <= nearClipPlane)
            {
                string message =
                    $"[SSNoir] Camera '{camera.name}' has an invalid lens: " +
                    $"FOV {fieldOfView:F2}, clip {nearClipPlane:F3}..{farClipPlane:F3}.";
                Debug.LogError(message);
                UnityEngine.Assertions.Assert.IsTrue(false, message);
                throw new System.InvalidOperationException(message);
            }

            var lens = camera.m_Lens;
            lens.FieldOfView = fieldOfView;
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

            var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
            if (config != null && config.dragMode == CameraDragMode.Static)
                return;

            _isDraggingCam = false;

            // A beacon click during a transition waits for the shot to land, then walks
            // on from there — the two must not drive the same camera at once.
            FinishFocusTravel();

            _navigationCamera = activeCamera;
            _navigationStartedAt = Time.unscaledTime;
            _navigationStartPosition = activeCamera.transform.position;

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
                if (config != null && config.dragMode == CameraDragMode.Pan && config.usePanBounds)
                    _navigationTargetPosition = ClampPanPosition(_navigationTargetPosition, config);
            }

            _isNavigating = true;

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

                    // Then park it back there for as long as the freeze needs. Landing on
                    // the destination before the frozen frame exists to cover it would put
                    // the swing's endpoint on screen for a frame — exactly the flicker
                    // being avoided.
                    Vector3 parkPosition = activeCamera.transform.position;
                    Quaternion parkRotation = activeCamera.transform.rotation;

                    ApplyNavigationPose(activeCamera, 1f);
                    _reducedParkedCamera = activeCamera;
                    _reducedTargetPosition = activeCamera.transform.position;
                    _reducedTargetRotation = activeCamera.transform.rotation;
                    _reducedTargetFieldOfView = activeCamera.m_Lens.FieldOfView;
                    _reducedTargetNearClip = activeCamera.m_Lens.NearClipPlane;
                    _reducedTargetFarClip = activeCamera.m_Lens.FarClipPlane;

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
                Vector3 position = Vector3.Lerp(
                    _navigationStartPosition,
                    _navigationTargetPosition,
                    eased);
                var config = activeCamera.GetComponent<SSNoirVirtualCameraConfig>();
                if (config == null || config.dragMode != CameraDragMode.Pan || !config.usePanBounds)
                {
                    activeCamera.transform.position = position;
                    return;
                }

                _panBoundsCamera = activeCamera;
                _panRawPosition = RecoverPanRawPosition(position, config);
                _panRawPosition.y = config.AuthoredPosition.y;
                ApplyPanPosition(activeCamera, config, _panRawPosition);
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
