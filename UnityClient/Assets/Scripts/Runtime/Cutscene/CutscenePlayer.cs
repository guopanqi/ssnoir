#nullable enable
using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Video;
using SSNoir.IMGUI;

namespace SSNoir
{
    /// <summary>
    /// 过场播放：推到第一个机位 → 压黑边进影幕 → 逐镜放片子（中间换镜头不动黑边）
    /// → 收黑边 → 回原机位。
    ///
    /// 三条规矩，破一条整场就散：
    ///
    /// 1. <b>视频按整幅 16:9 铺满屏幕，黑边只是盖在它上面的遮罩。</b>不能把视频缩进黑边中间
    ///    那条窄带——首帧是照全屏 16:9 截的，缩一下构图就变了，切进视频那一瞬间画面会跳。
    ///    黑边是取景框，不是画布。
    ///
    /// 2. <b>影幕是整场一个框。</b>压下来一次、收起来一次，中间换几次镜头都不动它。每镜各压
    ///    各收的话，镜头之间会闪一下白场，观感直接垮掉。
    ///
    /// 3. <b>每次进入一个机位都要等它真的停稳才开播</b>（见 <c>IsSettledOn</c>）。视频首帧是
    ///    照着某个固定机位截出来的，镜头还在动的时候切进去，第一帧就对不上。
    ///
    /// 「回来」这一头引擎管不了：视频末帧是什么样，切回实时渲染时世界就得是什么样。单镜过场
    /// 要设计成闭环（人进门、门关上、画面回到空门口，末帧约等于首帧）；多镜过场每多一段就多
    /// 一处这样的接缝——上一镜的末帧得停在下一次运镜起点复现得出来的状态上。
    /// </summary>
    public class CutscenePlayer : MonoBehaviour
    {
        private enum Phase
        {
            Idle,
            Approach,     // 推向当前这一镜的机位，等它停稳
            LetterboxIn,  // 黑边压下来（整场仅一次）
            Playing,      // 放这一镜的片子
            LetterboxOut, // 黑边收回去（整场仅一次）
            Return,       // 回原机位
        }

        // 镜头推不到位时的兜底。运镜被别的东西打断、或者压根没动起来时，不能把玩家永远关在
        // 影幕里——超时就照常往下走，宁可首帧对不齐也不能卡死。
        private const float MoveTimeout = 4f;

        // 没有视频的镜头仍要留出一小段时间，方便单独检查运镜和影幕。
        private const float EmptyShotHoldSeconds = 3f;

        // 影幕是全游戏统一的视觉语言，不允许由单场过场改写。
        private const float LetterboxAspect = 2.39f;
        private const float LetterboxDuration = 0.5f;

        // 片子迟迟没准备好时的兜底（解码失败、URL 打不开、平台不支持这个编码）。
        private const float VideoStartTimeout = 10f;

        // 过场机位必须压过场上所有相机。焦点相机拿 20，全局 10（见 SSNoirGameManager），
        // 留足余量。真出现比这还高的相机，那是场景配置该被发现的问题，不该在这里悄悄绕过去。
        private const int CutscenePriority = 100;

        private SSNoirGameManager _gameManager = null!;
        private VideoPlayer? _video;
        private RenderTexture? _videoTexture;
        private CinemachineBrain? _brain;

        private Phase _phase = Phase.Idle;
        private float _phaseStartedAt;

        private CutsceneSequence? _sequence;
        private readonly List<CutsceneShot> _shots = new List<CutsceneShot>();
        private int _shotIndex;
        private Action? _onComplete;

        // 当前占用着高优先级的那一镜，以及它原本的优先级。一次只有一个，切镜头时先还再占。
        private CutsceneShot? _activeShot;
        private int _activeShotOriginalPriority;

        private CinemachineVirtualCamera? _returnCamera;

        // 影幕是否真的压下来过。收场路径靠它决定要不要走收黑边——不能靠阶段推断：
        // 镜头全部配置失效时会一路跳到收尾，那时黑边根本没出现过，再"收"一次就是凭空闪一下。
        private bool _letterboxRaised;

