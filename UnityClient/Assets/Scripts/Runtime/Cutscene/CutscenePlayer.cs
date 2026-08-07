#nullable enable
using Cinemachine;
using UnityEngine;
using UnityEngine.Video;
using SSNoir.IMGUI;

namespace SSNoir
{
    /// <summary>
    /// 过场播放：镜头推到指定机位 → 压黑边进影幕 → 放片子 → 收黑边 → 镜头回原处。
    ///
    /// 全程要守住一件事：<b>视频按整幅 16:9 铺满屏幕，黑边只是盖在它上面的遮罩</b>。不能把
    /// 视频缩进黑边中间那条窄带里——首帧是照着全屏 16:9 截出来的，缩一下构图就变了，切进
    /// 视频的那一瞬间画面会跳，无缝就没了。黑边是取景框，不是画布。
    ///
    /// 同理，进影幕之前一定要等镜头真的切到位（见 <c>IsSettledOn</c>），镜头还在动的时候
    /// 第一帧就对不上。
    ///
    /// 「回来」这一头引擎管不了：视频末帧是什么样，切回实时渲染时世界就得是什么样。所以过场
    /// 在内容上要设计成闭环——人走进门、门关上、画面回到空门口，末帧约等于首帧。停在一个游戏
    /// 里复现不出来的状态上，收黑边那一下必然跳。
    /// </summary>
    public class CutscenePlayer : MonoBehaviour
    {
        private enum Phase
        {
            Idle,
            FlyIn,        // 镜头推向过场机位，等它停稳
            LetterboxIn,  // 黑边压下来
            Playing,      // 放片子（没配视频就是干等 HoldSeconds）
            LetterboxOut, // 黑边收回去
            FlyOut,       // 镜头回原处
        }

        // 镜头推不到位时的兜底。运镜被别的东西打断、或者压根没动起来时，不能把玩家永远关在
        // 影幕里——超时就照常往下走，宁可首帧对不齐也不能卡死。
        private const float FlyTimeout = 4f;

        // 片子迟迟没准备好时的兜底（解码失败、URL 打不开、平台不支持这个编码）。同样不能把
        // 玩家关死在影幕里。
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
        private CutsceneShot? _shot;
        private CinemachineVirtualCamera? _returnCamera;
        private int _shotOriginalPriority;
        private bool _hasVideo;
        private bool _videoPending;
        private bool _videoFinished;

        // Playing 阶段的绝对结束时刻。正常收尾靠 loopPointReached，这只是兜底——解码卡住或
        // 事件没来的话，没有它玩家就永远出不来了。
        private float _playDeadline;

        /// <summary>影幕是否占着画面。为真时世界照常渲染，但所有游戏 UI 都不出现。</summary>
        public bool IsActive => _phase != Phase.Idle;

        public void Initialize(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
        }

        /// <summary>播一镜过场。已经在播时忽略。</summary>
        public void Play(CutsceneShot shot)
        {
            if (IsActive)
                return;

            var camera = shot.Camera;
            if (camera == null)
            {
                Debug.LogWarning($"[SSNoir] 过场 '{shot.DisplayName}' 没有虚拟相机，播不了。", shot);
                return;
            }

            // 记下当前这一镜，散场时回到它。
            _returnCamera = _gameManager.CameraManager.GetActiveCamera();
            _shot = shot;
            _shotOriginalPriority = camera.Priority;
            _hasVideo = false;
            _videoPending = false;
            _videoFinished = false;

            PrepareVideo(shot);

            // 在跑的焦点运镜先落位。那条弧线每帧都在改目标相机的 transform，和过场抢同一台
            // brain 只会互相拉扯。
            _gameManager.CameraManager.FinishFocusTravel();

            // 只动优先级，让 Cinemachine 自己混过去，不走焦点运镜。
            //
            // 焦点弧线是为"绕着一个兴趣点转"写的：它把相机视线打到地面解出那个点，再绕着它
            // 画弧。焦点相机都是俯视看建筑的，绕着建筑转正是想要的；过场机位却可能贴地平视，
            // 脚下那个交点不代表任何东西，绕着它转出来的路径纯属偶然。更麻烦的是那个点有时
            // 解得出、有时解不出，解不出就静默退回 Cinemachine 的 blend——同一套调用两种
            // 运镜，取决于相机角度。过场要的本来也只是"切到这一镜"，交给 brain 最直接。
            camera.Priority = CutscenePriority;

            // Cinemachine 2.x 的 Priority setter 只改字段，内部优先队列要等 vcam 自己的
            // Update 才重排。过场是在 OnGUI 的点击里发起的，不立刻通知队列，brain 就会带着
            // 旧排序进入下一帧。
            camera.MoveToTopOfPrioritySubqueue();

            EnterPhase(Phase.FlyIn);
        }

        private void Update()
        {
            if (_phase == Phase.Idle || _shot == null)
                return;

            float elapsed = Time.unscaledTime - _phaseStartedAt;

            switch (_phase)
            {
                case Phase.FlyIn:
                    // 超时也放行：卡在这里等于把玩家锁死在一个没有 UI 的世界里。
                    if (IsSettledOn(_shot.Camera) || elapsed >= FlyTimeout)
                        EnterPhase(Phase.LetterboxIn);
                    break;

                case Phase.LetterboxIn:
                    if (elapsed >= _shot.LetterboxDuration)
                    {
                        StartVideo();
                        EnterPhase(Phase.Playing);
                    }
                    break;

                case Phase.Playing:
                    // 有片子等它放完，没片子就干等一段——影幕和运镜要能脱离视频单独测。
                    // 期限是兜底，正常都走上面那条。
                    if ((_hasVideo && _videoFinished) || Time.unscaledTime >= _playDeadline)
                        EnterPhase(Phase.LetterboxOut);
                    break;

                case Phase.LetterboxOut:
                    if (elapsed >= _shot.LetterboxDuration)
                    {
                        StopVideo();
                        RestoreCamera();
                        EnterPhase(Phase.FlyOut);
                    }
                    break;

                case Phase.FlyOut:
                    // 没有可回的相机就别等，直接收工。
                    if (_returnCamera == null || IsSettledOn(_returnCamera) || elapsed >= FlyTimeout)
                        Finish();
                    break;
            }
        }