        private bool _hasVideo;
        private bool _videoPending;
        private bool _videoFinished;
        private float _playDeadline;
        private int _playStartedFrame;

        /// <summary>影幕是否占着画面。为真时世界照常渲染，但所有游戏 UI 都不出现。</summary>
        public bool IsActive => _phase != Phase.Idle;

        public void Initialize(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
        }

        /// <summary>
        /// 播一场过场。<paramref name="onComplete"/> 在整场结束（含被跳过）后调用一次，
        /// 剧情靠它推进——所以任何退出路径都必须走到 <see cref="Finish"/>。
        /// </summary>
        public void Play(CutsceneSequence sequence, Action? onComplete = null)
        {
            if (IsActive)
            {
                Debug.LogWarning(
                    $"[SSNoir] 过场 '{sequence.DisplayName}' 被忽略：另一场还在播。", sequence);
                onComplete?.Invoke();
                return;
            }

            _shots.Clear();
            foreach (var shot in sequence.ValidShots())
                _shots.Add(shot);

            if (_shots.Count == 0)
            {
                Debug.LogWarning(
                    $"[SSNoir] 过场 '{sequence.DisplayName}' 一个镜头都没有，跳过。", sequence);
                onComplete?.Invoke();
                return;
            }

            _sequence = sequence;
            _onComplete = onComplete;
            _shotIndex = 0;
            _letterboxRaised = false;

            // 记下当前这一镜，散场时回到它。
            _returnCamera = _gameManager.CameraManager.GetActiveCamera();

            // 在跑的焦点运镜先落位：回程要回到的是那台相机站稳之后的位置，不是它半路上的
            // 位置。过场自己也用同一套运镜，一次只能有一趟在飞。
            _gameManager.CameraManager.FinishFocusTravel();

            EnterShot(_shots[0]);
        }

        /// <summary>
        /// 跳过整场。调试用——ESC 只有电脑上按得到，正式平台没有这个入口。
        ///
        /// 不瞬间收场：影幕已经压下来的话还是照常收黑边、回机位，否则画面会硬跳一下。
        /// 剧情推进照走，跳过不等于这一步没发生。
        /// </summary>
        public void Skip()
        {
            if (!IsActive)
                return;

            StopVideo();
            BeginWindDown();
        }

        private void Update()
        {
            if (_phase == Phase.Idle || _sequence == null)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Skip();
                return;
            }

            float elapsed = Time.unscaledTime - _phaseStartedAt;

            switch (_phase)
            {
                case Phase.Approach:
                    // 超时也放行：卡在这里等于把玩家锁死在一个没有 UI 的世界里。
                    if (IsSettledOn(_activeShot?.Camera) || elapsed >= MoveTimeout)
                    {
                        // 第一镜要先把影幕压下来；后面几镜影幕早就在了，直接开播。
                        if (_shotIndex == 0)
                            EnterPhase(Phase.LetterboxIn);
                        else
                            BeginPlayback();
                    }
                    break;

                case Phase.LetterboxIn:
                    if (elapsed >= LetterboxDuration)
                        BeginPlayback();
                    break;

                case Phase.Playing:
                    // 有片子等它放完，没片子就干等一段——影幕和运镜要能脱离视频单独测。
                    // 期限是兜底，正常都走上面那条。
                    if ((_hasVideo && _videoFinished) || Time.unscaledTime >= _playDeadline)
                        AdvanceShot();
                    break;

                case Phase.LetterboxOut:
                    if (elapsed >= LetterboxDuration)
                    {
                        StopVideo();
                        BeginReturn();
                    }
                    break;

                case Phase.Return:
                    if (_returnCamera == null || IsSettledOn(_returnCamera) || elapsed >= MoveTimeout)
                        Finish();
                    break;
            }
        }

        /// <summary>放完当前这一镜，接下一镜；没有下一镜就收影幕。</summary>
        private void AdvanceShot()
        {
            _shotIndex++;

            if (_shotIndex >= _shots.Count)
            {
                BeginWindDown();
                return;
            }

            StopVideo();
            EnterShot(_shots[_shotIndex]);
        }