        /// <summary>
        /// 画影幕。由 IMGUIWorldRenderer 在游戏 UI 之前调用，调完那边就直接 return——
        /// 影幕期间一个控件都不该出现。
        /// </summary>
        public void Draw()
        {
            if (_shot == null)
                return;

            float vw = UIScale.VW;
            float vh = UIScale.VH;

            // 视频铺满整幅，和截首帧时的取景一模一样。黑边是盖在上面的遮罩，不参与构图。
            //
            // 收黑边的过程里也接着画：片子放完 VideoPlayer 停在末帧，让这一帧顶到黑边完全收起
            // 再切回实时世界。切换点越晚，画面上还剩的东西越少，末帧和世界万一对不齐也越不显眼。
            // _hasVideo 要一起判——贴图是复用的，这一镜没配片子时不挡住它就会放出上一镜的画面。
            if (_hasVideo && _videoTexture != null
                && (_phase == Phase.Playing || _phase == Phase.LetterboxOut))
            {
                GUI.DrawTexture(new Rect(0, 0, vw, vh), _videoTexture, ScaleMode.ScaleAndCrop, false);
            }

            float fullBar = Mathf.Max(0f, (vh - vw / Mathf.Max(0.1f, _shot.LetterboxAspect)) * 0.5f);
            float bar = fullBar * LetterboxProgress();
            if (bar <= 0f)
                return;

            var previousColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, vw, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, vh - bar, vw, bar), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        /// <summary>0 = 没有黑边，1 = 影幕完全就位。</summary>
        private float LetterboxProgress()
        {
            if (_shot == null)
                return 0f;

            float elapsed = Time.unscaledTime - _phaseStartedAt;
            float duration = Mathf.Max(0.01f, _shot.LetterboxDuration);

            return _phase switch
            {
                Phase.FlyIn => 0f,
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

        private void PrepareVideo(CutsceneShot shot)
        {
            if (string.IsNullOrWhiteSpace(shot.VideoFileName))
                return;

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

            // 不能拿 File.Exists 探路，也不能用 Path.Combine 拼。WebGL 上 streamingAssetsPath
            // 是一个 URL，那儿没有文件系统：File.Exists 一律返回 false，包里每段视频都会被判成
            // "不存在"然后静默跳过——其余一切正常，就是没有画面。路径按 URL 拼好直接交给
            // VideoPlayer，能不能打开由它说了算，开不了会从 errorReceived 回来。
            _video.url = Application.streamingAssetsPath + "/Cutscenes/" + shot.VideoFileName;
            _videoPending = true;

            // 黑边压下来的这半秒正好用来解码首帧，轮到播的时候就不会卡一下。
            _video.Prepare();
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            // 不中断过场：影幕流程本身还是有测试价值的，缺片子只是少了中间那段。
            Debug.LogWarning(
                $"[SSNoir] 过场视频打不开：{source.url}\n{message}——这次只走影幕。", this);
            _videoPending = false;
            _hasVideo = false;
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
                // 标记，写入时多编码一次，整段片子就比 QuickTime 里亮一截、发白。
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

        private void StartVideo()
        {
            float now = Time.unscaledTime;

            if (_video != null && (_hasVideo || _videoPending))
            {
                // 还在 pending 也照样 Play：VideoPlayer 会自己等准备完成。期限先按启动超时
                // 给，等 prepareCompleted 回来知道真实时长了再续。
                _video.Play();
                _playDeadline = now + (_hasVideo ? (float)_video.length + 2f : VideoStartTimeout);
                return;
            }

            _playDeadline = now + (_shot != null ? _shot.HoldSeconds : 0f);
        }

        private void StopVideo()
        {
            if (_video != null && _video.isPlaying)
                _video.Stop();
        }

        /// <summary>
        /// 镜头这一镜是不是已经切到位、且不在混合中。
        ///
        /// 只问 brain，不问焦点运镜——过场走的是纯优先级切换，brain 说了算。
        /// </summary>
        private bool IsSettledOn(CinemachineVirtualCamera? camera)
        {
            if (camera == null)
                return true;

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

        private void RestoreCamera()
        {
            if (_shot == null)
                return;

            // 把优先级还回去就够了：原来的焦点相机重新胜出，brain 按自己的混合曲线接手，
            // 镜头自己就飞回去了。回程走焦点运镜反而会去搬那台相机的 transform。
            var camera = _shot.Camera;
            if (camera == null)
                return;

            // 只还原优先级，不再 MoveToTop——那是让它插队用的，回程要的正相反：退出竞争，
            // 让原来的焦点相机重新赢。重排晚一帧无所谓，镜头本来就在飞回去的路上。
            camera.Priority = _shotOriginalPriority;
        }

        private void Finish()
        {
            _phase = Phase.Idle;
            _shot = null;
            _returnCamera = null;
            _videoFinished = false;
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