        /// <summary>
        /// 走收场流程。影幕压下来过就先收黑边，否则直接回程——没出现过的东西不用收，
        /// 硬走一遍收黑边会让画面凭空闪一条黑边。
        /// </summary>
        private void BeginWindDown()
        {
            // 收场一旦开始就只能向前。否则 Return 阶段再次按 ESC 会倒退回 LetterboxOut，
            // 随后重复发起回程运镜，造成镜头瞬切后重新推一次。
            if (_phase == Phase.LetterboxOut || _phase == Phase.Return)
                return;

            if (_letterboxRaised)
            {
                EnterPhase(Phase.LetterboxOut);
                return;
            }

            BeginReturn();
        }

        /// <summary>
        /// 交还机位并起回程运镜。
        ///
        /// 先还优先级、再起运镜，顺序反了就露馅：BeginFocusTravel 读的是"此刻屏幕上是什么"，
        /// 它会把回程相机瞬移到那儿再往回飞。这一帧 brain 里活着的还是过场机位（优先级
        /// 改动要等下一次 LateUpdate 才重排），所以起点正好是这一镜，接上的那一下看不见。
        ///
        /// 回程目标还要被显式扶成最高优先级。运镜只搬相机、不改优先级，两者对不上就会出现
        /// 运镜在飞 A、brain 在放 B。平时它自然成立（回的就是刚才那台焦点相机），但过场跨了
        /// 场景切换时不成立：场上留着的是旧场景的优先级，新场景的镜头正是在这一刻接管。
        /// </summary>
        private void BeginReturn()
        {
            ReleaseCameras();

            if (_returnCamera != null)
            {
                _gameManager.PromoteFocusCamera(_returnCamera);
                _gameManager.CameraManager.BeginFocusTravel(_returnCamera);
            }

            EnterPhase(Phase.Return);
        }

        /// <summary>占住这一镜的机位，开始运镜过去。</summary>
        private void EnterShot(CutsceneShot shot)
        {
            var camera = shot.Camera;
            if (camera == null)
            {
                Debug.LogWarning($"[SSNoir] 镜头 '{shot.DisplayName}' 没有虚拟相机，跳过这一镜。", shot);
                AdvanceShot();
                return;
            }

            // 上一镜先把优先级还回去，否则两镜同为最高，谁赢取决于队列顺序。
            ReleaseActiveShot();

            _activeShot = shot;
            _activeShotOriginalPriority = camera.Priority;

            // 和游戏里其他每一次换镜走同一条路：焦点运镜的那条弧线，同样的时长，同样受
            // 减少动画影响。玩家不该能从运镜方式上看出「这一下是过场」。
            //
            // 它解不出兴趣点时会返回 false（平视机位的中心射线打不到地面就是这种情况），
            // 那就什么都没发生，下面抬完优先级由 brain 按默认混合直线推过去——退化成直线，
            // 不是不动。
            _gameManager.CameraManager.BeginFocusTravel(camera);

            camera.Priority = CutscenePriority;

            // Cinemachine 2.x 的 Priority setter 只改字段，内部优先队列要等 vcam 自己的
            // Update 才重排。过场可能是在 OnGUI 的点击里发起的，不立刻通知队列，brain 就会
            // 带着旧排序进入下一帧。
            camera.MoveToTopOfPrioritySubqueue();

            PrepareVideo(shot);
            EnterPhase(Phase.Approach);
        }

        private void BeginPlayback()
        {
            // 走到这儿说明黑边要么已经压完（第一镜），要么一直挂着（后面几镜）。
            _letterboxRaised = true;
            StartVideo();
            EnterPhase(Phase.Playing);
        }

        /// <summary>
        /// 画影幕。由 IMGUIWorldRenderer 在游戏 UI 之前调用，调完那边就直接 return——
        /// 影幕期间一个控件都不该出现。
        /// </summary>
        public void Draw()
        {
            if (_sequence == null)
                return;

            float vw = UIScale.VW;
            float vh = UIScale.VH;

            // 视频铺满整幅，和截首帧时的取景一模一样。黑边是盖在上面的遮罩，不参与构图。
            //
            // 收黑边的过程里也接着画：片子放完 VideoPlayer 停在末帧，让这一帧顶到黑边完全收起
            // 再切回实时世界。切换点越晚，画面上还剩的东西越少，末帧和世界万一对不齐也越不显眼。
            // _hasVideo 要一起判——贴图是复用的，这一镜没配片子时不挡住它就会放出上一镜的画面。
            if (_hasVideo && _videoTexture != null && VideoShowsCurrentShot
                && (_phase == Phase.Playing || _phase == Phase.LetterboxOut))
            {
                DrawVideo(new Rect(0, 0, vw, vh));
            }

            float fullBar = Mathf.Max(0f, (vh - vw / LetterboxAspect) * 0.5f);
            float bar = fullBar * LetterboxProgress();
            if (bar <= 0f)
                return;

            var previousColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, vw, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, vh - bar, vw, bar), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        /// <summary>
        /// 把这一镜的画面铺满 <paramref name="area"/>。
        ///
        /// 走涂装材质，和实时世界过同一个 shader、同一份参数——这是统一涂装成立的前提。视频
        /// 和 3D 分别涂各自的一层就等于没涂：两张网纹对不齐，反而比不涂更显眼。材质拿不到
        /// （没装 Feature、或被关了）时退回原样绘制，过场照播不误。
        ///
        /// 用 <c>Graphics.DrawTexture</c> 而不是 <c>GUI.DrawTexture</c>，是因为只有前者能指定
        /// 材质。代价是它不认 <c>ScaleMode</c>，裁剪得自己算（见 <see cref="CropToFill"/>），
        /// 而且只能在 Repaint 事件里调——GUI.DrawTexture 内部替你挡了这一层，这里得自己挡。
        /// </summary>
        private void DrawVideo(Rect area)
        {
            var material = Rendering.SSNoirStylizeMaterial.Shared;
            if (material == null)
            {
                GUI.DrawTexture(area, _videoTexture, ScaleMode.ScaleAndCrop, false);
                return;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            var source = CropToFill(
                area.width / area.height,
                (float)_videoTexture!.width / _videoTexture.height);

            Graphics.DrawTexture(
                area, _videoTexture, source, 0, 0, 0, 0,
                material, Rendering.SSNoirStylizeMaterial.GuiPass);
        }

        /// <summary>
        /// 算出 ScaleAndCrop 对应的源矩形（归一化贴图坐标）：铺满画幅，多出来的那一头对称裁掉。
        ///
        /// 只能裁不能缩——首帧是照全屏 16:9 截的，缩一下构图就变了，切进视频那一瞬间画面会跳。
        /// </summary>
        private static Rect CropToFill(float areaAspect, float textureAspect)
        {
            if (areaAspect > textureAspect)
            {
                // 画幅比片子宽：宽度铺满，上下各裁掉一条。
                float height = textureAspect / areaAspect;
                return new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }

            float width = areaAspect / textureAspect;
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        /// <summary>0 = 没有黑边，1 = 影幕完全就位。</summary>
        private float LetterboxProgress()
        {
            if (_sequence == null)
                return 0f;

            float elapsed = Time.unscaledTime - _phaseStartedAt;
            float duration = Mathf.Max(0.01f, LetterboxDuration);

            return _phase switch
            {
                // 第一镜是从实时画面推过来的，那时还没有影幕；之后每次换镜头影幕都还挂着。
                Phase.Approach => _letterboxRaised ? 1f : 0f,
                Phase.LetterboxIn => Mathf.SmoothStep(0f, 1f, elapsed / duration),
                Phase.Playing => 1f,
                Phase.LetterboxOut => Mathf.SmoothStep(1f, 0f, elapsed / duration),
                _ => 0f,
            };
        }

        private void EnterPhase(Phase phase)
        {
            _phase = phase;
            _phaseStartedAt = Time.unscaledTime;
        }

        // ── 相机 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 镜头这一镜是不是已经真的停稳了。
        ///
        /// 两头都要问。焦点运镜跑起来的时候 brain 是被切死的——弧线自己就是过渡——这时候
        /// 单问 brain 会立刻回答"到了"，视频就会在镜头还在飞的半路上开播，首帧当场对不上。
        /// 反过来，运镜没接手（解不出兴趣点，退回直线）的时候只有 brain 知道混合完没完。
        /// </summary>
        private bool IsSettledOn(CinemachineVirtualCamera? camera)
        {
            if (camera == null)
                return true;

            if (_gameManager.CameraManager.IsFocusTravelInFlight)
                return false;

            if (_brain == null)
            {
                var main = Camera.main;
                _brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
            }

            if (_brain == null)
                return true;

            return ReferenceEquals(_brain.ActiveVirtualCamera as CinemachineVirtualCamera, camera)
                && !_brain.IsBlending;
        }

        private void ReleaseActiveShot()
        {
            if (_activeShot == null)
                return;

            var camera = _activeShot.Camera;
            if (camera != null)
                camera.Priority = _activeShotOriginalPriority;

            _activeShot = null;
        }

        /// <summary>
        /// 把占着的机位还回去，让原来的焦点相机重新胜出。这里不再 MoveToTop：那是让人插队
        /// 用的，回程要的正相反。
        ///
        /// 只负责"还"，不负责"飞回去"——回程那一趟由 <see cref="BeginReturn"/> 显式起运镜。
        /// 也在 <see cref="Finish"/> 里兜底调用一次，防止哪条路径漏了归还。
        /// </summary>
        private void ReleaseCameras()
        {
            ReleaseActiveShot();
        }

        // ── 视频 ──────────────────────────────────────────────────────────

        private void PrepareVideo(CutsceneShot shot)
        {
            _hasVideo = false;
            _videoPending = false;
            _videoFinished = false;

            if (!shot.HasVideo)
            {
                Debug.LogWarning(
                    $"[SSNoir] 过场镜头 '{shot.DisplayName}' 没有填写视频文件名，将停留 {EmptyShotHoldSeconds:0.#} 秒。",
                    shot);
                return;
            }

            string videoPath = Application.streamingAssetsPath + "/Cutscenes/" + shot.VideoFileName;

#if !UNITY_WEBGL || UNITY_EDITOR
            if (!System.IO.File.Exists(videoPath))
            {
                Debug.LogError(
                    $"[SSNoir] 过场镜头 '{shot.DisplayName}' 的视频文件不存在：{videoPath}。"
                    + $"将按未填写处理，停留 {EmptyShotHoldSeconds:0.#} 秒。",
                    shot);
                return;
            }
#endif

            if (_video == null)
            {
                _video = gameObject.AddComponent<VideoPlayer>();
                _video.playOnAwake = false;
                _video.isLooping = false;
                _video.renderMode = VideoRenderMode.RenderTexture;
                _video.source = VideoSource.Url;
                _video.loopPointReached += _ => _videoFinished = true;
                _video.prepareCompleted += OnVideoPrepared;
                _video.errorReceived += OnVideoError;
            }

            // WebGL 的 streamingAssetsPath 是 URL，不能用 File.Exists 探测；交给 VideoPlayer
            // 打开，失败后由 errorReceived 报错并回退为空镜。
            _video.url = videoPath;
            _videoPending = true;

            // 运镜和压黑边的这一两秒正好用来解码首帧，轮到播的时候就不会卡一下。
            _video.Prepare();
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogError(
                $"[SSNoir] 过场视频打不开：{source.url}\n{message}——将按未填写处理，"
                + $"停留 {EmptyShotHoldSeconds:0.#} 秒。", this);
            _videoPending = false;
            _hasVideo = false;
            _playDeadline = Time.unscaledTime + EmptyShotHoldSeconds;
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            int width = (int)source.width;
            int height = (int)source.height;
            if (width <= 0 || height <= 0)
                return;

            if (_videoTexture != null
                && (_videoTexture.width != width || _videoTexture.height != height))
            {
                _videoTexture.Release();
                Destroy(_videoTexture);
                _videoTexture = null;
            }

            if (_videoTexture == null)
            {
                // ReadWrite.Linear 的意思是「读写都不做 sRGB 转换，按原样搬」——名字和效果
                // 正好相反，别被它骗了。视频解出来的本就是显示值，GUI 也是照显示值往屏幕上画，
                // 这条路径上不该有任何一次编码。留 Default 的话线性工程里会给它盖上 sRGB
                // 标记，写入时多编码一次，整段片子就比源文件亮一截、发白。
                _videoTexture = new RenderTexture(
                    width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "SSNoir.Cutscene",
                };
            }

            source.targetTexture = _videoTexture;
            _videoPending = false;
            _hasVideo = true;

            // 已经在放了才知道真实时长，把兜底期限从「启动超时」续成「片长 + 余量」。
            if (_phase == Phase.Playing)
                _playDeadline = Time.unscaledTime + (float)source.length + 2f;
        }

        /// <summary>
        /// 贴图里现在装的是不是这一镜的画面。
        ///
        /// 多镜共用一张 RenderTexture 是有代价的：轮到下一镜时它还留着上一段片子。更麻烦的是
        /// <c>Stop()</c> 不只是停——它把播放位置倒回 0，并且顺手把那一帧刷进目标贴图，所以留
        /// 下的正是「上一段片子的第一帧」。第二段开播时解码还没赶上，那一帧就露出来了。编辑器
        /// 里解码快，下一帧就被盖掉，看不出来；打包之后慢一点就看得见。
        ///
        /// <see cref="StartVideo"/> 已经在开播前把贴图清干净了，所以最坏情况只会是黑，不会是
        /// 别人的画面。这里再进一步：这一镜的第一帧真解出来之前干脆不画贴图，让实时世界透过来
        /// ——镜头此刻正停在这一镜的构图上，首帧本来就该和它一模一样，露出来的是「对的画面」，
        /// 比黑一下好得多。
        ///
        /// 末尾那个帧数兜底是保命的：万一某个平台的 <c>frame</c> 压根不走，也不能永远不画视频。
        /// 门槛给得宽（十几帧，约四分之一秒），因为两头的代价不对称——等久一点只是多露几帧
        /// 实时世界，而那本来就该和首帧一样；抢早一点露出来的是刚清空的黑。
        /// </summary>
        private bool VideoShowsCurrentShot =>
            _video != null && (_video.frame > 0 || Time.frameCount - _playStartedFrame > 15);

        /// <summary>
        /// 把贴图刷成黑的。开播前调用，清掉上一镜残留的画面——不能指望"反正马上会被新画面
        /// 盖掉"，那正是这个 bug 的成因。
        /// </summary>
        private void ClearVideoTexture()
        {
            if (_videoTexture == null)
                return;

            var previous = RenderTexture.active;
            RenderTexture.active = _videoTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        private void StartVideo()
        {
            float now = Time.unscaledTime;
            _playStartedFrame = Time.frameCount;

            // 清在这里而不是 PrepareVideo：Stop() 那次回写是异步的，紧挨着清会被它反超。
            // 到这一步运镜已经走完（默认两秒），残留的写入早就落定了。
            ClearVideoTexture();

            if (_video != null && (_hasVideo || _videoPending))
            {
                // 还在 pending 也照样 Play：VideoPlayer 会自己等准备完成。期限先按启动超时
                // 给，等 prepareCompleted 回来知道真实时长了再续。
                _video.Play();
                _playDeadline = now + (_hasVideo ? (float)_video.length + 2f : VideoStartTimeout);
                return;
            }

            _playDeadline = now + EmptyShotHoldSeconds;
        }

        private void StopVideo()
        {
            if (_video != null && _video.isPlaying)
                _video.Stop();
        }

        // ── 收场 ──────────────────────────────────────────────────────────

        private void Finish()
        {
            // 保险：任何路径漏了归还，都在这里兜住。
            ReleaseCameras();

            _phase = Phase.Idle;
            _sequence = null;
            _shots.Clear();
            _shotIndex = 0;
            _returnCamera = null;
            _hasVideo = false;
            _videoPending = false;
            _videoFinished = false;

            // 回调放最后：剧情推进可能立刻又起一场过场，状态得先干净。
            var onComplete = _onComplete;
            _onComplete = null;
            onComplete?.Invoke();
        }

        private void OnDestroy()
        {
            if (_videoTexture != null)
            {
                _videoTexture.Release();
                Destroy(_videoTexture);
            }
        }
    }
}
