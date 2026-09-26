#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public class IMGUIWorldRenderer : MonoBehaviour
    {
        private SSNoirGameManager _gameManager = null!;
        private IMGUIAnimationPlayer _animator = null!;
        private PresentationPlayer _presentationPlayer = null!;
        private UnityNarrationPlayer _narrationPlayer = null!;
        private DialogueVoicePlayer _voicePlayer = null!;
        private BanterPlayer _banterPlayer = null!;
        private ConversationPlayer _conversationPlayer = null!;
        private StoryStagePlayer _storyStagePlayer = null!;
        private readonly DialogueAnchors _dialogueAnchors = new DialogueAnchors();
        private bool _warnedAboutCurrentDialogueRemoteFallback;
        private string? _activeVideoTag;
        private float _animationTimer;

        // 这一步的收尾归过场播放器管，Update 里的占位倒计时要让开——否则两边都会去推进
        // 下一个剧情步骤，同一步走两次。
        private bool _videoOwnedByCutscene;

        private bool _isGrowthPanelOpen = false;
        private readonly IMGUIWindowStack _windowStack = new();

        private readonly Dictionary<string, Vector2> _cardCenters = new Dictionary<string, Vector2>();
        // 见 DrawCards 里 RegisterWorldPoint 的注释。
        private const float BanterHeadHeight = 1.6f;
        // 动作卡首次避让后的落点：Pan 存世界坐标，Orbit / Static 存屏幕坐标。
        private readonly Dictionary<string, Vector3> _panActionWorldCenters = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Vector2> _screenActionCenters = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Vector2> _stackFootprints = new Dictionary<string, Vector2>();
        private SSNoirVirtualCameraConfig? _actionLayoutCamera;
        private CameraDragMode _actionLayoutMode;
        private Rect _actionLayoutSafeArea;
        private bool _hasActionLayoutContext;
        // 结果停留：结算演完、新快照采纳前，结果条挂在宿主卡下面的那几秒。键是动作名。
        // 采纳快照时一起清掉，不留到下一手——旧快照的卡、新快照的钟，只在这段时间里同台。
        private readonly Dictionary<string, CardPresentationResidue> _cardResidues = new Dictionary<string, CardPresentationResidue>();
        // 判定条、结果条等不参与卡片布局；它们在所有卡本体之后统一绘制，才不会被近景卡遮住。
        private readonly List<CardAttachmentOverlay> _cardAttachmentOverlays = new List<CardAttachmentOverlay>();
        // 本帧世界投射层的浓度，由 DrawCards 写、附件那一段读——附件画在卡片之后、
        // 物品栏之上，隔着几个绘制阶段，只能这样把浓度带过去。
        private float _cardLayerReveal = 1f;
        // 本帧的引线。与 attachment 相反，它们在所有卡本体**之前**统一绘制。
        private readonly List<CardLeaderLineDrawer.Tether> _tethers = new List<CardLeaderLineDrawer.Tether>();
        private readonly List<float> _gridScrollStack = new();
        private float _gridScrollOffset = 0f;
        // 本帧的顶栏布局。顶栏是「世界内容从哪开始」的唯一依据，网格视口和边缘信标都问它。
        private TopHudLayout _topHud;
        // 本帧钉住条实际占的位置。卡片要绕开它，和顶栏按钮一样。
        private Rect _pinStripRect;
        // 本帧场景标注带占了多大。卡片排布与网格视口都要按它往下让，所以它必须在
        // 任何卡片定位之前量好；没有场景标注时是一个零高度的空矩形。
        private Rect _sceneBandRect;
        // 触控拖拽滚动的状态：手机没有滚轮，卡片网格只能靠手指推。
        private bool _gridDragActive;
        private float _gridDragLastY;
        private float _gridDragTravel;
        private int _lastNavigationDepth = 0;
        // 教程要圈的那张卡：本帧画出来的第一张「能投骰子的动作卡」，屏幕坐标。
        // 网格与空间投射两条路都往这里报，教程那边不必知道当前是哪一种版面。
        private Rect? _firstActionCardRect;
        // 结果条的去留只有一条规则：**采纳新快照会不会把结果从屏幕上抹掉**。
        //  · 不会（常规动作，哪怕这一手把卡打没了）：剧情步骤放完立刻采纳，玩家马上能做别的；
        //    结果条挂在活卡（或替消失的卡站着的残卡）下面烧几秒自己走，点一下也走。
        //  · 会（这一手翻页要黑幕，或者换了场景/阶段根）：先停住不采纳，让结果条烧完（或点一下）
        //    再采纳。这时玩家在旧场景里本来也没有事可做，停住不算拦人。
        // 两条路用的是同一根结果条、同一根绳子，只差 done() 什么时候调。
        private CardPresentationResidue? _holdingResidue;
        private ActionReport? _holdingReport;
        private Action? _holdingDone;
        private bool _holdBeforeBlocking;
        private bool _holdingBeforeBlocking;
        // 这次表现事务是否允许在阻塞步骤全部播完后再次进入结果停留。
        // 送医/换场的结果停留已经在阻塞步骤之前完成，后面可能还有多张 Spotlight 或对白，
        // 所以这个策略必须贯穿整串步骤，不能用“一次性跳过”标志。
        private bool _allowOutcomeHoldAfterBlocking = true;
        // 结果条挂多久。
        private const float OutcomeHoldBaseSeconds = 1.6f;
        private const float OutcomeHoldPerEffectSeconds = 0.35f;
        private const float OutcomeHoldRollExtraSeconds = 0.4f;
        private const float OutcomeHoldMaxSeconds = 4.0f;
        private readonly Queue<BlockingStoryStep> _pendingBlockingSteps = new Queue<BlockingStoryStep>();
        // 自动行动（别人拿走你的骰子去办的一件事）按玩家自己出手的节奏走，只是每一步都替他做了：
        //   卡出现 → 骰子从手里飞过去 → 落定停一拍 → 执行 → 结果条挂在卡下停一会 → 收起。
        // 第一版三步加起来一秒出头，玩家还没看清多了一张卡它就没了；时钟推了几格全靠猜。
        // 现在每一步都留出"看见它"的时间，结果条和玩家自己的动作一样挂出来，说清推了哪根钟。
        // 剧本里紧跟其后的对话 / banter 在这之后才播：先看见事情发生，再听人说话——
        // 和玩家自己出手时「执行 → 结果 → 台词」是同一个顺序。
        private BlockingStoryStep? _activeAutoAction;
        private float _activeAutoActionStartedAt;
        private float _activeAutoActionPreludeUntil;
        private float _activeAutoActionCaptureStartedAt;
        private bool _activeAutoActionPreludeStarted;
        private bool _activeAutoActionResolved;
        private bool _activeBeat;
        private float _activeBeatUntil;
        // 对方回合的一拍 = 引擎的一批。节奏固定：
        //   t=0        在动的卡亮起；这拍里开口的人，气泡从他卡上冒出
        //   t=落地     快照落地＝事情发生：钟脉冲、"−1" 飘起、冷静条脉冲，同一帧
        //   t=收尾     金边褪去，下一拍
        // 谁在动不靠脚本声明：把落地前后的两份快照按卡逐根钟比一遍，钟变了的卡就是在动的人；
        // 再加上这拍里开口的人。
        private const float SilentBeatSeconds = 0.9f;
        private const float SilentBeatLandAt = 0.3f;
        private const float SpokenBeatSeconds = 1.6f;
        private const float SpokenBeatLandAt = 0.45f;
        private const float ActingFadeSeconds = 0.6f;
        private readonly List<string> _actingNodeNames = new List<string>();
        private float _actingSince;
        private float _actingUntil;
        private float _beatLandAt;
        private Action? _beatLand;
        private readonly List<DialogueSequence> _beatNarrations = new List<DialogueSequence>();
        private readonly List<string> _beatNarrationIds = new List<string>();
        private const float AutoActionAppearSeconds = 0.5f;
        private const float AutoActionFlightSeconds = 0.6f;
        private const float AutoActionLandHoldSeconds = 0.35f;
        private const float AutoActionExecuteSeconds = 0.5f;
        private const float AutoActionResultHoldSeconds = 2.2f;
        private const float AutoActionExecuteStart = AutoActionFlightSeconds + AutoActionLandHoldSeconds;
        private const float AutoActionResolveAt = AutoActionExecuteStart + AutoActionExecuteSeconds;
        private const float AutoActionDuration = AutoActionResolveAt + AutoActionResultHoldSeconds;
        private readonly Dictionary<string, Rect> _drawnCardRects = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<GameNode> _sceneBandNotes = new List<GameNode>();
        private readonly Queue<DialogueSequence> _pendingImmediateDialogues = new Queue<DialogueSequence>();
        private SpotlightCard? _activeActionSpotlight;
        private ActionReport? _completionReport;
        private string _completionActionName = string.Empty;
        private Action? _completionDone;

        public void Initialize(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
            // 立绘标志色由剧情写在全局 "立绘色/<人>" 里（engine.scm 的 set-portrait-accent!）。
            StoryStageDrawer.AccentOverride = name =>
                _gameManager.GameState.Get<object>("立绘色/" + name) as string;
            TutorialState.Bind(gameManager.GameState);
            _animator = gameObject.AddComponent<IMGUIAnimationPlayer>();
            _presentationPlayer = new PresentationPlayer(_animator);
            _narrationPlayer = gameObject.AddComponent<UnityNarrationPlayer>();
            _voicePlayer = gameObject.AddComponent<DialogueVoicePlayer>();
            _banterPlayer = new BanterPlayer(_voicePlayer);
            _conversationPlayer = new ConversationPlayer(_voicePlayer);
            _storyStagePlayer = new StoryStagePlayer(_voicePlayer, gameObject.AddComponent<AudioSource>());
            _animator.OnAcknowledged = () => _presentationPlayer.OnRollAcknowledged();
            _gameManager.GameState.NarrationCenter.OnNarrationRequested += ShowNarration;
            _gameManager.GameState.DialogueCenter.OnBanterRequested += _banterPlayer.Enqueue;
            _gameManager.GameState.DialogueCenter.OnDialogueRequested += StartImmediateDialogue;
            _gameManager.GameState.DialogueCenter.OnStageRequested += StartImmediateStage;
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.GameState.NarrationCenter.OnNarrationRequested -= ShowNarration;
                _gameManager.GameState.DialogueCenter.OnBanterRequested -= _banterPlayer.Enqueue;
                _gameManager.GameState.DialogueCenter.OnDialogueRequested -= StartImmediateDialogue;
                _gameManager.GameState.DialogueCenter.OnStageRequested -= StartImmediateStage;
            }
        }

        // 动作外即时触发的阻塞对话:暂停 banter,演完恢复(不接入动作表现流程)。
        private void StartImmediateDialogue(SSNoir.Core.DialogueSequence sequence)
        {
            if (_conversationPlayer.IsActive)
            {
                _pendingImmediateDialogues.Enqueue(sequence);
                return;
            }

            _banterPlayer.Suspend();
            StartNextImmediateDialogue(sequence);
        }

        private void StartImmediateStage(StoryStageSequence sequence)
        {
            if (_storyStagePlayer.IsActive || _conversationPlayer.IsActive)
                throw new InvalidOperationException("舞台演出与对白不能重叠");
            _banterPlayer.Suspend();
            _storyStagePlayer.Start(sequence, () => _banterPlayer.Resume());
        }

        private void StartNextImmediateDialogue(SSNoir.Core.DialogueSequence sequence)
        {
            // 动作外即时对话仍以"此刻是否在场"校验说话人；普通 dialogue 不在场时会报警。
            StoryStageDrawer.BeginConversation();
            _conversationPlayer.Start(sequence, () =>
            {
                if (_pendingImmediateDialogues.Count > 0)
                {
                    StartNextImmediateDialogue(_pendingImmediateDialogues.Dequeue());
                    return;
                }

                _banterPlayer.Resume();
            });
        }

        public bool IsPresentationActive => _presentationPlayer.IsPlaying || _animator.IsPlaying
            || _activeAutoAction != null
            || _activeBeat
            || _activeActionSpotlight != null
            || _conversationPlayer.IsActive || _storyStagePlayer.IsActive || _activeVideoTag != null;

        public void PlayPresentation(ActionReport report, string actionName, Action onDone,
            bool holdBeforeBlocking = false)
        {
            _pendingBlockingSteps.Clear();
            _pendingImmediateDialogues.Clear();
            foreach (var step in report.BlockingStorySteps)
                _pendingBlockingSteps.Enqueue(step);
            _completionReport = report;
            _completionActionName = actionName;
            _completionDone = onDone;
            _holdBeforeBlocking = holdBeforeBlocking;
            _allowOutcomeHoldAfterBlocking = true;

            // 「冷静与伤势」讲的是不顺利会怎样，所以挂在第一次坏结果上，不挂在开局。
            if (report.Type == ActionType.Roll && report.Outcome == RollOutcome.Fail)
                TutorialDirector.RequestOnFailedRoll();

            _presentationPlayer.Play(report, actionName, () =>
            {
                // 换场时先让玩家读完结果，再放结局剧情；同场景动作沿用剧情期间挂结果的节奏。
                AddLightResidueIfNeeded(report, actionName);
                if (_holdBeforeBlocking && _cardResidues.TryGetValue(actionName, out var residue))
                {
                    residue.FuseStartedAt = Time.unscaledTime;
                    residue.FuseSeconds = OutcomeHoldSeconds(report);
                    _holdingResidue = residue;
                    _holdingReport = report;
                    _holdingDone = null;
                    _holdingBeforeBlocking = true;
                    return;
                }
                AdvanceToBlockingPresentationOrFinish();
            });
        }

        /// <summary>
        /// 回合转换里的一批（对方回应、回合末收尾）。它不是玩家按下的一手，所以不走
        /// <see cref="PlayPresentation"/> 开头那段"执行中…"进度。
        /// <paramref name="land"/> 在拍的中段被调（采纳这一批的快照）；批里排在后面的阻塞步骤
        /// （散场告示、倒下）在落地之后播；全部播完调 <paramref name="done"/>。
        /// </summary>
        public void PlayRoundBatch(ActionReport report, PresentationSnapshot landed, Action land, Action done)
        {
            _pendingBlockingSteps.Clear();
            _pendingImmediateDialogues.Clear();
            foreach (var step in report.BlockingStorySteps)
                _pendingBlockingSteps.Enqueue(step);
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = done;

            _actingNodeNames.Clear();
            foreach (var host in HostsOfChangedClocks(_gameManager.DisplayedSnapshot, landed))
                _actingNodeNames.Add(host);

            var speech = new List<DialogueLine>();
            _beatNarrations.Clear();
            _beatNarrationIds.Clear();
            _beatNarrationIds.AddRange(report.NarrationIds);
            foreach (var sequence in report.Banter)
            {
                // 旁白（"毡帽从侧面撞进来"）是拳头落下那一刻的字幕，等落地再放；
                // 人说的话是这一拍的开场，立刻从他卡上冒出来。
                if (sequence.Lines.Count > 0 && sequence.Lines[0].Speaker == DialogueBubbleDrawer.NarratorSpeaker)
                {
                    _beatNarrations.Add(sequence);
                    continue;
                }
                foreach (var line in sequence.Lines)
                {
                    speech.Add(line);
                    if (!_actingNodeNames.Contains(line.Speaker))
                        _actingNodeNames.Add(line.Speaker);
                }
            }
            bool spoken = speech.Count > 0
                || report.Effects.Any(effect => effect.Kind == ActionEffectKind.Composure || effect.Kind == ActionEffectKind.Injury);
            float now = Time.unscaledTime;
            float dwell = speech.Count > 0 ? _banterPlayer.ShowBeatLines(speech) : 0f;
            _activeBeat = true;
            _activeBeatUntil = now + Mathf.Max(spoken ? SpokenBeatSeconds : SilentBeatSeconds, dwell);
            _beatLandAt = now + (spoken ? SpokenBeatLandAt : SilentBeatLandAt);
            _beatLand = land;
            _actingSince = now;
            _actingUntil = _activeBeatUntil + ActingFadeSeconds;
        }

        // 两份快照按卡名对齐，同名卡上同标签的钟值不同 → 这张卡在动。
        private static IEnumerable<string> HostsOfChangedClocks(PresentationSnapshot before, PresentationSnapshot after)
        {
            var old = new Dictionary<string, GameNode>(StringComparer.Ordinal);
            foreach (var node in Walk(before.RootNode))
                old[node.Name] = node;
            foreach (var node in Walk(after.RootNode))
            {
                if (node.Clocks.Count == 0 || !old.TryGetValue(node.Name, out var was))
                    continue;
                foreach (var clock in node.Clocks)
                {
                    var prior = was.Clocks.FirstOrDefault(c => c.Label == clock.Label);
                    if (prior != null && prior.Current != clock.Current)
                    {
                        yield return node.Name;
                        break;
                    }
                }
            }
        }

        private static IEnumerable<GameNode> Walk(GameNode? node)
        {
            if (node == null) yield break;
            yield return node;
            foreach (var child in node.Children)
                foreach (var n in Walk(child))
                    yield return n;
        }

        // 拍的中段：快照落地。钟和冷静条的脉冲、飘字都从这一帧起；旁白字幕也在这时出来。
        private void LandActiveBeat()
        {
            var land = _beatLand;
            _beatLand = null;
            land?.Invoke();
            ReleaseNarrations(_beatNarrationIds);
            foreach (var sequence in _beatNarrations)
                _banterPlayer.Enqueue(sequence);
            _beatNarrations.Clear();
            _beatNarrationIds.Clear();
        }

        /// <summary>回合转换整段结束：金边收掉，别拖进玩家自己的回合。</summary>
        public void EndRoundTransitionPresentation()
        {
            _actingNodeNames.Clear();
            _actingUntil = 0f;
            _beatLand = null;
            _beatNarrations.Clear();
            _beatNarrationIds.Clear();
        }

        public void PlayBlockingPresentation(IReadOnlyList<BlockingStoryStep> steps, Action onDone)
        {
            if (steps.Count == 0)
            {
                onDone();
                return;
            }
            _pendingBlockingSteps.Clear();
            foreach (var step in steps)
                _pendingBlockingSteps.Enqueue(step);
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = onDone;
            _allowOutcomeHoldAfterBlocking = true;
            AdvanceToBlockingPresentationOrFinish();
        }

        // 按 Scheme 调用顺序逐个播放阻塞剧情步骤;每个步骤完成后回调本方法推进下一个。
        private void AdvanceToBlockingPresentationOrFinish()
        {
            if (_pendingBlockingSteps.Count > 0)
            {
                var step = _pendingBlockingSteps.Dequeue();
                switch (step.Kind)
                {
                    case BlockingStoryStepKind.Spotlight:
                        _activeActionSpotlight = step.Spotlight;
                        return;
                    case BlockingStoryStepKind.Dialogue:
                        _banterPlayer.Suspend();   // 对话聚焦,杂音让位
                        // 动作内先尝试锚定动作前画面；普通对话失败时由绘制层报警并降级为场外卡，
                        // 显式 remote 则直接允许场外说话人且不报警。
                        StoryStageDrawer.BeginConversation();
                        _conversationPlayer.Start(step.Dialogue!, () =>
                        {
                            _banterPlayer.Resume();
                            AdvanceToBlockingPresentationOrFinish();
                        });
                        return;
                    case BlockingStoryStepKind.Stage:
                        _banterPlayer.Suspend();
                        _storyStagePlayer.Start(step.Stage!, () =>
                        {
                            _banterPlayer.Resume();
                            AdvanceToBlockingPresentationOrFinish();
                        });
                        return;
                    case BlockingStoryStepKind.Video:
                        PlayVideoStep(step.VideoTag);
                        return;
                    case BlockingStoryStepKind.Motion:
                        PlayMotionStep(step);
                        return;
                    case BlockingStoryStepKind.EnterPlace:
                        _gameManager.EnterForcedPlace(step.PlaceName);
                        // 位置切换本身没有确认按钮；下一张倒下卡立刻在诊所画面上接管输入。
                        AdvanceToBlockingPresentationOrFinish();
                        return;
                    case BlockingStoryStepKind.AutoAction:
                        _activeAutoAction = step;
                        _activeAutoActionStartedAt = Time.unscaledTime;
                        _activeAutoActionPreludeUntil = float.PositiveInfinity;
                        _activeAutoActionCaptureStartedAt = -1f;
                        _activeAutoActionPreludeStarted = false;
                        _activeAutoActionResolved = false;
                        return;
                }
            }

            var report = _completionReport;
            var actionName = _completionActionName;
            var done = _completionDone;
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = null;

            bool allowOutcomeHold = _allowOutcomeHoldAfterBlocking;
            _allowOutcomeHoldAfterBlocking = true;
            if (allowOutcomeHold && report != null && _cardResidues.TryGetValue(actionName, out var residue))
            {
                // 这一手按下去就开始黑的（睡觉）不挂——黑幕底下没人看得见。
                if (SSNoirGameManager.ShouldDipEarly(report))
                    _cardResidues.Remove(actionName);
                else
                {
                    residue.FuseStartedAt = Time.unscaledTime;
                    residue.FuseSeconds = OutcomeHoldSeconds(report);
                    if (AdoptWouldEraseOutcome(report))
                    {
                        _holdingResidue = residue;
                        _holdingReport = report;
                        _holdingDone = done;
                        return;
                    }
                }
            }

            FinishCompletion(report, done);
        }

        // 采纳快照会不会把结果从屏幕上抹掉：翻页要黑幕；换了场景或阶段根，旧卡整层都没了。
        private bool AdoptWouldEraseOutcome(ActionReport report)
        {
            if (report.TurnEnded)
                return true;
            string shownRoot = _gameManager.DisplayedSnapshot.RootNode?.Name ?? string.Empty;
            string nextRoot = _gameManager.SceneManager.LatestSnapshot.RootNode?.Name ?? string.Empty;
            return !string.Equals(shownRoot, nextRoot, StringComparison.Ordinal);
        }

        // 停住的那条路走到头（烧完或点了一下）：结果条不跨过黑幕/换场，采纳快照。
        private void FinishHold()
        {
            var report = _holdingReport;
            var done = _holdingDone;
            bool beforeBlocking = _holdingBeforeBlocking;
            _holdingResidue = null;
            _holdingReport = null;
            _holdingDone = null;
            _holdingBeforeBlocking = false;
            _cardResidues.Clear();
            if (beforeBlocking)
            {
                _allowOutcomeHoldAfterBlocking = false;
                AdvanceToBlockingPresentationOrFinish();
            }
            else
                FinishCompletion(report, done);
        }

        private static float OutcomeHoldSeconds(ActionReport report)
        {
            float seconds = OutcomeHoldBaseSeconds + report.Effects.Count * OutcomeHoldPerEffectSeconds;
            if (report.Type == ActionType.Roll)
                seconds += OutcomeHoldRollExtraSeconds;
            return Mathf.Min(seconds, OutcomeHoldMaxSeconds);
        }

        // ESC：停住的先放行；没停住就把挂着的结果条都收了。
        public bool TrySkipOutcomeHold()
        {
            if (_holdingResidue != null)
            {
                FinishHold();
                return true;
            }
            if (_cardResidues.Count == 0)
                return false;
            _cardResidues.Clear();
            return true;
        }

        private void FinishCompletion(ActionReport? report, Action? done)
        {
            done?.Invoke();   // adopt 最新快照
            if (report != null)
            {
                ReleaseNarrations(report.NarrationIds);
                // banter 在 adopt 之后释放,锚定动作后的新快照(避免提前剧透)
                foreach (var sequence in report.Banter)
                    _banterPlayer.Enqueue(sequence);
            }
        }

        /// <summary>
        /// 剧本里的 (play-video! "tag") 落到这里：tag 认场景里同名的 CutsceneSequence，
        /// 找到就播那场过场，播完（或被 ESC 跳过）再推进下一个阻塞步骤。
        ///
        /// 找不到不算错，退回原来的定时占位就行——剧本先行、镜头后补是常态，不该因为镜头还
        /// 没配就把剧情卡死。但要出声，否则配错名字会表现成"过场莫名其妙没播"。
        /// </summary>
        private void PlayVideoStep(string? tag)
        {
            var sequence = CutsceneSequence.Find(tag ?? string.Empty);
            if (sequence != null)
            {
                _activeVideoTag = tag;
                _videoOwnedByCutscene = true;
                _gameManager.Cutscene.Play(sequence, () =>
                {
                    _activeVideoTag = null;
                    _videoOwnedByCutscene = false;
                    AdvanceToBlockingPresentationOrFinish();
                });
                return;
            }

            Debug.LogWarning(
                $"[SSNoir] play-video! 的 tag '{tag}' 在场景里找不到对应的 CutsceneSequence，"
                + "这一步按占位时长跳过。");
            _activeVideoTag = tag;
            _animationTimer = AnimationPlaceholderSeconds;
        }

        private const float AnimationPlaceholderSeconds = 0.8f;

        /// <summary>
        /// 剧本里的 (play-motion! 地点/道具 状态 机位) 落到这里：找到明确的地点实例和 Camera_<机位>_VCam，
        /// 交给过场播放器走影幕 + 运镜，开演时播过渡、播完收场，再推进下一个阻塞步骤。
        /// 道具或显式机位找不到都是内容契约错误，立即中断，不能让关键演出静默消失。
        /// </summary>
        private void PlayMotionStep(BlockingStoryStep step)
        {
            var motion = PropMotion.Find(step.MotionProp, out string prop);
            if (motion == null)
                throw new InvalidOperationException(
                    $"[SSNoir] play-motion! 的道具 '{step.MotionProp}' 场上不存在；检查地点名、clips.json 与发布产物。");
            Cinemachine.CinemachineVirtualCamera? camera = null;
            if (!string.IsNullOrEmpty(step.MotionCamera))
            {
                string wanted = "Camera_" + step.MotionCamera + "_VCam";
                foreach (var vcam in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>(true))
                {
                    if (vcam.name == wanted)
                    {
                        camera = vcam;
                        break;
                    }
                }
                if (camera == null)
                    throw new InvalidOperationException(
                        $"[SSNoir] play-motion! 显式要求的机位 '{wanted}' 场上不存在。");
            }
            string state = step.MotionState;
            _gameManager.Cutscene.PlayLive(
                camera,
                () => motion.PlayTransition(prop, state),
                () => !motion.IsTransitioning(prop),
                AdvanceToBlockingPresentationOrFinish);
        }

        private void ReleaseNarrations(System.Collections.Generic.List<string> ids)
        {
            foreach (var id in ids)
                _gameManager.GameState.NarrationCenter.Play(id);
        }

        private void ShowNarration(string id)
        {
            _narrationPlayer.Play(id);
        }

        private void ConfirmActionSpotlight()
        {
            _activeActionSpotlight = null;
            AdvanceToBlockingPresentationOrFinish();
        }

        public void AcknowledgePresentationRoll()
        {
            _presentationPlayer.OnRollAcknowledged();
        }

        public void ShowNotification(string message)
        {
            _gameManager.GameState.NotificationCenter.Push(message, NotificationKind.Info);
        }

        public bool IsAnimationPlaying => _animator.IsPlaying || _presentationPlayer.IsPlaying
            || _activeAutoAction != null
            || _activeBeat
            || _activeActionSpotlight != null
            || _conversationPlayer.IsActive || _storyStagePlayer.IsActive || _activeVideoTag != null;
        public bool IsAnimationReadyToAcknowledge => _animator != null && _animator.IsReadyToAcknowledge();
        public bool IsInputLocked => _inputLocked || _activeAutoAction != null
            || _activeActionSpotlight != null
            || _gameManager.GameState.SpotlightCenter.HasSpotlight || _conversationPlayer.IsActive || _storyStagePlayer.IsActive
            || _activeVideoTag != null || _gameManager.Cutscene.IsActive || _gameManager.Title.IsActive
            || _gameManager.StageController.IsTransitioning || _gameManager.StageController.TurnDipActive
            || _gameManager.IsStateTainted;

        // 与对白舞台上的左键点击共用同一套推进语义：打字中先显示全文，否则进入下一句。
        // 由 SSNoirGameManager 的全局 ESC 输入调用，避免 ESC 在对白期间落入返回导航逻辑。
        public bool TryAdvanceConversation()
        {
            if (_storyStagePlayer.IsActive)
            {
                _storyStagePlayer.Advance();
                return true;
            }
            if (!_conversationPlayer.IsActive)
                return false;

            if (StoryStageDrawer.IsCurrentLineFullyRevealed)
            {
                _conversationPlayer.Advance();
                _warnedAboutCurrentDialogueRemoteFallback = false;
            }
            else
            {
                StoryStageDrawer.CompleteCurrentLine();
            }

            return true;
        }

        // 动作内 Spotlight 与全局 Spotlight 都是“确认后继续”的阻塞层。
        public bool TryAdvanceSpotlight()
        {
            if (_activeActionSpotlight != null)
            {
                ConfirmActionSpotlight();
                return true;
            }

            if (!_gameManager.GameState.SpotlightCenter.HasSpotlight)
                return false;

            _gameManager.GameState.SpotlightCenter.Dismiss();
            return true;
        }

        // 普通界面面板不参与剧情推进；ESC 只关闭当前最上层面板。
        public bool TryCloseUiPanel()
        {
            if (_isGrowthPanelOpen)
            {
                _isGrowthPanelOpen = false;
                return true;
            }

            if (DebugPanelDrawer.IsOpen)
            {
                DebugPanelDrawer.Close();
                return true;
            }

            if (DossierPanelDrawer.IsOpen)
            {
                DossierPanelDrawer.Close();
                return true;
            }

            if (SettingsPanelDrawer.IsOpen)
            {
                SettingsPanelDrawer.Close();
                return true;
            }

            if (HelpPanelDrawer.IsOpen)
            {
                HelpPanelDrawer.Close();
                return true;
            }

            return false;
        }

        // Whether the pointer was over any interactive UI in the last OnGUI pass.
        public bool PointerOverUI { get; private set; }

        private bool _inputLocked = false;
        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
        }

        // Called on scene load/reset to close all overlay panels.
        public void ResetUiState()
        {
            _isGrowthPanelOpen = false;
            _inputLocked = false;
            _gridScrollOffset = 0f;
            _gridScrollStack.Clear();
            _lastNavigationDepth = 0;
            _cardResidues.Clear();
            _holdingResidue = null;
            _holdingReport = null;
            _holdingDone = null;
            _holdBeforeBlocking = false;
            _holdingBeforeBlocking = false;
            _allowOutcomeHoldAfterBlocking = true;
            _pendingBlockingSteps.Clear();
            _activeAutoAction = null;
            _activeBeat = false;
            EndRoundTransitionPresentation();
            _activeActionSpotlight = null;
            _activeVideoTag = null;
            _animationTimer = 0f;
            _videoOwnedByCutscene = false;
            _banterPlayer.Reset();
            _conversationPlayer.Reset();
            _storyStagePlayer.Reset();
            StoryStageDrawer.BeginConversation();
            _warnedAboutCurrentDialogueRemoteFallback = false;
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = null;
            DebugPanelDrawer.Reset();
            SettingsPanelDrawer.Reset();
            HelpPanelDrawer.Reset();
            TutorialDirector.Reset();
        }

        // 一手开始时的保险：结果条本该在采纳快照时就清光了。
        public void ClearCardResidues()
        {
            _cardResidues.Clear();
        }

        private void Update()
        {
            _storyStagePlayer.Update();
            _gameManager.GameState.NotificationCenter.Update(Time.deltaTime);
            _presentationPlayer.Update(Time.deltaTime);
            _banterPlayer.Update(Time.deltaTime);
            if (_holdingResidue != null && _holdingResidue.FuseBurntOut)
                FinishHold();
            // 烧完的结果条自己走。
            foreach (var key in _cardResidues.Where(pair => pair.Value.FuseBurntOut).Select(pair => pair.Key).ToList())
                _cardResidues.Remove(key);
            if (_activeAutoAction != null)
            {
                float now = Time.unscaledTime;
                if (!_activeAutoActionPreludeStarted
                    && now - _activeAutoActionStartedAt >= AutoActionAppearSeconds)
                {
                    _activeAutoActionPreludeStarted = true;
                    var prelude = _activeAutoAction.AutoActionPrelude;
                    float dwell = prelude == null ? 0f : _banterPlayer.ShowBeatLines(prelude.Lines);
                    _activeAutoActionPreludeUntil = now + dwell;
                }
                if (_activeAutoActionPreludeStarted
                    && _activeAutoActionCaptureStartedAt < 0f
                    && now >= _activeAutoActionPreludeUntil)
                {
                    _activeAutoActionCaptureStartedAt = now;
                }

                float elapsed = AutoActionCaptureElapsed(now);
                if (!_activeAutoActionResolved && elapsed >= AutoActionResolveAt)
                {
                    // 状态已由引擎提交；执行条走完时只揭示缓存结果，表现层不再执行规则。
                    _activeAutoActionResolved = true;
                    var node = _activeAutoAction.AutoActionNode!;
                    var report = _activeAutoAction.ResolvedReport
                        ?? throw new InvalidOperationException("AutoAction 缺少引擎提交的结果。");
                    _cardResidues[node.Name] = new CardPresentationResidue
                    {
                        HostNodeName = node.Name,
                        Effects = new List<ActionEffectRecord>(report.Effects),
                        FuseStartedAt = Time.unscaledTime,
                        FuseSeconds = AutoActionResultHoldSeconds,
                    };
                    ReleaseNarrations(report.NarrationIds);
                    foreach (var sequence in report.Banter)
                        _banterPlayer.Enqueue(sequence);
                }
                else if (_activeAutoActionResolved && elapsed >= AutoActionDuration)
                {
                    _cardResidues.Remove(_activeAutoAction.AutoActionNode!.Name);
                    _activeAutoAction = null;
                    AdvanceToBlockingPresentationOrFinish();
                }
            }

            if (_activeBeat && _beatLand != null && Time.unscaledTime >= _beatLandAt)
                LandActiveBeat();
            if (_activeBeat && Time.unscaledTime >= _activeBeatUntil)
            {
                _activeBeat = false;
                if (_beatLand != null)
                    LandActiveBeat();
                AdvanceToBlockingPresentationOrFinish();
            }

            // 命名动画占位:到点后推进下一个阻塞剧情步骤。
            // 归过场管的那种不在这里收尾，它自己播完会回调。
            if (_activeVideoTag != null && !_videoOwnedByCutscene)
            {
                _animationTimer -= Time.deltaTime;
                if (_animationTimer <= 0f)
                {
                    _activeVideoTag = null;
                    AdvanceToBlockingPresentationOrFinish();
                }
            }

            _animator.Update();

            // 过场首帧截图。截的是纯世界，界面开着照样干净，所以随时按都行，也不挑时机。
            if (Input.GetKeyDown(KeyCode.F9))
            {
                CinematicCapture.Capture(this, 1080);
            }
            else if (Input.GetKeyDown(KeyCode.F10))
            {
                CinematicCapture.Capture(this, 2160);
            }

            // Right-click to cancel selection
            if (Input.GetMouseButtonDown(1)
                && !IsAnimationPlaying
                && !_isGrowthPanelOpen
                && !DebugPanelDrawer.IsOpen
                && !_inputLocked
                // 影幕期间没有任何 UI 反馈，右键把选中的资源清掉，玩家出来才发现，等于静默丢状态。
                && !_gameManager.Cutscene.IsActive)
            {
                if (_gameManager.SelectedResource != null)
                {
                    _gameManager.ClearSelectedResource();
                }
            }
        }

        private void OnGUI()
        {
            // MouseDrag 必须放行：触控上的「滑动 vs 轻点」判定和卡片网格的拖拽滚动都靠它。
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown
                && Event.current.type != EventType.MouseUp && Event.current.type != EventType.MouseDrag
                && Event.current.type != EventType.ScrollWheel
                && Event.current.type != EventType.Layout)
                return;

            // 图层透明度每帧归位。它是全局状态，Begin / End 中间断一次就会一直半透明下去。
            IMGUIStyles.ResetLayer();

            // 把整个 IMGUI 放进按设备物理尺寸推导出来的虚拟画布里。
            UIScale.Apply();

            // 指针状态要在 GUI.matrix 生效之后再读——mousePosition 是按当前矩阵换算的，
            // 在 Apply 之前取到的是物理坐标，和后面所有比较的坐标系对不上。
            IMGUIInteractionContext.NotePointerEvent(Event.current);

            // 触控设备上「悬停」只在手指按住时存在；手指抬起后 mousePosition 停在最后触点，
            // 不把这件事说清楚的话，最后碰过的控件会一直亮着。
            IMGUIInteractionContext.SetHoverAvailable(
                UIScale.HasHoverPointer || Input.GetMouseButton(0) || Input.GetMouseButton(1));

            // Initialize styles if needed
            IMGUIStyles.Init(_gameManager.ChineseFont, _gameManager.SemiboldFont);

            // ── 标题菜单 ──
            // 和影幕同一个位置、同一个道理：菜单在的时候世界照常渲染（那就是背景），但一个
            // 游戏控件都不出现。PointerOverUI 置真，镜头也就跟着不接受拖拽了。
            if (_gameManager.Title.IsActive)
            {
                _gameManager.Title.Draw(Event.current.mousePosition);
                PointerOverUI = true;
                IMGUIInteractionContext.FinishPointerEvent(Event.current);
                return;
            }

            // ── 影幕 ──
            // 排在最前面，画完就走：过场期间一个游戏控件都不该出现，玩家看到的只有实时世界
            // （或盖在上面的视频）加黑边。世界本身照常渲染，所以黑边压下来的过程中背景是活的，
            // 视频首帧接上去才不会有缝。
            if (_gameManager.Cutscene.IsActive)
            {
                IMGUIInteractionContext.ConsumeCurrentPress();
                _gameManager.Cutscene.Draw();
                PointerOverUI = true;
                IMGUIInteractionContext.FinishPointerEvent(Event.current);
                return;
            }

            if (_gameManager.DisplayedSnapshot.Failure.IsFailed)
            {
                DrawFailureOverlay(Event.current.mousePosition);
                PointerOverUI = true;
                IMGUIInteractionContext.FinishPointerEvent(Event.current);
                return;
            }
            SyncNavigationScrollState();

            Vector2 mouse = Event.current.mousePosition;
            bool mouseDown = Event.current.type == EventType.MouseDown && Event.current.button == 0;
            _windowStack.BeginFrame(mouse, mouseDown);

            // Reset the pointer-over-UI accumulator; widgets set it via CanHover
            // during this pass, and we persist the result at the end of OnGUI.
            IMGUIInteractionContext.ResetPointerOverUi();
            _firstActionCardRect = null;
            _drawnCardRects.Clear();
            TopHudLayout topHud = TopHudLayout.Create();
            _topHud = topHud;

            if (DossierPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                var (_, dossierPanelRect) = DossierPanelDrawer.GetRects(topHud);
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.DossierPanel,
                    Bounds = dossierPanelRect,
                    Layer = IMGUIWindowLayer.Panel,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (SettingsPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                var (_, settingsPanelRect) = SettingsPanelDrawer.GetRects(topHud);
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.SettingsPanel,
                    Bounds = settingsPanelRect,
                    Layer = IMGUIWindowLayer.Panel,
                    // 现在是跟成长面板同一套居中纸卡模态，也要跟成长面板一样挡住整屏——
                    // 否则背后世界还能被点到（相机拖拽/卡片点击穿透）。
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (HelpPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                var (_, helpPanelRect) = HelpPanelDrawer.GetRects(topHud);
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.HelpPanel,
                    Bounds = helpPanelRect,
                    Layer = IMGUIWindowLayer.Panel,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            // 教程是模态：它一出现，底下的世界和面板都不该被点到——正在教你怎么点，
            // 你却先点到了别处，是最坏的一种混乱。
            if (TutorialDirector.IsOpen)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.Tutorial,
                    Bounds = new Rect(0, 0, UIScale.VW, UIScale.VH),
                    Layer = IMGUIWindowLayer.Modal,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (DebugPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                Rect debugPanelRect = DebugPanelDrawer.GetPanelRect(topHud);
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.DebugPanel,
                    Bounds = debugPanelRect,
                    Layer = IMGUIWindowLayer.Panel,
                    BlockMode = IMGUIBlockMode.Bounds,
                    CloseOnClickedOutside = false,
                });
            }

            if (_isGrowthPanelOpen)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.GrowthPanel,
                    Bounds = GrowthPanelDrawer.GetPanelRect(),
                    Layer = IMGUIWindowLayer.Panel,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (_activeActionSpotlight != null || _gameManager.GameState.SpotlightCenter.HasSpotlight)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.Spotlight,
                    Bounds = new Rect(0, 0, UIScale.VW, UIScale.VH),
                    Layer = IMGUIWindowLayer.Modal,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            // 阻塞对话:全屏 blocker 锁住下层(表现上不画遮罩),点击由顶层 overlay 消费来推进。
            if (_conversationPlayer.IsActive || _storyStagePlayer.IsActive)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.Conversation,
                    Bounds = new Rect(0, 0, UIScale.VW, UIScale.VH),
                    Layer = IMGUIWindowLayer.Modal,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            bool baseLocked = IsInputLocked || IsAnimationPlaying;
            if (baseLocked)
                IMGUIInteractionContext.ConsumeCurrentPress();
            _windowStack.Update();

            // 阻塞对白期间世界的界面整个收起来：标签、动作卡、卷宗、顶栏都不画——舞台上只留城市和人。
            // 锚点表故意不清：对白是阻塞的，导航和相机都冻着，上一帧登记的「谁在场」在整段对白里
            // 都作数，舞台照旧用它判断说话人在不在场。
            bool worldUiHidden = _conversationPlayer.IsActive || _storyStagePlayer.IsActive;
            var panelUi = _windowStack.MakeContext(IMGUIWindowLayer.Panel, baseLocked);
            if (!worldUiHidden)
            {
            _dialogueAnchors.Clear();

            // 你正身处其中的那些容器（导航栈）也是在场的人/地点，只是没有卡——进入「夜莺」
            // 之后她本人依然能开口。锚在面包屑上（气泡会翻到它下方），先登记，这样同名的
            // 真实卡片随后覆盖它，卡片优先。
            foreach (var container in _gameManager.NavigationStack)
                _dialogueAnchors.RegisterNode(container.Name, topHud.Breadcrumb);

            var worldUi = _windowStack.MakeContext(IMGUIWindowLayer.World, baseLocked);

            // 对方回合：世界先变冷，界面再画上去——所以它在所有 GUI 之前、只压在 3D 世界上。
            DrawOpponentTurnTint();

            // 半身像只负责把人物钉在场景里；可读、可点的卡片与附件必须永远压在它上面。
            HandPanelDrawer.DrawPortraits(_gameManager);

            // ── Node Cards (3D projected) ──
            DrawCards(worldUi);
            bool pointerOnFixedHud = topHud.Bar.Contains(worldUi.Mouse)
                || topHud.FunctionPlate(!_gameManager.DisplayedSnapshot.IsInEncounter).Contains(worldUi.Mouse)
                || _pinStripRect.Contains(worldUi.Mouse)
                || _sceneBandRect.Contains(worldUi.Mouse);
            var cardOverlayUi = pointerOnFixedHud ? worldUi.Occluded() : worldUi;
            foreach (var kv in _cardCenters)
                _dialogueAnchors.RegisterNode(kv.Key, new Rect(kv.Value.x - 60f, kv.Value.y - 80f, 120f, 160f));

            // ── Bottom Panel ──
            // 附件的视觉层在物品栏之后；点击消费提前做，避免附件盖住物品栏后
            // 视觉层在前、交互层却误点到底下的物品。
            // 淡入途中的卡片不接点击，它的附件同理。
            if (_cardLayerReveal >= 0.999f && !pointerOnFixedHud)
                HandleAttachmentTaps(worldUi);
            HandPanelDrawer.Draw(_gameManager, worldUi, _dialogueAnchors, PendingAutoActionReservedDice());
            AddAttachmentsForHandCards();
            float attachmentRestore = IMGUIStyles.BeginLayer(_cardLayerReveal);
            DrawCardAttachmentOverlays(cardOverlayUi);
            IMGUIStyles.EndLayer(attachmentRestore);
            DrawGhostsAndFloatingAttachments(cardOverlayUi);
            // 固定 UI 最后盖住世界卡、引线及其附件。
            if (!_gameManager.DisplayedSnapshot.IsInEncounter)
                DossierPanelDrawer.DrawPinStrip(_gameManager, topHud.FunctionPlate(true).yMax + 8f);
            if (_sceneBandNotes.Count > 0)
                AnnotationDrawer.DrawSceneBand(_sceneBandNotes, topHud.ContentTop, _pinStripRect);
            bool showDossierPlate = !_gameManager.DisplayedSnapshot.IsInEncounter;
            Rect functionPlate = topHud.FunctionPlate(showDossierPlate);
            IMGUIStyles.SetColor(IMGUIStyles.FunctionSlotBg);
            GUI.DrawTexture(functionPlate, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            float sepLeft = (showDossierPlate ? topHud.DossierToggle : topHud.GrowthToggle).xMin;
            float sepX = (topHud.Day.xMax + sepLeft) * 0.5f;
            IMGUIStyles.DrawLine(
                new Vector2(sepX, functionPlate.y + 8f),
                new Vector2(sepX, functionPlate.yMax - 8f),
                IMGUIStyles.FunctionSlotLine, 1f);
            NavigationDrawer.Draw(_gameManager, worldUi, topHud);
            // 停住的那条路：输入是锁着的，结果条接不到 WasTapped，任意一次抬手都算「看完了」。
            if (_holdingResidue != null && Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                Event.current.Use();
                FinishHold();
            }

            // ── Growth / Team Toggle Button ──
            DrawGrowthToggleButton(worldUi, topHud.GrowthToggle);

            // ── 顶栏开关与面板 ──
            // 五个面板（成长/卷宗/设置/帮助/调试）都不能整段跳过绘制——之前那样做会让按钮凭空消失，
            // 很突兀。改成始终画出来，被更高优先级面板占屏时只是传一个强制锁定的 ui 上下文，
            // 按钮可见但点不动。优先级：成长 > 卷宗 > 设置 > 帮助 > 调试；打开谁就顺手关掉下面优先级的
            // 面板，避免两个居中纸卡模态叠在一起抢点击。调试的开关直接在顶栏，面板挂在它自己下面。
            var lockedPanelUi = _windowStack.MakeContext(IMGUIWindowLayer.Panel, true);

            // 卷宗：城里的事，交锋里不给入口。
            if (!_gameManager.DisplayedSnapshot.IsInEncounter)
            {
                bool dossierWasOpen = DossierPanelDrawer.IsOpen;
                DossierPanelDrawer.Draw(_gameManager, _isGrowthPanelOpen ? lockedPanelUi : panelUi, topHud);
                if (!dossierWasOpen && DossierPanelDrawer.IsOpen)
                {
                    SettingsPanelDrawer.Close();
                    DebugPanelDrawer.Close();
                }
            }
            else
            {
                // 交锋开场时把它收起来：否则打完回到城里，面板会自己弹回来。
                DossierPanelDrawer.Close();
            }

            var settingsUi = (_isGrowthPanelOpen || DossierPanelDrawer.IsOpen) ? lockedPanelUi : panelUi;
            bool settingsWasOpen = SettingsPanelDrawer.IsOpen;
            SettingsPanelDrawer.Draw(settingsUi, topHud);
            if (!settingsWasOpen && SettingsPanelDrawer.IsOpen)
                DebugPanelDrawer.Close();

            var helpUi = (_isGrowthPanelOpen || DossierPanelDrawer.IsOpen || SettingsPanelDrawer.IsOpen)
                ? lockedPanelUi : panelUi;
            bool helpWasOpen = HelpPanelDrawer.IsOpen;
            HelpPanelDrawer.Draw(helpUi, topHud);
            if (!helpWasOpen && HelpPanelDrawer.IsOpen)
                DebugPanelDrawer.Close();

            var debugUi = (_isGrowthPanelOpen || SettingsPanelDrawer.IsOpen || DossierPanelDrawer.IsOpen
                    || HelpPanelDrawer.IsOpen)
                ? lockedPanelUi : panelUi;
            DebugPanelDrawer.Draw(_gameManager, debugUi, topHud);

            // ── Overlays ──
            // 通知摞在左上、标注带之下（上一帧的带子位置，带子很少动，差一帧无妨）。
            float notifTop = _sceneBandRect.height > 0f
                ? _sceneBandRect.yMax + 8f
                : UIScale.SafeArea.y + 66f;
            OverlayDrawer.DrawNotifications(_gameManager.GameState.NotificationCenter, notifTop);
            OverlayDrawer.DrawCursorFollower(_gameManager);
            }
            DrawActingCardHighlights();
            DrawPresentationOverlay();
            DrawAutoActionDiceFlights();
            DrawBanterOverlay();
            DrawSpotlightOverlay();
            DrawVideoOverlay();
            DrawNarrationOverlay();

            // ── Growth Panel ──
            if (_isGrowthPanelOpen && !worldUiHidden)
            {
                var growthInteraction = GrowthPanelDrawer.Draw(_gameManager, panelUi);
                if (growthInteraction.ShouldClose)
                {
                    _isGrowthPanelOpen = false;
                }
            }

            // ── 教程提示 ──
            // 观察放在所有卡片画完之后：这一帧到底有没有动作卡、第一张在哪儿，这时才知道。
            // 演出/对白/其它模态在跑的时候不弹——教程要指的东西这会儿正被盖着。
            bool tutorialQuiet = !IsPresentationActive && !_isGrowthPanelOpen
                && !DossierPanelDrawer.IsOpen && !SettingsPanelDrawer.IsOpen && !HelpPanelDrawer.IsOpen;
            if (tutorialQuiet)
                TutorialDirector.Observe(_gameManager, _firstActionCardRect.HasValue);
            if (TutorialDirector.IsOpen && tutorialQuiet)
                TutorialDirector.Draw(
                    _gameManager,
                    _windowStack.MakeContext(IMGUIWindowLayer.Modal),
                    _firstActionCardRect);

            // ── Animation Modal ──
            _animator.DrawModal();

            // 阻塞对白必须盖住成长面板和其余可交互 UI；底层同时由 IsInputLocked 显式禁用。
            DrawConversationOverlay();

            // ── Stage Transition Flash ──
            var stageCtrl = _gameManager.StageController;
            if (stageCtrl.FadeAlpha > 0f)
            {
                var oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, stageCtrl.FadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }

            // Persist whether the pointer is over any UI this pass, so the camera
            // manager (which polls raw Input in Update, one step ahead of OnGUI)
            // can skip starting a drag that begins on the UI.
            PointerOverUI = baseLocked
                || IMGUIInteractionContext.PointerOverUi
                || _windowStack.IsPointerOverBlocker();

            // Resolve a resource drag on release. rawType (not type) so this still
            // fires when a slot already consumed the MouseUp to place the token.
            if (Event.current.rawType == EventType.MouseUp && _gameManager.IsDraggingResource)
            {
                _gameManager.EndResourceDrag(Event.current.mousePosition);
            }

            IMGUIInteractionContext.FinishPointerEvent(Event.current);
        }

        // 对方回合的世界：镜头从你身上移开。整层压一层冷蓝，上下两道再压深一点——
        // 城市的白线暗下去，唯一亮着的是在动的那张卡。没有任何字：谁的回合由颜色说。
        // 进出各 0.35 秒，从按下结束回合起，到新骰落定止（强制行动属于你的回合，画面已经回来）。
        private float _opponentTint;

        private void DrawOpponentTurnTint()
        {
            float target = _gameManager.IsOpponentActing ? 1f : 0f;
            _opponentTint = Mathf.MoveTowards(_opponentTint, target, Time.unscaledDeltaTime / 0.35f);
            if (_opponentTint <= 0.001f)
                return;
            var full = new Rect(0f, 0f, UIScale.VW, UIScale.VH);
            var oldColor = GUI.color;
            // 冷蓝：比 Ink 更靠青一点，和暖金的卡边拉开。
            GUI.color = new Color(0.03f, 0.06f, 0.12f, 0.42f * _opponentTint);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            float band = UIScale.VH * 0.18f;
            GUI.color = new Color(0.01f, 0.02f, 0.05f, 0.30f * _opponentTint);
            GUI.DrawTexture(new Rect(0f, 0f, UIScale.VW, band), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, UIScale.VH - band, UIScale.VW, band), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        // 对方回合里正在动的那张卡：一圈金边从卡外一点亮起，这一拍结束后慢慢褪掉。
        // 它回答的是"现在是谁在动"——气泡说的话、卡下挂的后果、卡上跳的钟都归到这一圈里。
        private void DrawActingCardHighlights()
        {
            if (_actingNodeNames.Count == 0 || Time.unscaledTime >= _actingUntil)
                return;
            float now = Time.unscaledTime;
            float rise = Mathf.Clamp01((now - _actingSince) / 0.18f);
            float fade = Mathf.Clamp01((_actingUntil - now) / ActingFadeSeconds);
            float alpha = Mathf.Min(rise, fade);
            if (alpha <= 0.01f)
                return;
            var gold = new Color(IMGUIStyles.Gold.r, IMGUIStyles.Gold.g, IMGUIStyles.Gold.b, 0.92f * alpha);
            var glow = new Color(IMGUIStyles.Gold.r, IMGUIStyles.Gold.g, IMGUIStyles.Gold.b, 0.28f * alpha);
            foreach (var name in _actingNodeNames)
            {
                if (!_drawnCardRects.TryGetValue(name, out var rect))
                    continue;
                float pad = 4f + 2f * (1f - rise);
                var outer = UIScale.PixelSnap(new Rect(rect.x - pad, rect.y - pad, rect.width + pad * 2f, rect.height + pad * 2f));
                IMGUIStyles.DrawOutline(new Rect(outer.x - 2f, outer.y - 2f, outer.width + 4f, outer.height + 4f), 3f, glow);
                IMGUIStyles.DrawOutline(outer, 1.5f, gold);
            }
        }

        // 失败卡。这一屏是一章的句号，也是玩家在这一局里读到的最后一段字——
        // 它的排版不能比路边一张动作卡还随便。
        //
        // 上一版三个具体的毛病，都不是"审美问题"，是排版规则用错了：
        //   · 正文居中。中文段落居中排，最后一行常常只剩一个句号孤零零挂在中间。
        //     叙述性段落一律左对齐——居中只留给标题这种一两行的东西。
        //   · 正文框写死 56px 高，而三行 18px 的字要 80 上下。文字从框里溢出去，
        //     和下面的分隔线挤在一起。现在高度由 CalcHeight 量出来，卡的高度跟着走，
        //     以后写多长的失败文案都不会再撞。
        //   · 「退出游戏」直接借了 StatusLabel，那是个 MiddleLeft、给暗底用的样式，
        //     落在纸白卡上就是一行左对齐的浅灰字。按钮的样式在这儿现配，不借共享静态的。
        private void DrawFailureOverlay(Vector2 mouse)
        {
            var failure = _gameManager.DisplayedSnapshot.Failure;
            var screen = new Rect(0f, 0f, UIScale.VW, UIScale.VH);
            GUI.DrawTexture(screen, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                new Color(0.02f, 0.03f, 0.05f, 0.92f), 0f, 0f);

            const float cardWidth = 560f;
            const float padX = 52f;
            const float padTop = 46f;
            const float padBottom = 40f;
            const float ruleWidth = 44f;     // 标题上方那一道红杠：印章的语气，不是装饰线
            const float ruleHeight = 3f;
            const float gapRuleToTitle = 18f;
            const float gapTitleToBody = 20f;
            const float gapBodyToDivider = 28f;
            const float gapDividerToButtons = 24f;
            const float gapBetweenButtons = 10f;
            const float primaryH = 44f;
            const float secondaryH = 38f;
            float contentW = cardWidth - padX * 2f;

            var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontSize = IMGUIStyles.FontSize(32),
                normal = { textColor = IMGUIStyles.SealRed },
            };
            // 正文自带一份样式，不从 StatusLabel 派生：那一份是 MiddleLeft + 暗底配色，
            // 每次都要改两个字段才能用在纸上，改漏一个就是上一版那行浅灰字。
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(17),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                richText = false,
                normal = { textColor = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.88f) },
            };

            float titleH = Mathf.Max(
                titleStyle.CalcHeight(new GUIContent(failure.Title), contentW),
                IMGUIStyles.FontSize(32) * 1.3f);
            float bodyH = string.IsNullOrEmpty(failure.Description)
                ? 0f
                : bodyStyle.CalcHeight(new GUIContent(failure.Description), contentW);

            // 卡有多高由内容说了算。写死高度是上一版文字溢出来的根源。
            float cardHeight = padTop + ruleHeight + gapRuleToTitle + titleH
                             + (bodyH > 0f ? gapTitleToBody + bodyH : 0f)
                             + gapBodyToDivider + 1f + gapDividerToButtons
                             + primaryH + gapBetweenButtons + secondaryH + padBottom;

            var card = UIScale.PixelSnap(UIScale.CenteredModal(cardWidth, cardHeight));
            IMGUIStyles.DrawShadow(card, new Vector2(6f, 7f), 0.55f);
            GUI.DrawTexture(card, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                IMGUIStyles.Paper, 0f, 0f);
            IMGUIStyles.DrawOutline(card, 1f, IMGUIStyles.PaperInk);

            float x = card.x + padX;
            float y = card.y + padTop;

            GUI.color = IMGUIStyles.SealRed;
            GUI.DrawTexture(new Rect(x, y, ruleWidth, ruleHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += ruleHeight + gapRuleToTitle;

            IMGUIStyles.DrawLabel(new Rect(x, y, contentW, titleH), failure.Title, titleStyle);
            y += titleH;

            if (bodyH > 0f)
            {
                y += gapTitleToBody;
                IMGUIStyles.DrawLabel(new Rect(x, y, contentW, bodyH), failure.Description, bodyStyle);
                y += bodyH;
            }

            y += gapBodyToDivider;
            IMGUIStyles.DrawLine(new Vector2(x, y), new Vector2(card.xMax - padX, y),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.30f), 1f);
            y += 1f + gapDividerToButtons;

            // 两个按钮的字都在这儿现配成居中的深色：DrawTechnicalButton 会拿描边色去写字，
            // 所以主次之分靠 alpha（0.85 / 0.55），不靠换一个给暗底用的样式。
            var buttonLabel = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(16),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
            };
            IMGUIStyles.ApplyStrongFont(buttonLabel);

            var ui = new IMGUIInteractionContext(mouse, isLocked: false);
            var restart = new Rect(x, y, contentW, primaryH);
            var quit = new Rect(x, y + primaryH + gapBetweenButtons, contentW, secondaryH);

            // 两个各自判断。上一版写成 if / else if——重新开始被点中的那一帧，
            // 退出按钮整个不画，屏幕上会缺一块。
            bool restartClicked = IMGUIButton.Draw(restart, "重新开始", ui,
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.85f),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.10f),
                buttonLabel);
            bool quitClicked = IMGUIButton.Draw(quit, "退出游戏", ui,
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.55f),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.06f),
                buttonLabel);

            if (restartClicked)
                _gameManager.RestartGame();
            else if (quitClicked)
                Application.Quit();
        }

        private void DrawCards(IMGUIInteractionContext ui)
        {
            _sceneBandNotes.Clear();
            var cam = Camera.main;
            if (cam == null) return;
            _cardAttachmentOverlays.Clear();
            var actionCamera = _gameManager.DisplayedFocusCamera != null
                ? _gameManager.DisplayedFocusCamera.GetComponent<SSNoirVirtualCameraConfig>()
                : null;
            CameraDragMode actionMode = actionCamera != null ? actionCamera.dragMode : CameraDragMode.Static;
            EnsureActionLayoutContext(actionCamera, actionMode);

            var nodes = new List<GameNode>(_gameManager.VisibleNodes);
            if (_activeAutoAction?.AutoActionNode != null)
                nodes.Add(_activeAutoAction.AutoActionNode);
            var focusedName = _gameManager.FocusedNodeName;
            // Container 牌子贴着锚点上浮；Pan 动作卡在锚点附近寻找空位，
            // 其他视角的动作卡退到左右栏。见 CardGutterLayout。
            // 世界投射层跟着换镜一起显形：新那一镜露出多少，这一层就画多浓（见下面
            // BeginLayer 那一段）。低动画换镜之外恒为 1，什么都不变。
            float viewReveal = _gameManager.CameraManager.ReducedViewReveal;
            // 附件（判定条、结果条）画在别处、隔着几个绘制阶段，浓度只能这样带过去。
            _cardLayerReveal = viewReveal;

            // Split nodes into two groups: those with world anchors and those without
            var initialProjected = new List<(GameNode node, string anchorKey, Vector3 screenPos, Vector3 labelScreenPos, float distance, int order)>();
            var gridNodes = new List<GameNode>();
            var sceneNotes = _sceneBandNotes;
            var importantBeacons = new List<ImportantNodeBeacon>();
            var restBlockers = _gameManager.DisplayedSnapshot.RestBlockers;
            var protectedAnchors = new List<Vector2>();

            // 可见节点的锚点也是场景视觉重心。即使聚焦筛掉了它的卡片，
            // Pan 的动作面板也不应盖住该锚点附近的人物或道具。
            if (actionMode == CameraDragMode.Pan)
            {
                foreach (var visibleNode in _gameManager.VisibleNodes)
                {
                    var visibleAnchor = _gameManager.ResolveAnchor(visibleNode);
                    if (visibleAnchor == null) continue;
                    var projected = cam.WorldToScreenPoint(visibleAnchor.transform.position);
                    if (projected.z <= 0f) continue;
                    var point = UIScale.WorldPointToVirtual(projected);
                    if (!UIScale.SafeArea.Contains(point)) continue;
                    if (!protectedAnchors.Any(existing => (existing - point).sqrMagnitude < 1f))
                        protectedAnchors.Add(point);
                    // 锚点常在人物脚边；把已有的对白头部高度也纳入保护，
                    // 否则卡片避开脚边却仍会遮住人物上半身。
                    var head = cam.WorldToScreenPoint(
                        visibleAnchor.transform.position + Vector3.up * BanterHeadHeight);
                    if (head.z > 0f)
                    {
                        var headPoint = UIScale.WorldPointToVirtual(head);
                        if (UIScale.SafeArea.Contains(headPoint)
                            && !protectedAnchors.Any(existing => (existing - headPoint).sqrMagnitude < 1f))
                            protectedAnchors.Add(headPoint);
                    }
                }
            }

            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                bool isAutoAction = _activeAutoAction?.AutoActionNode == node;
                if (!isAutoAction && !string.IsNullOrEmpty(focusedName) && node.Name != focusedName)
                    continue;

                var containedBlocker = RestBlockerPresentation.FindContained(node, restBlockers);
                var anchor = _gameManager.ResolveAnchor(node);
                if (anchor != null)
                {
                    // 地点牌与锚定标注只要求锚点在镜头前方：锚点出了画面，牌子
                    // 仍可能露出一角，玩家可以拖镜头把它带回来。动作卡保留视口筛选。
                    var viewPos = cam.WorldToViewportPoint(anchor.transform.position);
                    float padding = 0.05f;
                    bool worldFixed = AnnotationDrawer.IsAnnotation(node) || node.IsContainer;
                    bool cachedAction = !worldFixed && HasActionCenter(node.Name, actionMode);
                    bool waitForLanding = !worldFixed && !cachedAction
                        && _gameManager.CameraManager.IsFocusTravelInFlight;
                    bool inCameraSight = cachedAction || (viewPos.z >= 0
                                      && (worldFixed || (viewPos.x >= -padding && viewPos.x <= (1f + padding)
                                          && viewPos.y >= -padding && viewPos.y <= (1f + padding))));

                    if (inCameraSight)
                    {
                        var screenPos = cam.WorldToScreenPoint(anchor.transform.position);
                        // screenPos is actual screen pixels; order 是内容里的声明序号，
                        // 只在锚点投影几乎重合时用来定先后（见 SolveProjectedStacks）。
                        // 地点牌的悬浮高度属于世界空间；固定屏幕像素会随镜头俯仰改变
                        // 它看起来离建筑的高度。
                        var labelScreenPos = cam.WorldToScreenPoint(anchor.transform.position + Vector3.up * LocationLabelWorldHeight);
                        // 新动作卡只在目标机位停稳后排布一次。运镜途中用瞬时投影
                        // 反复求解，会让它在场景上左右跳；已有落点的卡仍可稳定显示。
                        if (!waitForLanding)
                            initialProjected.Add((node, anchor.ResolvedNodeName, screenPos, labelScreenPos, screenPos.z, nodeIndex));

                        // banter 气泡的落点：锚点是地面上的一个点，气泡要从人形的头上冒出来，
                        // 所以抬一个人头的高度再投影。锚点按分区而不按人形摆，这个高度是
                        // 「站在这儿的人大概多高」，不是某个模型量出来的数。
                        var headScreen = cam.WorldToScreenPoint(anchor.transform.position + Vector3.up * BanterHeadHeight);
                        _dialogueAnchors.RegisterWorldPoint(node.Name, UIScale.WorldPointToVirtual(headScreen));
                    }
                    else if (containedBlocker != null)
                    {
                        importantBeacons.Add(new ImportantNodeBeacon(
                            node.Name,
                            containedBlocker.Reason,
                            ViewportDirection(viewPos)));
                    }
                    // 只有动作卡在锚点离开视口时隐藏；固定世界标识继续按投影绘制。
                }
                else if (AnnotationDrawer.IsAnnotation(node))
                {
                    // 标注没有锚点就升到画面上方的场景标注带：它不指某一处，指的是整个画面。
                    // 它永远不进网格——网格是"还没给锚点的卡"的临时住处，而一条标注宁可
                    // 去说这一整场在发生什么，也不该排在待办里假装自己可以点。
                    sceneNotes.Add(node);
                }
                else
                {
                    gridNodes.Add(node); // No anchor, draw in grid
                }
            }

            // ── 钉住条 ──
            // 地图上常驻的唯一一条故事信息：玩家钉的那几条线的线名和那一句「现在」。
            //
            // 它挂在右上角功能槽下面，和功能槽同一右缘，位置**恒定**：玩家每天要看的就是
            // 这几句，眼睛该知道往哪儿落，只往右上角落一次。所以让位的是标注带——
            // 标注是"这一场是什么样子"，一天里换好几副面孔，本来就该跟着环境走。
            // 交锋里不画：那时候没有别的线可想。
            _pinStripRect = _gameManager.DisplayedSnapshot.IsInEncounter
                ? new Rect(0f, 0f, 0f, 0f)
                : DossierPanelDrawer.MeasurePinStrip(_gameManager,
                    _topHud.FunctionPlate(true).yMax + 8f);

            // 带子先量后画：卡片的可用区间（含网格视口）要按它实际占了多高往下让。
            // 场景标注与任务条同属固定 UI，量完后在世界卡之上绘制。
            //
            // 带子从左上起排，绕着右边的钉住条走（见 LayoutSceneBand）。这个 Rect 只记带子
            // 自己占的那一块；钉住条另有 _pinStripRect，网格按列绕它，不在这儿并成一整行。
            float bandTop = _topHud.ContentTop;
            _sceneBandRect = sceneNotes.Count == 0
                ? new Rect(0f, 0f, 0f, 0f)
                : new Rect(
                    UIScale.SafeArea.xMin, bandTop, UIScale.SafeArea.width,
                    AnnotationDrawer.MeasureSceneBandHeight(sceneNotes, bandTop, _pinStripRect));

            // 结果条不参与排布：宿主卡还在旧快照里就挂在它下面（见 AddAttachmentForNode），
            // 宿主此刻没画出来（换了场景、镜头走了、宿主是随身卡）就由 DrawFloatingAttachments 兜底。

            // Clean up old cached centers that are no longer visible to avoid memory leaks
            var visibleKeys = new HashSet<string>(initialProjected.Select(x => x.node.Name));
            var keysToRemove = _cardCenters.Keys.Where(k => !visibleKeys.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _cardCenters.Remove(key);
            }

            // Create layout records for projected cards
            var layouts = new List<ProjectedCardLayout>();
            foreach (var item in initialProjected)
            {
                // Convert from actual screen pixels (Y-up) to virtual GUI coords (Y-down).
                var virtualAnchor = UIScale.WorldPointToVirtual(item.screenPos);
                float anchorX = virtualAnchor.x;
                float anchorY = virtualAnchor.y;

                bool isAnnotation = AnnotationDrawer.IsAnnotation(item.node);
                bool isLocation = !isAnnotation && item.node.IsContainer;
                bool focused = !isAnnotation && isFocused(item.node.Name);

                // 地点卡按地名实际需要多宽收敛（见 PreferredLocationWidth）：它现在是一行
                // 「小符号 + 地名」的牌子，不需要偏方的体量。
                // 标注与动作卡同宽，高度完全由内容定——它没有卡框，也就没有「最小卡高」
                // 这回事：内容只有一行就只占一行。
                float cardWidth = isAnnotation
                    ? AnnotationDrawer.AnchoredWidth
                    : (focused
                        ? FocusedCardWidth
                        : (isLocation ? ContainerNodeDrawer.PreferredLocationWidth(item.node) : ActionCardDesignWidth));
                float contentHeight = isAnnotation
                    ? AnnotationDrawer.MeasureHeight(item.node, cardWidth)
                    : CardDrawer.MeasureCardHeight(
                        item.node,
                        CardDrawer.Classify(item.node, anchored: true),
                        cardWidth,
                        _gameManager.DisplayedSnapshot.Actors);
                // 聚焦卡放大是「凑近看」：只加宽，不再撑高——高度永远由内容说了算。
                float cardHeight = contentHeight;
                bool isAction = !isAnnotation && !item.node.IsContainer;
                Vector2 targetCenter;
                if (isAction)
                {
                    targetCenter = TryGetActionCenter(item.node.Name, cam, actionMode, out var settledCenter)
                        ? settledCenter
                        : actionMode == CameraDragMode.Pan
                            ? new Vector2(anchorX, anchorY - cardHeight / 2f - 16f)
                            : CardGutterLayout.TargetCenter(item.anchorKey, new Vector2(anchorX, anchorY), cardWidth, ActionCardDesignWidth);
                }
                else if (isAnnotation)
                {
                    // 标注也跟随抬高后的世界点，文字挂在竖线旁；左右方向由
                    // 锚点决定，不因屏幕边界临时翻边。
                    var labelPoint = UIScale.WorldPointToVirtual(item.labelScreenPos);
                    float offset = AnnotationDrawer.StemGap + cardWidth / 2f;
                    targetCenter = new Vector2(labelPoint.x + offset, labelPoint.y - cardHeight / 2f);
                }
                else
                {
                    // 牌子下沿落在锚点上方固定的世界高度，再投影到屏幕。
                    // 因而镜头俯仰只改变透视，不会改变牌子在场景中的悬浮高度。
                    var labelPoint = UIScale.WorldPointToVirtual(item.labelScreenPos);
                    targetCenter = new Vector2(labelPoint.x, labelPoint.y - cardHeight / 2f);
                }

                layouts.Add(new ProjectedCardLayout(
                    item.node,
                    new Vector2(anchorX, anchorY),
                    item.distance,
                    item.order,
                    targetCenter,
                    targetCenter,
                    cardWidth,
                    cardHeight,
                    isAnnotation));
            }

            // 地点牌和锚定标注直接映射到世界点。动作卡只在首次出现、内容尺寸或
            // 视角改变时避让一次；Pan 在锚点周围避开卡片与视觉重心后固定世界点，
            // Orbit 仍固定左右栏的屏幕位置。
            // 地点牌和锚定标注不参与重排，但它们占据的区域会约束动作卡的首次落点。
            var keepOut = BuildCardKeepOut(layouts.Where(layout => layout.IsWorldFixed));
            var actionLayouts = layouts.Where(layout => !layout.IsWorldFixed).ToList();
            if (StackContentChanged(actionLayouts))
            {
                if (actionMode == CameraDragMode.Pan)
                    SolvePanActions(actionLayouts, protectedAnchors, keepOut);
                else
                    SolveProjectedStacks(actionLayouts, keepOut);
                // 换镜途中只可能有已有落点的卡；新卡在落镜后才进入排布。
                if (!_gameManager.CameraManager.IsFocusTravelInFlight)
                    RememberActionCenters(actionLayouts, cam, actionMode);
            }
            foreach (var layout in layouts)
            {
                layout.CurrentCenter = layout.IsWorldFixed
                    ? layout.TargetCenter
                    : (TryGetActionCenter(layout.Key, cam, actionMode, out var settledCenter)
                        ? settledCenter : layout.SolvedCenter);
                _cardCenters[layout.Key] = layout.CurrentCenter;
            }

            // Draw projected cards (sorted by distance, far to near)。List.Sort 不稳定，
            // 同深度时再落回排序键，免得压叠关系和命中归属逐帧抖。
            // 活动卡（见 IsActiveCard）压在深度之上：手上正在办的那件事不该被一张更近的卡遮住。
            layouts.Sort((a, b) =>
            {
                int byActive = IsActiveLayout(a).CompareTo(IsActiveLayout(b));
                if (byActive != 0) return byActive;
                int byDistance = b.Distance.CompareTo(a.Distance);
                return byDistance != 0 ? byDistance : CompareStackRank(a, b);
            });

            // ── 世界投射层：跟着换镜一起显形 ──
            //
            // 这一层是钉在世界上的东西（卡片、引线、标注、边缘信标），它属于**镜头看到的
            // 那一镜**。低动画换镜时相机先一步落到新机位，屏幕上却还盖着旧画面的冻帧：
            // 照常画，卡片就会扎在一张旧画面上先跳一下，再滑向新排布。所以这一层的浓度
            // 直接跟着"新那一镜露出了多少"走——世界怎么显形，它就怎么显形。位置在上面
            // 已经直接落到解算位，全程不动，只是从无到有地浮出来。
            //
            // 淡入途中不接点击：那时它还是个影子，点一张看不清的卡不该算数。
            bool ghostLayer = viewReveal < 0.999f;
            float cardLayerRestore = IMGUIStyles.BeginLayer(viewReveal);

            foreach (var layout in layouts)
            {
                if (layout.IsAnnotation)
                    continue;   // 标注没有判定条也没有结果条，它不会长出附件
                AddAttachmentForNode(layout.Node, layout.Rect, spacious: true);
            }

            // 命中归属：卡片是画家算法——投射卡按远→近画，网格卡再盖在最上面。而 IMGUI
            // 的点击是「先处理者 Event.Use() 吃掉」，顺序正好相反：不先解析一遍，重叠区域
            // 就会被画在最底下的那张卡抢走点击。附件不是卡的碰撞体：它可以压在邻卡上，
            // 但压住的那一块不该让底下的卡接到点击。
            ProjectedCardLayout? hitOwner = null;
            bool attachmentConsumesPointer = AttachmentConsumesPointer(ui.Mouse);
            // 顶栏里真正要点的控件（返回键 / 功能组）各自有 Hard 区；整条 Bar 不算，
            // 面包屑那段只是读的，压在下面的地点牌照样要点。
            if (!ghostLayer && !attachmentConsumesPointer
                && !keepOut.IsUiBlocked(new Rect(ui.Mouse.x, ui.Mouse.y, 1f, 1f))
                && !MouseOverGridCard(gridNodes, ui.Mouse))
            {
                for (int i = 0; i < layouts.Count; i++)
                {
                    // 标注永远不可点，也就永远不该抢走指针归属。
                    if (layouts[i].IsAnnotation)
                        continue;
                    if (layouts[i].Rect.Contains(ui.Mouse))
                        hitOwner = layouts[i];
                }
            }

            // 标注在最底下：它是印在图纸上的字，卡片压在它上面——卡才是可操作层。
            // 它自己画那一笔引线（线走到头长出字，见 AnnotationDrawer），所以不进下面
            // 那一层；卡片引线接的是矩形边中点，两种线的语言本来就不同。
            foreach (var layout in layouts)
            {
                if (layout.IsAnnotation)
                    AnnotationDrawer.DrawAnchored(layout.Rect, layout.Node, layout.AnchorPos);
            }

            // 引线自成一层，在所有卡片之前一次画完。让每张卡各画各的线，后画的卡就会把
            // 先画的线拦腰切断——理由与实现都在 CardLeaderLineDrawer。
            _tethers.Clear();
            foreach (var layout in layouts)
            {
                if (layout.IsAnnotation || layout.Distance <= 0f)
                    continue;
                _tethers.Add(new CardLeaderLineDrawer.Tether(
                    layout.AnchorPos, layout.Rect, TetherWeight(layout, hitOwner)));
            }
            CardLeaderLineDrawer.Draw(_tethers);

            foreach (var layout in layouts)
            {
                if (layout.IsAnnotation)
                    continue;

                var cardUi = !ghostLayer && ReferenceEquals(layout, hitOwner) ? ui : ui.Occluded();
                DrawNodeCard(layout.Node, layout.Rect, cardUi);
            }

            IMGUIStyles.EndLayer(cardLayerRestore);

            // 网格卡最后绘制，因此视觉上压在世界投射卡之上。它钉在屏幕上、不在那一镜里，
            // 所以不跟着换镜显形——上面那层已经收掉了。
            if (gridNodes.Count > 0)
            {
                bool overHud = keepOut.IsUiBlocked(new Rect(ui.Mouse.x, ui.Mouse.y, 1f, 1f));
                DrawCardsGrid(gridNodes, overHud ? ui.Occluded() : ui);
            }

            // 信标说的是"这一镜外头还有东西"，所以它和投射层同进同退。
            cardLayerRestore = IMGUIStyles.BeginLayer(viewReveal);
            bool beaconOverHud = keepOut.IsUiBlocked(new Rect(ui.Mouse.x, ui.Mouse.y, 1f, 1f));
            string? beaconTarget = ImportantNodeBeaconDrawer.Draw(
                importantBeacons, beaconOverHud ? ui.Occluded() : ui, _topHud.ContentTop);
            IMGUIStyles.EndLayer(cardLayerRestore);
            if (beaconTarget != null && !ghostLayer)
                _gameManager.CameraManager.NavigateToNode(beaconTarget);

        }

        /// <summary>
        /// 这条引线该画多重。指针停在哪张卡上、或哪张卡是当前焦点，就只让那一条亮起来，
        /// 其余压暗——比把所有线都加粗有效得多，平时也不多一分噪音。
        /// 已结算的残影没有"指着谁"可言，一律压暗。
        ///
        /// 手指没有悬停：`hitOwner` 在触控上是"上一次点在哪"，拿它做常驻高亮会让某张卡
        /// 在松手之后一直亮着。所以联动高亮只在有悬停指针时生效——这是输入能力的差别，
        /// 不改任何元素画在哪、画多大。
        /// </summary>
        private CardLeaderLineDrawer.Emphasis TetherWeight(
            ProjectedCardLayout layout, ProjectedCardLayout? hitOwner)
        {
            var pointed = UIScale.HasHoverPointer ? hitOwner : null;
            bool highlighted = ReferenceEquals(layout, pointed)
                || (layout.Node != null && isFocused(layout.Node.Name));
            if (highlighted)
                return CardLeaderLineDrawer.Emphasis.Highlighted;
            if (pointed != null)
                return CardLeaderLineDrawer.Emphasis.Muted;
            return CardLeaderLineDrawer.Emphasis.Normal;
        }

        private bool IsLocalRollOf(string nodeName)
            => _animator.IsPlaying
               && !_animator.UsesModal
               && string.Equals(_animator.ActionName, nodeName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 这张卡此刻是不是「手上正在办的那件事」：装了骰子 / 正在执行 / 判定动画 / 结果还挂着。
        /// 四个状态首尾相接（执行的同一帧就开始演出，演出落定的同一帧就留下结果），
        /// 所以从放进骰子到收起结果，答案一直是 true——它不是存下来的层级，是每帧按状态算的，
        /// 也就没有「掉回去」这回事。活动卡在同一层里最后画（压在邻卡之上），它的附件在附件层。
        /// 装填与结果都是互斥的（放骰子会清掉别的卡的槽和残影），一层里至多一张活动卡。
        /// </summary>
        private bool IsActiveCard(GameNode node)
            => ActionNodeDrawer.IsConsoleArmed(node, SlotsForNode(node), GetExecutionState(node.Name).IsExecuting)
               || IsLocalRollOf(node.Name)
               || _cardResidues.ContainsKey(node.Name);

        private bool IsActiveLayout(ProjectedCardLayout layout)
            => !layout.IsAnnotation && IsActiveCard(layout.Node);

        private void AddAttachmentForNode(GameNode node, Rect cardRect, bool spacious)
        {
            if (IsLocalRollOf(node.Name))
            {
                _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForLocalRoll(
                    cardRect, node.Name, _animator.CurrentReport!, _animator.Phase,
                    _animator.DisplayedDieValue, _animator.DisplayScale, spacious));
            }
            else if (_cardResidues.TryGetValue(node.Name, out var residue))
            {
                _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForResidue(cardRect, residue, spacious));
            }
            else
            {
                var slots = SlotsForNode(node);
                if (ActionNodeDrawer.IsConsoleArmed(node, slots, GetExecutionState(node.Name).IsExecuting))
                    _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForConsole(cardRect, node, slots!));
            }
        }

        private void DrawCardAttachmentOverlays(IMGUIInteractionContext ui)
        {
            for (int i = 0; i < _cardAttachmentOverlays.Count; i++)
            {
                var attachment = _cardAttachmentOverlays[i];
                if (attachment.ConsoleNode != null)
                {
                    var execution = GetExecutionState(attachment.HostNodeName);
                    if (ActionNodeDrawer.DrawConsoleAttachment(
                            attachment.CardRect, attachment.ConsoleNode, attachment.ConsoleSlots!, ui, _gameManager,
                            execution.IsExecuting, execution.Progress, execution.Text))
                        _gameManager.ExecuteNodeAction(attachment.ConsoleNode);
                    continue;
                }
                ActionNodeDrawer.DrawAttachmentOverlay(
                    attachment.CardRect, attachment.LocalRoll, attachment.LocalRollPhase,
                    attachment.LocalRollDieValue, attachment.LocalRollScale, attachment.Residue,
                    ui, attachment.Spacious, attachment.Above);
            }
        }

        private void HandleAttachmentTaps(IMGUIInteractionContext ui)
        {
            for (int i = 0; i < _cardAttachmentOverlays.Count; i++)
            {
                var attachment = _cardAttachmentOverlays[i];
                if (attachment.ConsoleNode != null)
                {
                    HandleConsoleTap(attachment, ui);
                    continue;
                }
                if (attachment.Residue != null)
                    TryDismissResidueByTap(attachment.Residue,
                        ActionNodeDrawer.ResidueAttachmentRect(attachment.CardRect, attachment.Residue, attachment.Spacious, attachment.Above), ui);
            }
        }

        // 结果条点一下就走。它不是按钮，只是「看完了」的一个手势。
        private void TryDismissResidueByTap(CardPresentationResidue residue, Rect rect, IMGUIInteractionContext ui)
        {
            ui.CanHover(rect);
            if (!ui.WasTapped(rect))
                return;
            Event.current.Use();
            _cardResidues.Remove(residue.HostNodeName);
        }

        // 随身卡（抽烟）和休息键画在底部手牌区，不走卡片层；它们的判定条和结果条在这里补挂，
        // 往上挂——往下就出屏了。
        private void AddAttachmentsForHandCards()
        {
            foreach (var pair in HandPanelDrawer.DrawnActionRects)
            {
                if (IsLocalRollOf(pair.Key))
                {
                    _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForLocalRoll(
                        pair.Value, pair.Key, _animator.CurrentReport!, _animator.Phase,
                        _animator.DisplayedDieValue, _animator.DisplayScale, spacious: false, above: true));
                }
                else if (_cardResidues.TryGetValue(pair.Key, out var residue))
                {
                    residue.AttachAbove = true;
                    _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForResidue(pair.Value, residue, spacious: false));
                }
            }
        }

        // 宿主这一帧没画出来的附件：
        //  · 宿主从快照里消失了、但结算那一帧它在屏幕上——画一张它结算后模样的残卡（钟已归零、
        //    次数已用完）替它站着，结果条挂在残卡下面。玩家看见的是「这一拳把它打没了」，
        //    而不是卡凭空不见。残卡不可操作，点一下连同结果条一起走。
        //  · 连位置都没有（换了场景镜头已经走了、宿主出镜）——挂在屏幕中央一张只写动作名的浮牌下。
        private void DrawGhostsAndFloatingAttachments(IMGUIInteractionContext ui)
        {
            var attached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attachment in _cardAttachmentOverlays)
                if (attachment.ConsoleNode == null)
                    attached.Add(attachment.HostNodeName);

            if (_animator.IsPlaying && !_animator.UsesModal
                && !string.IsNullOrEmpty(_animator.ActionName) && !attached.Contains(_animator.ActionName))
            {
                var host = ActionNodeDrawer.DrawFloatingHost(_animator.ActionName);
                ActionNodeDrawer.DrawAttachmentOverlay(host, _animator.CurrentReport!, _animator.Phase,
                    _animator.DisplayedDieValue, _animator.DisplayScale, null, ui, spacious: true);
                return;
            }

            foreach (var pair in _cardResidues.ToList())
            {
                if (attached.Contains(pair.Key))
                    continue;
                var residue = pair.Value;
                Rect host;
                bool spacious;
                // 停住那条路里旧快照还在：卡没画出来只能是镜头已经走了，残卡站在旧位置上没有意义。
                bool ghostAllowed = !ReferenceEquals(residue, _holdingResidue);
                if (ghostAllowed && residue.SourceNode != null && TryGhostRect(residue, out var ghostRect))
                {
                    float restore = IMGUIStyles.BeginLayer(0.8f);
                    CardDrawer.DrawCard(ghostRect, residue.SourceNode, CardDrawer.Classify(residue.SourceNode, anchored: residue.GhostAnchor != null),
                        isHovered: false, isFlipped: false, isFocused: false,
                        slotted: null, clocks: residue.SettledClocks ?? residue.SourceNode.Clocks, backText: string.Empty,
                        ui: ui.Occluded(), gameManager: _gameManager);
                    IMGUIStyles.EndLayer(restore);
                    TryDismissResidueByTap(residue, ghostRect, ui);
                    host = ghostRect;
                    spacious = residue.GhostAnchor != null;
                }
                else
                {
                    residue.AttachAbove = false;
                    host = ActionNodeDrawer.DrawFloatingHost(pair.Key);
                    spacious = true;
                }
                ActionNodeDrawer.DrawAttachmentOverlay(host, null, 0, 0, 1f, residue, ui, spacious, residue.AttachAbove);
                TryDismissResidueByTap(residue, ActionNodeDrawer.ResidueAttachmentRect(host, residue, spacious, residue.AttachAbove), ui);
            }
        }

        // 残卡站哪：有锚点就跟着锚点的投影走（镜头转了它也贴着原处），锚点在镜头后面
        // 或没有锚点就站在结算那一帧的位置。
        private static bool TryGhostRect(CardPresentationResidue residue, out Rect rect)
        {
            rect = default;
            if (residue.GhostRect == null)
                return false;
            rect = residue.GhostRect.Value;
            var anchor = residue.GhostAnchor;
            var cam = Camera.main;
            if (anchor == null || cam == null)
                return true;
            var screen = cam.WorldToScreenPoint(anchor.position);
            if (screen.z < 0f)
                return true;
            var virtualPos = UIScale.WorldPointToVirtual(screen);
            rect.position = new Vector2(virtualPos.x, virtualPos.y) + residue.GhostOffset;
            return true;
        }

        // 结果条挂着期间，钟按结算后的样子显示。
        private List<GameClock> ClocksForCard(GameNode node)
            => _cardResidues.TryGetValue(node.Name, out var residue) && residue.SettledClocks != null
                ? residue.SettledClocks
                : node.Clocks;

        // 操作台的执行钮：视觉在附件层（物品栏之后），点击在这里（物品栏之前）接，
        // 理由同结果残影——不能视觉盖着骰子、点下去却是骰子在响。
        private void HandleConsoleTap(in CardAttachmentOverlay attachment, IMGUIInteractionContext ui)
        {
            var node = attachment.ConsoleNode!;
            var slots = attachment.ConsoleSlots!;
            if (node.Disabled || GetExecutionState(node.Name).IsExecuting || slots.Any(sl => sl == null))
                return;
            var exeRect = ActionNodeDrawer.ConsoleExecuteRect(
                ConsoleRect(attachment));
            if (ui.IsLocked || !ui.WasTapped(exeRect))
                return;
            Event.current.Use();
            _gameManager.ExecuteNodeAction(node);
        }

        private Rect ConsoleRect(in CardAttachmentOverlay attachment)
            => ActionNodeDrawer.ConsoleAttachmentRect(
                attachment.CardRect, attachment.ConsoleNode!, attachment.ConsoleSlots!,
                _gameManager.DisplayedSnapshot.Actors);

        private bool AttachmentConsumesPointer(Vector2 pointer)
        {
            foreach (var attachment in _cardAttachmentOverlays)
            {
                if (attachment.ConsoleNode != null && ConsoleRect(attachment).Contains(pointer))
                    return true;
                if (attachment.Residue != null
                    && ActionNodeDrawer.ResidueAttachmentRect(attachment.CardRect, attachment.Residue, attachment.Spacious, attachment.Above).Contains(pointer))
                    return true;
            }
            return false;
        }

        private const float LocationLabelWorldHeight = 1f;
        private void EnsureActionLayoutContext(SSNoirVirtualCameraConfig? camera, CameraDragMode mode)
        {
            if (_hasActionLayoutContext && _actionLayoutCamera == camera
                && _actionLayoutMode == mode && _actionLayoutSafeArea.Equals(UIScale.SafeArea))
                return;

            _actionLayoutCamera = camera;
            _actionLayoutMode = mode;
            _actionLayoutSafeArea = UIScale.SafeArea;
            _hasActionLayoutContext = true;
            _panActionWorldCenters.Clear();
            _screenActionCenters.Clear();
            _stackFootprints.Clear();
        }

        private bool HasActionCenter(string key, CameraDragMode mode) => mode == CameraDragMode.Pan
            ? _panActionWorldCenters.ContainsKey(key)
            : _screenActionCenters.ContainsKey(key);

        private bool TryGetActionCenter(string key, Camera cam, CameraDragMode mode, out Vector2 center)
        {
            if (mode == CameraDragMode.Pan)
            {
                if (_panActionWorldCenters.TryGetValue(key, out var worldPoint))
                {
                    center = UIScale.WorldPointToVirtual(cam.WorldToScreenPoint(worldPoint));
                    return true;
                }
            }
            else if (_screenActionCenters.TryGetValue(key, out center))
            {
                return true;
            }
            center = default;
            return false;
        }

        // 只有成员或本体尺寸变化才重排；镜头运动不在条件里。
        private bool StackContentChanged(List<ProjectedCardLayout> layouts)
        {
            if (layouts.Count != _stackFootprints.Count)
                return true;
            foreach (var layout in layouts)
            {
                if (!_stackFootprints.TryGetValue(layout.Key, out var footprint)
                    || !Mathf.Approximately(footprint.x, layout.Width)
                    || !Mathf.Approximately(footprint.y, layout.Height))
                    return true;
            }
            return false;
        }

        private void RememberActionCenters(List<ProjectedCardLayout> layouts, Camera cam, CameraDragMode mode)
        {
            _panActionWorldCenters.Clear();
            _screenActionCenters.Clear();
            _stackFootprints.Clear();
            foreach (var layout in layouts)
            {
                if (mode == CameraDragMode.Pan)
                {
                    var screen = new Vector3(layout.SolvedCenter.x * UIScale.Scale,
                        Screen.height - layout.SolvedCenter.y * UIScale.Scale, layout.Distance);
                    _panActionWorldCenters[layout.Key] = cam.ScreenToWorldPoint(screen);
                }
                else
                    _screenActionCenters[layout.Key] = layout.SolvedCenter;
                _stackFootprints[layout.Key] = new Vector2(layout.Width, layout.Height);
            }
        }

        // 同一列里两张卡之间留出的呼吸缝。
        private const float CardStackGap = 12f;
        // 一列放不下、并排分成几摞时，两摞之间的横向缝。
        private const float CardLaneGap = 14f;
        // 两个锚点的投影差在这个范围内视作一样高/一样偏，先后交给下一级键。
        private const float AnchorTieBand = 6f;

        // Pan 只在卡片集合/尺寸或机位改变时求解。按稳定顺序逐张找离锚点最近的空位；
        // 卡片和固定 UI 是硬约束，视觉锚点是次一级约束，拥挤时才允许侵入其保护半径。
        private static void SolvePanActions(
            List<ProjectedCardLayout> layouts, List<Vector2> anchors, in CardKeepOut keepOut)
        {
            const float anchorRadius = 64f;
            const float gridStep = 12f;
            const float cardGap = 10f;
            var ranked = new List<ProjectedCardLayout>(layouts);
            ranked.Sort(CompareStackRank);
            var placed = new List<Rect>();
            Rect safe = UIScale.SafeArea;

            foreach (var card in ranked)
            {
                float minX = safe.xMin + card.Width / 2f + 6f;
                float maxX = safe.xMax - card.Width / 2f - 6f;
                float minY = safe.yMin + card.Height / 2f + 6f;
                float maxY = safe.yMax - card.Height / 2f - 6f;
                float bestScore = float.PositiveInfinity;
                Vector2 bestCenter = default;

                for (float x = minX; x <= maxX; x += gridStep)
                for (float y = minY; y <= maxY; y += gridStep)
                {
                    var center = new Vector2(x, y);
                    var rect = new Rect(x - card.Width / 2f, y - card.Height / 2f,
                        card.Width, card.Height);
                    if (keepOut.IsHardBlocked(rect)) continue;
                    var spaced = new Rect(rect.xMin - cardGap / 2f, rect.yMin - cardGap / 2f,
                        rect.width + cardGap, rect.height + cardGap);
                    if (placed.Any(other => spaced.Overlaps(other))) continue;

                    int coveredAnchors = 0;
                    foreach (var anchor in anchors)
                    {
                        float nearestX = Mathf.Clamp(anchor.x, rect.xMin, rect.xMax);
                        float nearestY = Mathf.Clamp(anchor.y, rect.yMin, rect.yMax);
                        if ((anchor - new Vector2(nearestX, nearestY)).sqrMagnitude < anchorRadius * anchorRadius)
                            coveredAnchors++;
                    }
                    // 肖像是软占位：有其他空位就让开，所有空位都紧张时仍可借用。
                    float score = coveredAnchors * 1000000f
                        + keepOut.SoftOverlapArea(rect) * 1000f
                        + (center - card.TargetCenter).sqrMagnitude;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCenter = center;
                    }
                }

                if (bestScore < float.PositiveInfinity)
                {
                    card.SolvedCenter = bestCenter;
                    placed.Add(new Rect(card.SolvedCenter.x - card.Width / 2f,
                        card.SolvedCenter.y - card.Height / 2f, card.Width, card.Height));
                }
                else
                {
                    // 局部搜索塞不下全部卡时，交还给现有的分列堆叠解算；
                    // 再整体移动拥挤的列，尽量保住锚点保护区。
                    SolveProjectedStacks(layouts, keepOut);
                    ImproveDensePanColumns(layouts, anchors, keepOut, anchorRadius, gridStep);
                    return;
                }
            }
        }

        // 分列回退不能丢掉 Pan 的锚点约束。整列平移保留原有卡片间距，
        // 逐列寻找锚点遮挡更少的位置；空间确实不足时保持可读的原布局。
        private static void ImproveDensePanColumns(
            List<ProjectedCardLayout> layouts, List<Vector2> anchors,
            in CardKeepOut keepOut, float anchorRadius, float gridStep)
        {
            var columns = layouts
                .GroupBy(card => Mathf.RoundToInt(card.SolvedCenter.x / 2f))
                .Select(group => group.ToList())
                .OrderByDescending(group => PanColumnAnchorOverlap(group, anchors, anchorRadius))
                .ToList();
            Rect safe = UIScale.SafeArea;

            foreach (var column in columns)
            {
                var members = new HashSet<ProjectedCardLayout>(column);
                var others = layouts.Where(card => !members.Contains(card)).Select(card =>
                    new Rect(card.SolvedCenter.x - card.Width / 2f,
                        card.SolvedCenter.y - card.Height / 2f, card.Width, card.Height)).ToList();
                Vector2 bestOffset = Vector2.zero;
                float bestScore = PanColumnScore(column, Vector2.zero, anchors, keepOut, anchorRadius);

                for (float dx = -safe.width; dx <= safe.width; dx += gridStep)
                for (float dy = -safe.height; dy <= safe.height; dy += gridStep)
                {
                    var offset = new Vector2(dx, dy);
                    bool fits = true;
                    foreach (var card in column)
                    {
                        var rect = new Rect(card.SolvedCenter.x + dx - card.Width / 2f,
                            card.SolvedCenter.y + dy - card.Height / 2f, card.Width, card.Height);
                        if (rect.xMin < safe.xMin + 6f || rect.xMax > safe.xMax - 6f
                            || rect.yMin < safe.yMin + 6f || rect.yMax > safe.yMax - 6f
                            || keepOut.IsHardBlocked(rect)
                            || others.Any(other => new Rect(rect.xMin - 5f, rect.yMin - 5f,
                                rect.width + 10f, rect.height + 10f).Overlaps(other)))
                        {
                            fits = false;
                            break;
                        }
                    }
                    if (!fits) continue;
                    float score = PanColumnScore(column, offset, anchors, keepOut, anchorRadius);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestOffset = offset;
                    }
                }

                foreach (var card in column)
                    card.SolvedCenter += bestOffset;
            }
        }

        private static int PanColumnAnchorOverlap(
            List<ProjectedCardLayout> column, List<Vector2> anchors, float anchorRadius)
        {
            int count = 0;
            foreach (var card in column)
            {
                var rect = new Rect(card.SolvedCenter.x - card.Width / 2f,
                    card.SolvedCenter.y - card.Height / 2f, card.Width, card.Height);
                count += CoveredPanAnchors(rect, anchors, anchorRadius);
            }
            return count;
        }

        private static float PanColumnScore(
            List<ProjectedCardLayout> column, Vector2 offset, List<Vector2> anchors,
            in CardKeepOut keepOut, float anchorRadius)
        {
            float score = 0f;
            foreach (var card in column)
            {
                var center = card.SolvedCenter + offset;
                var rect = new Rect(center.x - card.Width / 2f,
                    center.y - card.Height / 2f, card.Width, card.Height);
                score += CoveredPanAnchors(rect, anchors, anchorRadius) * 1000000f
                    + keepOut.SoftOverlapArea(rect) * 1000f
                    + (center - card.TargetCenter).sqrMagnitude;
            }
            return score;
        }

        private static int CoveredPanAnchors(Rect rect, List<Vector2> anchors, float radius)
        {
            int covered = 0;
            foreach (var anchor in anchors)
            {
                float nearestX = Mathf.Clamp(anchor.x, rect.xMin, rect.xMax);
                float nearestY = Mathf.Clamp(anchor.y, rect.yMin, rect.yMax);
                if ((anchor - new Vector2(nearestX, nearestY)).sqrMagnitude < radius * radius)
                    covered++;
            }
            return covered;
        }

        // 世界投射卡的排布：先定出唯一的上下顺序，再在每一列里做一次一维消重叠，
        // 得到唯一的 SolvedCenter。
        //
        // 不能像原来那样「看谁此刻偏上就把谁往上推」——那让顺序取决于卡片这一瞬间飘到
        // 了哪里。同一栋楼的几个锚点投影得很近，卡片开局几乎重合，谁上谁下就由浮点噪声
        // 决定：同样的镜头推进，这次埃迪在上，下次它在下。
        //
        // 排序键是锚点自己的投影位置：屏幕上锚点在上的，卡也在上。这既是玩家眼里唯一
        // 说得通的顺序（楼上的房间卡如果排到楼下那张的下面，引线就是拧着的），也已经
        // 足够稳定——它只跟镜头有关，同一个机位每次都算出同一份排布。锚点投影几乎重合
        // 时才落到声明顺序和名字，避免在毫厘之差上分先后。
        //
        // 注：曾经试过用锚点的世界高度当主键，图上更「客观」，但读起来是反的——家在
        // 世界里比码头高，屏幕上却是码头的锚点更靠上，卡片于是和它指的东西上下颠倒。
        private static void SolveProjectedStacks(List<ProjectedCardLayout> layouts, in CardKeepOut keepOut)
        {
            if (layouts.Count == 0)
                return;

            var ranked = new List<ProjectedCardLayout>(layouts);
            ranked.Sort(CompareStackRank);

            // 只有横向真压在一起的卡才需要争上下；彼此错开的卡各排各的，
            // 否则屏幕两头毫不相干的两张卡会被硬拉进同一列。
            foreach (var column in GroupIntoColumns(ranked))
                SolveColumn(column, keepOut);
        }

        private static int CompareStackRank(ProjectedCardLayout a, ProjectedCardLayout b)
        {
            // 卡片先占位，标注填剩下的。卡是要点的，位置稳到能形成肌肉记忆比"离锚点最近"
            // 重要；标注被挤下去一点不影响读。同一条边距里两者争位置时，这一行就是裁决。
            if (a.IsAnnotation != b.IsAnnotation)
                return a.IsAnnotation ? 1 : -1;

            // GUI 坐标 Y 向下：锚点投影得越靠上，排得越靠前。分档而不是直接比大小——
            // 「差不多齐平」要判成平级，而分档是可传递的，直接比差值会得到 a<b、b<c 却
            // a==c 的比较器，List.Sort 会当场抛。
            int ya = Mathf.RoundToInt(a.AnchorPos.y / AnchorTieBand);
            int yb = Mathf.RoundToInt(b.AnchorPos.y / AnchorTieBand);
            if (ya != yb)
                return ya.CompareTo(yb);
            int xa = Mathf.RoundToInt(a.AnchorPos.x / AnchorTieBand);
            int xb = Mathf.RoundToInt(b.AnchorPos.x / AnchorTieBand);
            if (xa != xb)
                return xa.CompareTo(xb);
            if (a.DeclarationOrder != b.DeclarationOrder)
                return a.DeclarationOrder.CompareTo(b.DeclarationOrder);
            return string.CompareOrdinal(a.Key, b.Key);
        }

        // 按目标横向跨度扫一遍，把彼此搭上的卡归进同一列；列内保持稳定的上下顺序。
        private static List<List<ProjectedCardLayout>> GroupIntoColumns(List<ProjectedCardLayout> ranked)
        {
            var order = new List<int>();
            for (int i = 0; i < ranked.Count; i++)
                order.Add(i);
            order.Sort((x, y) =>
            {
                float lx = ranked[x].TargetCenter.x - ranked[x].FootprintWidth / 2f;
                float ly = ranked[y].TargetCenter.x - ranked[y].FootprintWidth / 2f;
                return !Mathf.Approximately(lx, ly) ? lx.CompareTo(ly) : x.CompareTo(y);
            });

            var columns = new List<List<ProjectedCardLayout>>();
            var current = new List<int>();
            float reach = float.NegativeInfinity;
            foreach (int i in order)
            {
                float left = ranked[i].TargetCenter.x - ranked[i].FootprintWidth / 2f;
                float right = ranked[i].TargetCenter.x + ranked[i].FootprintWidth / 2f;
                if (current.Count > 0 && left >= reach)
                {
                    columns.Add(TakeColumn(ranked, current));
                    current = new List<int>();
                    reach = float.NegativeInfinity;
                }
                current.Add(i);
                reach = Mathf.Max(reach, right);
            }
            if (current.Count > 0)
                columns.Add(TakeColumn(ranked, current));
            return columns;
        }

        private static List<ProjectedCardLayout> TakeColumn(List<ProjectedCardLayout> ranked, List<int> indices)
        {
            indices.Sort();   // 回到 rank 顺序
            var column = new List<ProjectedCardLayout>(indices.Count);
            foreach (int i in indices)
                column.Add(ranked[i]);
            return column;
        }

        // 一列卡的排布。
        //
        // 竖着放得下就竖着放（一维消重叠）；一列真的塞不下，就**并排分成几摞**，
        // 而不是硬挤成一叠。以前是后者：解算完再一张张按边界夹回来，超出去的部分被压平，
        // 于是一间挂了四五个动作的酒馆总是叠成一摞——这正是「怎么还是重叠」的根因，
        // 光把上下边界放宽是治不了的。
        //
        // 边界按整列的横向跨度算一次，全列共用：每张卡各按自己的跨度算的话，
        // 同一列的卡会得到不一样的上下限，解算出来的间距又会在夹取时被破坏。
        private static void SolveColumn(List<ProjectedCardLayout> column, in CardKeepOut keepOut)
        {
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            foreach (var card in column)
            {
                left = Mathf.Min(left, card.TargetCenter.x - card.Width / 2f);
                right = Mathf.Max(right, card.TargetCenter.x + card.Width / 2f);
            }

            float required = StackHeight(column);
            var band = keepOut.BandFor(left, right, required);

            if (required > band.Bottom - band.Top)
            {
                FanOutColumn(column, keepOut, band.Bottom - band.Top);
                return;
            }

            StackInPlace(column);
            FitStackIntoBand(column, band, required);
        }

        // 这一摞卡竖着摆需要多高（各自的上下伸展 + 卡间缝）。
        private static float StackHeight(List<ProjectedCardLayout> stack)
        {
            float total = CardStackGap * (stack.Count - 1);
            foreach (var card in stack)
                total += card.TopExtent + card.BottomExtent;
            return total;
        }

        // 解算完的一摞整体平移进可用区间。分块解算给出的间距可能大于最小值（几块之间本来
        // 就该有空隙），整摞比区间还长时就退回「贴紧排一遍」——宁可失去那点呼吸感，
        // 也不让卡片被逐张夹回来后又叠在一起。
        private static void FitStackIntoBand(List<ProjectedCardLayout> stack, (float Top, float Bottom) band, float required)
        {
            float spanTop = float.PositiveInfinity, spanBottom = float.NegativeInfinity;
            foreach (var card in stack)
            {
                spanTop = Mathf.Min(spanTop, card.SolvedCenter.y - card.TopExtent);
                spanBottom = Mathf.Max(spanBottom, card.SolvedCenter.y + card.BottomExtent);
            }

            if (spanBottom - spanTop > band.Bottom - band.Top)
            {
                StackTight(stack, Mathf.Clamp(MeanTargetY(stack) - required / 2f, band.Top, band.Bottom - required));
                return;
            }

            float shift = Mathf.Max(0f, band.Top - spanTop) - Mathf.Max(0f, spanBottom - band.Bottom);
            if (Mathf.Approximately(shift, 0f))
                return;
            foreach (var card in stack)
                card.SolvedCenter = new Vector2(card.SolvedCenter.x, card.SolvedCenter.y + shift);
        }

        // 从 top 起贴着最小间距排一遍。横向位置保持不变（由调用方先定好）。
        private static void StackTight(List<ProjectedCardLayout> stack, float top)
        {
            float y = top;
            foreach (var card in stack)
            {
                card.SolvedCenter = new Vector2(card.SolvedCenter.x, y + card.TopExtent);
                y += card.TopExtent + card.BottomExtent + CardStackGap;
            }
        }

        private static float MeanTargetY(List<ProjectedCardLayout> stack)
        {
            float sum = 0f;
            foreach (var card in stack)
                sum += card.TargetCenter.y;
            return sum / stack.Count;
        }

        // 一列放不下：拆成并排的几摞，整体居中在原来那一列的位置上。
        //
        // 顺序仍然是「先上后下、再左到右」——第一摞装最靠上的几张，装满换下一摞。引线会
        // 说明每张卡指的是哪儿，所以横向散开不会让人跟丢；叠在一起才会。
        // 散开后的总宽可能压到旁边一列的卡上（那一列本来横向错开、没被并进来）；
        // 同一栋楼的卡挤在一处时这种情况很少见，让它按画家算法压过去即可。
        private static void FanOutColumn(List<ProjectedCardLayout> column, in CardKeepOut keepOut, float available)
        {
            var lanes = SplitIntoLanes(column, Mathf.Clamp(
                Mathf.CeilToInt(StackHeight(column) / Mathf.Max(1f, available)), 2, column.Count));

            float totalWidth = -CardLaneGap;
            foreach (var lane in lanes)
                totalWidth += LaneWidth(lane) + CardLaneGap;

            float center = 0f;
            foreach (var card in column)
                center += card.TargetCenter.x;
            center /= column.Count;

            Rect safe = UIScale.SafeArea;
            float minX = safe.x + 12f;
            float x = Mathf.Clamp(center - totalWidth / 2f, minX, Mathf.Max(minX, safe.xMax - 12f - totalWidth));

            foreach (var lane in lanes)
            {
                float width = LaneWidth(lane);
                float laneCenter = x + width / 2f;
                foreach (var card in lane)
                    card.SolvedCenter = new Vector2(laneCenter, card.SolvedCenter.y);

                // 每摞按自己所在的横向位置重新问一次边界：靠边那摞可能正压在按钮上下。
                float required = StackHeight(lane);
                var band = keepOut.BandFor(x, x + width, required);
                StackTight(lane, Mathf.Clamp(
                    MeanTargetY(lane) - required / 2f,
                    band.Top,
                    Mathf.Max(band.Top, band.Bottom - required)));

                x += width + CardLaneGap;
            }
        }

        // 按累计高度切分，保持原有的上下顺序。切到只剩一摞时全部塞进去——
        // 摞数是个上限，不是必须凑够的数。
        private static List<List<ProjectedCardLayout>> SplitIntoLanes(List<ProjectedCardLayout> column, int laneCount)
        {
            float perLane = StackHeight(column) / laneCount;
            var lanes = new List<List<ProjectedCardLayout>>();
            var current = new List<ProjectedCardLayout>();
            float filled = 0f;

            foreach (var card in column)
            {
                float need = card.TopExtent + card.BottomExtent + CardStackGap;
                // 过半才换摞：一张卡跨在分界上时，按它落在哪一边多一点来归属。
                if (current.Count > 0 && lanes.Count < laneCount - 1 && filled + need * 0.5f > perLane)
                {
                    lanes.Add(current);
                    current = new List<ProjectedCardLayout>();
                    filled = 0f;
                }
                current.Add(card);
                filled += need;
            }
            lanes.Add(current);
            return lanes;
        }

        private static float LaneWidth(List<ProjectedCardLayout> lane)
        {
            float width = 0f;
            foreach (var card in lane)
                width = Mathf.Max(width, card.Width);
            return width;
        }

        // 一维消重叠：顺序已经定死，只需让挤在一起的卡各自让开。互相挤住的连成一「块」，
        // 整块停在成员们期望位置的平均处——所以卡群仍然贴着锚点，不会整体往下漂。
        private static void StackInPlace(List<ProjectedCardLayout> column)
        {
            int n = column.Count;

            // cum[i]：第 i 张卡的中心相对本块块首至少要偏开多远（相邻最小间距的前缀和）。
            var cum = new float[n];
            for (int i = 1; i < n; i++)
                cum[i] = cum[i - 1] + column[i - 1].BottomExtent + column[i].TopExtent + CardStackGap;

            var blockHead = new List<int>();
            var blockCount = new List<int>();
            var blockSum = new List<float>();   // Σ(期望中心 − 相对块首的固定偏移)
            for (int i = 0; i < n; i++)
            {
                blockHead.Add(i);
                blockCount.Add(1);
                blockSum.Add(column[i].TargetCenter.y);

                while (blockHead.Count >= 2)
                {
                    int last = blockHead.Count - 1;
                    int prev = last - 1;
                    float span = cum[blockHead[last]] - cum[blockHead[prev]];
                    if (blockSum[last] / blockCount[last] >= blockSum[prev] / blockCount[prev] + span)
                        break;

                    blockSum[prev] += blockSum[last] - blockCount[last] * span;
                    blockCount[prev] += blockCount[last];
                    blockHead.RemoveAt(last);
                    blockCount.RemoveAt(last);
                    blockSum.RemoveAt(last);
                }
            }

            for (int b = 0; b < blockHead.Count; b++)
            {
                int head = blockHead[b];
                int end = b + 1 < blockHead.Count ? blockHead[b + 1] : n;
                float headY = blockSum[b] / blockCount[b];
                for (int i = head; i < end; i++)
                {
                    column[i].SolvedCenter = new Vector2(
                        column[i].TargetCenter.x,
                        headY + cum[i] - cum[head]);
                }
            }
        }

        private static Vector2 ViewportDirection(Vector3 viewportPosition)
        {
            var direction = new Vector2(viewportPosition.x - 0.5f, 0.5f - viewportPosition.y);
            if (viewportPosition.z < 0f)
                direction = -direction;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        }

        // 网格卡的版面：位置只由序号、屏幕尺寸和滚动量决定，所以命中解析和实际绘制
        // 共用这几个函数，不各算一遍。返回的是视口内的局部坐标。
        // ── 卡片体量 ─────────────────────────────────────────────────────
        //
        // 手机上所有卡都要按更小的基准画，这不是"缩水"，是横屏手机的形状使然：
        // 虚拟画布只有 826×465 上下，一张按 340 宽画的卡就占掉近半个屏幕宽，一屏放不下
        // 两列还看得清的东西。字号不受影响（字号由画布定，不由卡宽定），少掉的是留白。
        //
        // 250 是按内容反推的：副标题一行 14 字、标题 10 字、三个骰位横排 196px 都放得下，
        // 而 826 宽的画布正好排得下三列。
        // 地点牌的宽度不在这儿：它由地名实际多长决定，见 ContainerNodeDrawer.PreferredLocationWidth。
        private const float ActionCardDesignWidth = 250f;
        private const float FocusedCardWidth = 330f;

        // 网格卡的基准宽度。实际宽度由 GridCardWidth 按当前列数摊开：列数定死之后，
        // 剩下的宽度摊回给每张卡，右边不留一条没用的空地。
        private const float GridCardDesignWidth = ActionCardDesignWidth;
        // 残留（节点已消失、只剩结算结果的宿主卡）没有内容可量，保留一个固定的小盒子。
        private const float GridSpacing = 20f;
        // 金色脉冲描边会向卡片外扩最多 7.5px；网格卡不能贴着 GUI.Group 顶部，
        // 否则第一行卡片的上边会被父 Group 裁掉。
        private const float GridContentTopPadding = 10f;

        // 左右留白随设备走：桌面 40px 的边距搬到手机的虚拟画布上会吃掉整整一列卡。
        private const float GridSideMargin = 16f;
        private static float GridStartX => UIScale.SafeArea.x + GridSideMargin;
        private static float GridEndX => UIScale.SafeArea.xMax - GridSideMargin;

        private static int GridCardsPerRow()
        {
            return Mathf.Max(1, (int)((GridEndX - GridStartX + GridSpacing) / (GridCardDesignWidth + GridSpacing)));
        }

        // 列数定下来之后，把剩下的宽度摊回给每张卡（最多放宽到基准的 1.4 倍，再宽就不像卡了）。
        private static float GridCardWidth
        {
            get
            {
                int columns = GridCardsPerRow();
                float available = GridEndX - GridStartX - GridSpacing * (columns - 1);
                return Mathf.Clamp(available / columns, GridCardDesignWidth, GridCardDesignWidth * 1.4f);
            }
        }

        // 网格视口夹在顶栏下沿和底部手牌簇之间；两头都由各自的所有者报高度，这里不写死。
        // 场景标注带横贯整行，它占了画面上方多少，网格就从多少往下开始。
        // 钉住条**不**算在这一头：它只占左上一块，整行让开等于把 W×B 白白让掉。
        // 它压着的那几列从它底下起排，别的列照常从顶上起（见 BuildGridCardLayouts）。
        private Rect GridViewport()
        {
            // 网格是固定在屏幕上的 UI，不会像空间投射卡那样随视角移动；它的最低上界
            // 永远是顶栏内容区。根节点没有场景标注时 _sceneBandRect 是零矩形；
            // 只取它的 yMax 会让网格从 y=0 开始，把顶栏整块盖住。
            float top = Mathf.Max(_topHud.ContentTop, _sceneBandRect.yMax);
            float bottom = UIScale.SafeArea.yMax - HandPanelDrawer.ReservedHeight;
            return new Rect(0f, top, UIScale.VW, Mathf.Max(0f, bottom - top));
        }

        private List<GridCardLayout> BuildGridCardLayouts(
            IReadOnlyList<GameNode> nodes,
            float scrollOffset,
            out float contentHeight)
        {
            int columnCount = GridCardsPerRow();
            int totalCards = nodes.Count;
            var columnHeights = new float[columnCount];
            // 钉住条压着的列从它底下起排（视口局部坐标）；没压着的列从顶上起。
            float viewportTop = GridViewport().y;
            for (int i = 0; i < columnHeights.Length; i++)
            {
                float colX = GridStartX + i * (GridCardWidth + GridSpacing);
                bool underPin = _pinStripRect.width > 0f
                    && colX < _pinStripRect.xMax && colX + GridCardWidth > _pinStripRect.xMin
                    && _pinStripRect.yMax > viewportTop;
                columnHeights[i] = GridContentTopPadding
                    + (underPin ? _pinStripRect.yMax + GridSpacing - viewportTop : 0f);
            }
            var layouts = new List<GridCardLayout>(totalCards);

            for (int i = 0; i < totalCards; i++)
            {
                int column = i % columnCount;
                bool mustHandle = RestBlockerPresentation.ContainsTarget(nodes[i], _gameManager.DisplayedSnapshot.RestBlockers);
                // 动作卡的「必须处理」是一张顶部书签（见 ActionNodeDrawer.DrawTopTabs）；
                // 只有人物/地点这类没有书签的卡还挂卡外的牌子。
                bool isActionCard = nodes[i].HasResolve;
                float markerSpace = mustHandle && !isActionCard ? CardDrawer.ExternalRestBlockerMarkerSpace : 0f;
                // 卡顶书签探出卡外，网格行距只有 GridSpacing，不预留就会盖住上一张卡的底。
                if (isActionCard && ActionNodeDrawer.HasTopTabs(nodes[i], mustHandle))
                    markerSpace += ActionNodeDrawer.TopTabsRise();
                // 每张网格卡按自身内容定高（瀑布流本来就允许列内高度不齐）。以前这里是固定
                // 190：副标题长一点、时钟徽章多一个，内容就只能在同一个盒子里互相挤。
                float cardHeight = CardDrawer.MeasureCardHeight(
                    nodes[i],
                    CardDrawer.Classify(nodes[i], anchored: false),
                    GridCardWidth,
                    _gameManager.DisplayedSnapshot.Actors);
                var cardRect = new Rect(
                    GridStartX + column * (GridCardWidth + GridSpacing),
                    columnHeights[column] + markerSpace - scrollOffset,
                    GridCardWidth,
                    cardHeight);
                layouts.Add(new GridCardLayout(cardRect));
                columnHeights[column] += markerSpace + cardHeight + GridSpacing;
            }

            contentHeight = totalCards == 0
                ? 0f
                : Mathf.Max(0f, columnHeights.Max() - GridSpacing);
            return layouts;
        }

        // 鼠标是否落在某张可见网格卡上（视口裁剪之外的部分不算）。
        private bool MouseOverGridCard(IReadOnlyList<GameNode> nodes, Vector2 mouse)
        {
            var viewport = GridViewport();
            if (nodes.Count <= 0 || !viewport.Contains(mouse))
                return false;

            var local = mouse - new Vector2(viewport.x, viewport.y);
            var layouts = BuildGridCardLayouts(nodes, _gridScrollOffset, out _);
            foreach (var layout in layouts)
            {
                if (layout.FootprintRect.Contains(local))
                    return true;
            }
            return false;
        }

        // 手指推着卡片网格滚动。触控设备上没有滚轮，这是唯一能翻到下面那几张卡的办法。
        //
        // 只在触控上启用：桌面用滚轮，鼠标按住拖动是「拖镜头」，不该变成滚列表。
        // 卡片的激活走 WasTapped（抬手才算），所以从卡片上开始滑不会顺手把它点掉。
        private void UpdateGridDragScroll(
            IMGUIInteractionContext ui, Rect viewport, bool pointerInViewport, float maxScroll)
        {
            if (UIScale.HasHoverPointer) return;

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && pointerInViewport && maxScroll > 0f:
                    _gridDragActive = true;
                    _gridDragLastY = ui.Mouse.y;
                    _gridDragTravel = 0f;
                    break;

                case EventType.MouseDrag when _gridDragActive:
                {
                    float dy = ui.Mouse.y - _gridDragLastY;
                    _gridDragLastY = ui.Mouse.y;
                    _gridDragTravel += Mathf.Abs(dy);
                    _gridScrollOffset -= dy;
                    // 推过阈值之后这次按压归滚动所有，别让它同时传给镜头。
                    if (_gridDragTravel >= IMGUIInteractionContext.TapSlop)
                        e.Use();
                    break;
                }

                case EventType.MouseUp:
                    _gridDragActive = false;
                    _gridDragTravel = 0f;
                    break;
            }
        }

        private void DrawCardsGrid(List<GameNode> nodes, IMGUIInteractionContext ui)
        {
            var visibleNodes = nodes;
            int totalCards = visibleNodes.Count;
            BuildGridCardLayouts(visibleNodes, 0f, out float contentHeight);
            var viewport = GridViewport();
            float maxScroll = Mathf.Max(0f, contentHeight - viewport.height);

            // ContainsMouse (not CanHover): this viewport spans the whole play area
            // and is only used to catch scroll-wheel events — it must not mark the
            // area as UI, or camera dragging through empty grid space would break.
            bool mouseInViewport = ui.ContainsMouse(viewport);
            if (mouseInViewport && Event.current.type == EventType.ScrollWheel && maxScroll > 0f)
            {
                _gridScrollOffset += Event.current.delta.y * 18f;
                Event.current.Use();
            }
            UpdateGridDragScroll(ui, viewport, mouseInViewport, maxScroll);
            _gridScrollOffset = Mathf.Clamp(_gridScrollOffset, 0f, maxScroll);
            var layouts = BuildGridCardLayouts(visibleNodes, _gridScrollOffset, out _);

            // 网格卡同样先登记附件、后画本体；后面的统一 overlay 才能盖过所有卡片。
            for (int i = 0; i < totalCards; i++)
            {
                Rect screenCardRect = layouts[i].CardRect;
                screenCardRect.position += viewport.position;
                NoteActionCardRect(visibleNodes[i], screenCardRect);
                AddAttachmentForNode(visibleNodes[i], screenCardRect, spacious: false);
            }
            bool attachmentConsumesPointer = AttachmentConsumesPointer(ui.Mouse);

            GUI.BeginGroup(viewport);
            var localUi = attachmentConsumesPointer
                ? ui.Occluded().Translated(new Vector2(viewport.x, viewport.y))
                : ui.Translated(new Vector2(viewport.x, viewport.y));

            // 卡片的边缘外挂（便签 / 能力片）攒到最后一起画：网格列间距只有 GridSpacing，
            // 外挂一定伸进邻居的地盘，一遍画下来右缘的能力片会被下一张卡的卡身盖掉。
            // 详见 ActionNodeDrawer.BeginDeferredEdgeAttachments。
            ActionNodeDrawer.BeginDeferredEdgeAttachments();

            for (int i = 0; i < totalCards; i++)
            {
                var layout = layouts[i];
                var cardRect = layout.CardRect;
                if (layout.FootprintRect.y > viewport.height || layout.FootprintRect.yMax < 0f)
                {
                    continue;
                }

                var node = visibleNodes[i];
                _dialogueAnchors.RegisterNode(node.Name, new Rect(
                    cardRect.x + viewport.x,
                    cardRect.y + viewport.y,
                    cardRect.width,
                    cardRect.height));

                bool isHovered = localUi.CanHover(cardRect);
                bool isFlipped = _gameManager.IsNodeFlipped(node.Name);
                bool focused = isFocused(node.Name);

                List<SlottedResource?>? slotted = null;
                if (node.Requires != null && node.Requires.Count > 0)
                {
                    slotted = SlotsForNode(node);
                }

                string backText = (node.Resolve?.Type == ResolveType.Observe) ? (node.Resolve?.ObserveText ?? "") : "";
                var execution = GetExecutionState(node.Name);
                bool isRestBlockerTarget = RestBlockerPresentation.IsTarget(node, _gameManager.DisplayedSnapshot.RestBlockers);
                bool containsRestBlockerTarget = RestBlockerPresentation.ContainsTarget(node, _gameManager.DisplayedSnapshot.RestBlockers);

                var interaction = CardDrawer.DrawCard(cardRect, node, CardDrawer.Classify(node, anchored: false), isHovered, isFlipped, focused,
                    slotted, ClocksForCard(node), backText, localUi, _gameManager,
                    execution.IsExecuting, execution.Progress, execution.Text,
                    IsLocalRollOf(node.Name),
                    isRestBlockerTarget,
                    containsRestBlockerTarget);

                if (interaction.CardClicked)
                {
                    if (node.IsContainer)
                    {
                        _gridScrollStack.Add(_gridScrollOffset);
                        _gridScrollOffset = 0f;
                    }
                    _gameManager.OnNodeCardClicked(node);
                }
                if (interaction.ClickedSlotIndex != -1 && slotted != null && node.Requires != null)
                {
                    _gameManager.OnSlotClicked(node, interaction.ClickedSlotIndex);
                }
                if (interaction.DroppedSlotIndex != -1 && slotted != null && node.Requires != null)
                {
                    if (_gameManager.TryPlaceSelectedResource(node, interaction.DroppedSlotIndex))
                        _gameManager.MarkResourceDropHandled();
                }
                if (interaction.ExecuteClicked)
                {
                    _gameManager.ExecuteNodeAction(node);
                }
            }

            // 必须在 EndGroup 之前：外挂的坐标是这个分组里的局部坐标。
            // 这一句不能被上面任何一条 continue 或异常绕过，否则标志位会卡在"还在攒"，
            // 世界投射的卡也跟着不画外挂了。
            ActionNodeDrawer.FlushDeferredEdgeAttachments();

            GUI.EndGroup();
            DrawGridScrollbar(viewport, contentHeight, _gridScrollOffset, maxScroll);
        }

        private readonly struct GridCardLayout
        {
            public Rect CardRect { get; }
            public Rect FootprintRect => CardRect;

            public GridCardLayout(Rect cardRect)
            {
                CardRect = cardRect;
            }
        }

        // 一张卡下面挂的东西，三选一：操作台（装填了 / 执行中）→ 判定条（掷骰动画）→ 结果条。
        // 三者接力，同一时刻一张卡只有一件。
        private readonly struct CardAttachmentOverlay
        {
            public Rect CardRect { get; }
            public string HostNodeName { get; }
            public GameNode? ConsoleNode { get; }
            public List<SlottedResource?>? ConsoleSlots { get; }
            public ActionReport? LocalRoll { get; }
            public int LocalRollPhase { get; }
            public int LocalRollDieValue { get; }
            public float LocalRollScale { get; }
            public CardPresentationResidue? Residue { get; }
            public bool Spacious { get; }
            // 宿主贴着屏幕底边（随身卡、休息键）：附件往上挂。
            public bool Above { get; }

            private CardAttachmentOverlay(
                Rect cardRect, string hostNodeName, GameNode? consoleNode, List<SlottedResource?>? consoleSlots,
                ActionReport? localRoll, int localRollPhase,
                int localRollDieValue, float localRollScale, CardPresentationResidue? residue, bool spacious,
                bool above = false)
            {
                Above = above;
                CardRect = cardRect;
                HostNodeName = hostNodeName;
                ConsoleNode = consoleNode;
                ConsoleSlots = consoleSlots;
                LocalRoll = localRoll;
                LocalRollPhase = localRollPhase;
                LocalRollDieValue = localRollDieValue;
                LocalRollScale = localRollScale;
                Residue = residue;
                Spacious = spacious;
            }

            public static CardAttachmentOverlay ForConsole(Rect cardRect, GameNode node, List<SlottedResource?> slots)
                => new CardAttachmentOverlay(cardRect, node.Name, node, slots, null, 0, 0, 1f, null, false);

            public static CardAttachmentOverlay ForLocalRoll(
                Rect cardRect, string hostNodeName, ActionReport report, int phase, int dieValue, float scale, bool spacious,
                bool above = false)
                => new CardAttachmentOverlay(cardRect, hostNodeName, null, null, report, phase, dieValue, scale, null, spacious, above);

            public static CardAttachmentOverlay ForResidue(Rect cardRect, CardPresentationResidue residue, bool spacious)
                => new CardAttachmentOverlay(cardRect, residue.HostNodeName, null, null, null, 0, 0, 1f, residue, spacious, residue.AttachAbove);
        }

        // 引线不在这里画：它是所有卡片之前的一整层，见 CardLeaderLineDrawer。
        private void NoteActionCardRect(GameNode node, Rect screenRect)
        {
            _drawnCardRects[node.Name] = screenRect;
            if (_firstActionCardRect.HasValue) return;
            if (node.Resolve == null || node.IsContainer) return;
            _firstActionCardRect = screenRect;
        }

        private void DrawNodeCard(GameNode node, Rect cardRect, IMGUIInteractionContext ui)
        {
            NoteActionCardRect(node, cardRect);
            bool isHovered = ui.CanHover(cardRect);
            bool isFlipped = _gameManager.IsNodeFlipped(node.Name);
            bool focused = isFocused(node.Name);

            List<SlottedResource?>? slotted = null;
            if (node.Requires != null && node.Requires.Count > 0)
            {
                slotted = SlotsForNode(node);
            }

            string backText = (node.Resolve?.Type == ResolveType.Observe) ? (node.Resolve?.ObserveText ?? "") : "";
            var execution = GetExecutionState(node.Name);
            bool isRestBlockerTarget = RestBlockerPresentation.IsTarget(node, _gameManager.DisplayedSnapshot.RestBlockers);
            bool containsRestBlockerTarget = RestBlockerPresentation.ContainsTarget(node, _gameManager.DisplayedSnapshot.RestBlockers);

            var interaction = CardDrawer.DrawCard(cardRect, node, CardDrawer.Classify(node, anchored: true), isHovered, isFlipped, focused,
                slotted, ClocksForCard(node), backText, ui, _gameManager,
                execution.IsExecuting, execution.Progress, execution.Text,
                IsLocalRollOf(node.Name),
                isRestBlockerTarget,
                containsRestBlockerTarget);

            if (interaction.CardClicked)
            {
                _gameManager.OnNodeCardClicked(node);
            }

            if (interaction.ClickedSlotIndex != -1 && slotted != null && node.Requires != null)
            {
                _gameManager.OnSlotClicked(node, interaction.ClickedSlotIndex);
            }

            if (interaction.DroppedSlotIndex != -1 && slotted != null && node.Requires != null)
            {
                if (_gameManager.TryPlaceSelectedResource(node, interaction.DroppedSlotIndex))
                    _gameManager.MarkResourceDropHandled();
            }

            if (interaction.ExecuteClicked)
            {
                _gameManager.ExecuteNodeAction(node);
            }
        }

        private void SyncNavigationScrollState()
        {
            int depth = _gameManager.NavigationStack.Count;
            if (depth < _lastNavigationDepth)
            {
                while (_gridScrollStack.Count > depth)
                {
                    _gridScrollStack.RemoveAt(_gridScrollStack.Count - 1);
                }
                _gridScrollOffset = PopGridScrollOffset();
            }
            else if (depth == 0 && _lastNavigationDepth != 0)
            {
                _gridScrollStack.Clear();
                _gridScrollOffset = 0f;
            }
            else if (depth > _lastNavigationDepth + 1)
            {
                _gridScrollStack.Clear();
                _gridScrollOffset = 0f;
            }

            _lastNavigationDepth = depth;
        }

        private float PopGridScrollOffset()
        {
            if (_gridScrollStack.Count == 0)
            {
                return 0f;
            }

            int last = _gridScrollStack.Count - 1;
            float offset = _gridScrollStack[last];
            _gridScrollStack.RemoveAt(last);
            return offset;
        }

        private static void DrawGridScrollbar(Rect viewport, float contentHeight, float scrollOffset, float maxScroll)
        {
            if (contentHeight <= viewport.height || viewport.height <= 0f)
            {
                return;
            }

            float trackW = 6f;
            var track = new Rect(viewport.xMax - trackW - 10f, viewport.y + 4f, trackW, viewport.height - 8f);
            float thumbH = Mathf.Max(34f, track.height * (viewport.height / contentHeight));
            float travel = Mathf.Max(0f, track.height - thumbH);
            float thumbY = track.y + (maxScroll <= 0f ? 0f : travel * (scrollOffset / maxScroll));
            var thumb = new Rect(track.x, thumbY, track.width, thumbH);

            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.75f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f);
            GUI.DrawTexture(thumb, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private bool isFocused(string nodeName)
        {
            return _gameManager.FocusedNodeName == nodeName;
        }

        /// <summary>
        /// 某个动作此刻的「执行中」进度。原来这只服务画成卡的节点（进度条画在执行钮里），
        /// 于是没有执行钮的两处——右下角的随身挂件（抽烟）和交锋里的休息键——
        /// 明明也在播同一段演出，却什么都不显示。改成按名字问，谁都能来取。
        /// </summary>
        public (bool IsExecuting, float Progress, string Text) GetExecutionState(string nodeName)
        {
            if (_activeAutoAction?.AutoActionNode != null
                && string.Equals(_activeAutoAction.AutoActionNode.Name, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                float elapsed = AutoActionCaptureElapsed(Time.unscaledTime);
                if (elapsed < AutoActionExecuteStart || _activeAutoActionResolved)
                    return (false, 0f, string.Empty);
                return (true, Mathf.Clamp01((elapsed - AutoActionExecuteStart) / AutoActionExecuteSeconds), "执行中");
            }
            if (!_presentationPlayer.IsPlaying || _animator.IsPlaying)
            {
                return (false, 0f, string.Empty);
            }
            if (!string.Equals(_presentationPlayer.ActionName, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                return (false, 0f, string.Empty);
            }
            if (string.IsNullOrEmpty(_presentationPlayer.ProgressText))
            {
                return (false, 0f, string.Empty);
            }
            return (true, _presentationPlayer.Progress01, _presentationPlayer.ProgressText);
        }

        private List<SlottedResource?>? SlotsForNode(GameNode node)
        {
            if (_activeAutoAction?.AutoActionNode == node)
            {
                var result = new List<SlottedResource?>();
                bool landed = AutoActionCaptureElapsed(Time.unscaledTime) >= AutoActionFlightSeconds;
                for (int i = 0; i < node.Requires.Count; i++)
                    result.Add(landed && i < _activeAutoAction.AutoActionSlots.Count
                        ? _activeAutoAction.AutoActionSlots[i]
                        : null);
                return result;
            }
            return _gameManager.GetSlotsForNode(node.Name);
        }

        private void DrawGrowthToggleButton(IMGUIInteractionContext ui, Rect btnRect)
        {
            // 和卷宗 / 帮助 / 设置共用同一份顶栏文字开关。原来这里是另写的一份，
            // 用了 ExecuteLabel、也没铺 HUD 底，排在其余三个旁边一眼就看得出不是一伙的。
            if (IMGUIButton.DrawTopTextToggle(btnRect, "成长", _isGrowthPanelOpen, ui))
            {
                _isGrowthPanelOpen = !_isGrowthPanelOpen;
                if (_isGrowthPanelOpen)
                {
                    // 成长面板打开时不留一个悬在背后的下拉——避免两个 Panel 层弹窗抢点击。
                    SettingsPanelDrawer.Close();
                    DebugPanelDrawer.Close();
                }
            }

            // 未花成长点角标：只看尼尔。加点只负责他一个人，没有全队概念——
            // 他还有点可加，按钮右上角就挂一块墨牌写着数；挂零就藏，不占地方。
            var snapshot = _gameManager.DisplayedSnapshot;
            int unspent = 0;
            if (snapshot.Actors.Count > 0)
            {
                var lead = snapshot.Actors[0];
                unspent = Mathf.Max(0, snapshot.GrowthLevel - lead.SpentGrowthPoints);
            }
            if (unspent > 0)
                DrawGrowthBadge(btnRect, unspent);
        }

        /// <summary>
        /// 成长按钮右上角的墨牌角标：实心墨底＋纸字数字，骑在按钮角上。
        /// 矩形全由按钮矩形推导，不碰顶栏布局；纯展示，不消费点击。
        /// </summary>
        private static void DrawGrowthBadge(Rect btnRect, int count)
        {
            string text = count.ToString();
            float badgeH = 18f;
            float badgeW = 20f + 8f * (text.Length - 1);
            var badge = UIScale.PixelSnap(new Rect(
                btnRect.xMax - badgeW * 0.5f,
                btnRect.y - badgeH * 0.5f + 1f,
                badgeW,
                badgeH));
            IMGUIStyles.SetColor(IMGUIStyles.PaperInk);
            GUI.DrawTexture(badge, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            var style = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                fontSize = IMGUIStyles.FontSize(12),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.Paper },
            };
            IMGUIStyles.ApplyStrongFont(style);
            IMGUIStyles.DrawLabel(badge, text, style);
        }

        private void DrawPresentationOverlay()
        {
            // Execution progress is drawn inside the active card's execute button.
        }

        /// <summary>
        /// 已经登记但还没轮到播放的 auto-action（排在 _pendingBlockingSteps 里，卡还没出现）
        /// 早就在游戏状态里扣走了这些骰子——不然回合结束时骰子不够用没法校验。但那几支骰子
        /// 在玩家眼里应该还在手上：真正"离手"的观感由 DrawAutoActionDiceFlights 从卡片出现
        /// 那一刻开始画。这里把这段真空期要补的骰子列出来，交给 HandPanelDrawer 当成手牌
        /// 还在的骰子画，卡片出现后这份数据自然从队列里消失，接力棒转给飞行动画。
        /// 正在播放的那一个（_activeAutoAction）不算在内：它从第 0 帧起已经由
        /// DrawAutoActionDiceFlights 在同一个位置画出实际数值，这里再画一遍只会重复。
        /// </summary>
        private List<SlottedResource> PendingAutoActionReservedDice()
        {
            var result = new List<SlottedResource>();
            foreach (var step in _pendingBlockingSteps)
                if (step.Kind == BlockingStoryStepKind.AutoAction)
                    result.AddRange(step.AutoActionSlots);
            return result;
        }

        private void DrawAutoActionDiceFlights()
        {
            var step = _activeAutoAction;
            var node = step?.AutoActionNode;
            if (step == null || node == null || !_drawnCardRects.TryGetValue(node.Name, out var cardRect))
                return;

            float elapsed = AutoActionCaptureElapsed(Time.unscaledTime);
            if (elapsed < 0f || elapsed >= AutoActionFlightSeconds)
                return;
            float raw = Mathf.Clamp01(elapsed / AutoActionFlightSeconds);
            float eased = raw * raw * (3f - 2f * raw);
            for (int i = 0; i < step.AutoActionSlots.Count; i++)
            {
                var die = step.AutoActionSlots[i];
                if (!HandPanelDrawer.TryGetActionDieRect(_gameManager, die.ActorId, die.DieIndex, out var source))
                    continue;
                var target = ActionNodeDrawer.AutoActionSlotRect(cardRect, node, i);
                var moving = new Rect(
                    Mathf.Lerp(source.x, target.x, eased),
                    Mathf.Lerp(source.y, target.y, eased),
                    Mathf.Lerp(source.width, target.width, eased),
                    Mathf.Lerp(source.height, target.height, eased));
                HandPanelDrawer.DrawMovingDie(moving, die.Value);
            }
        }

        private float AutoActionCaptureElapsed(float now)
            => _activeAutoActionCaptureStartedAt < 0f
                ? -1f
                : now - _activeAutoActionCaptureStartedAt;

        private void AddLightResidueIfNeeded(ActionReport report, string actionName)
        {
            // 没有锚定动作名的演出（到达一个地点）没有宿主，也没有「这一手」可言，不挂。
            if (string.IsNullOrWhiteSpace(actionName))
                return;

            // 判定总要留下命运条；即时动作什么都没改就没有可看的，不挂空条。
            if (report.Type != ActionType.Roll && report.Effects.Count == 0)
                return;

            var sourceNode = _gameManager.VisibleNodes.FirstOrDefault(node =>
                string.Equals(node.Name, actionName, StringComparison.OrdinalIgnoreCase));
            // 功能键（交锋里的休息）从来不是一张卡：没有宿主就没有残影可挂。
            // 否则它的回合账会顶着浮空牌子压在转场演出上，和散场对白叠在一起。
            if (sourceNode == null)
                return;
            var residue = new CardPresentationResidue
            {
                HostNodeName = actionName,
                RollOutcome = report.Type == ActionType.Roll ? report.Outcome : null,
                FateDieValue = report.Type == ActionType.Roll ? report.FateDieValue : null,
                PreparedValue = report.Type == ActionType.Roll ? report.PreparedValue : 0,
                Effects = new List<ActionEffectRecord>(report.Effects),
                SourceNode = sourceNode,
                SettledClocks = SettledClocksFor(actionName, sourceNode, report),
            };
            // 记下宿主现在站在哪，它从快照里消失后残卡就站这儿。
            if (_drawnCardRects.TryGetValue(actionName, out var drawnRect))
            {
                residue.GhostRect = drawnRect;
                var anchor = sourceNode != null ? _gameManager.ResolveAnchor(sourceNode) : null;
                var cam = Camera.main;
                if (anchor != null && cam != null)
                {
                    var virtualPos = UIScale.WorldPointToVirtual(cam.WorldToScreenPoint(anchor.transform.position));
                    residue.GhostAnchor = anchor.transform;
                    residue.GhostOffset = drawnRect.position - new Vector2(virtualPos.x, virtualPos.y);
                }
            }
            _cardResidues[actionName] = residue;
        }

        // 结算后的钟。宿主还在引擎树里就直接读它；已经被删了（钟归零把它带走了）就拿旧钟
        // 按这一手的效果行推一遍——效果行里写着推了哪根钟几格。
        private List<GameClock>? SettledClocksFor(string name, GameNode? sourceNode, ActionReport report)
        {
            var root = _gameManager.SceneManager.LatestSnapshot.RootNode;
            var settled = root == null ? null : FindNodeByName(root, name);
            if (settled != null)
                return settled.Clocks;
            if (sourceNode == null || sourceNode.Clocks.Count == 0)
                return null;

            var clocks = new List<GameClock>();
            foreach (var clock in sourceNode.Clocks)
            {
                int current = clock.Current;
                foreach (var effect in report.Effects)
                    if (effect.Kind == ActionEffectKind.Clock && effect.Delta.HasValue && effect.Label == clock.Label)
                        current = Mathf.Clamp(current + effect.Delta.Value, 0, clock.Max);
                clocks.Add(new GameClock
                {
                    Label = clock.Label, Note = clock.Note, Current = current, Max = clock.Max, Style = clock.Style,
                });
            }
            return clocks;
        }

        private static GameNode? FindNodeByName(GameNode node, string name)
        {
            if (string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase))
                return node;
            foreach (var child in node.Children)
            {
                var found = FindNodeByName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void DrawSpotlightOverlay()
        {
            var spotlight = _activeActionSpotlight ?? _gameManager.GameState.SpotlightCenter.Current;
            if (spotlight == null)
            {
                return;
            }

            GUI.color = IMGUIStyles.Blocker;
            GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 聚光弹窗是「一封信 / 一条通告」，正文长度完全由内容决定：先量出正文换行后要多高，
            // 弹窗再照这个高度长。以前是固定 220 高 + 固定 72 高的正文框，长信要么被挤到按钮上，
            // 要么直接被裁掉。
            const float modalW = 600f;
            const float titleTop = 42f;
            const float titleH = 42f;
            const float bodyTop = 14f;      // 标题与正文之间
            const float bodyToButton = 22f; // 正文与按钮之间
            const float buttonH = 34f;
            const float bottomPad = 26f;
            float bodyWidth = modalW - 104f;

            var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                fontSize = IMGUIStyles.FontSize(20)
            };
            bool hasBody = !string.IsNullOrWhiteSpace(spotlight.Subtitle);
            float bodyH = hasBody
                ? subtitleStyle.CalcHeight(new GUIContent(spotlight.Subtitle), bodyWidth)
                : 0f;

            float modalH = titleTop + titleH
                + (hasBody ? bodyTop + bodyH : 0f)
                + bodyToButton + buttonH + bottomPad;
            var modal = UIScale.CenteredModal(modalW, modalH);

            IMGUIStyles.DrawShadow(modal, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(modal, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(28)
            };
            IMGUIStyles.DrawLabel(new Rect(modal.x + 28f, modal.y + titleTop, modal.width - 56f, titleH), spotlight.Title, titleStyle);

            if (hasBody)
            {
                IMGUIStyles.DrawLabel(new Rect(modal.x + 44f, modal.y + titleTop + titleH + bodyTop, bodyWidth, bodyH),
                    spotlight.Subtitle, subtitleStyle);
            }

            var btnRect = new Rect(modal.x + (modal.width - 112f) / 2f, modal.yMax - bottomPad - buttonH, 112f, buttonH);
            var mouse = Event.current.mousePosition;
            bool hovered = btnRect.Contains(mouse);
            bool clicked = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (IMGUIStyles.DrawTechnicalButton(btnRect, "确 定", hovered, clicked, IMGUIStyles.PaperInk, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f), IMGUIStyles.ExecuteLabel))
            {
                TryAdvanceSpotlight();
                Event.current.Use();
            }
            else
            {
                UsePointerEventForModal();
            }
        }

        // 非阻塞:把当前可见的 banter 气泡画在各说话人锚点上方(不消费点击,游戏照常)。
        private void DrawBanterOverlay()
        {
            if (_banterPlayer.Visible.Count == 0)
                return;
            DialogueBubbleDrawer.DrawBanter(
                _banterPlayer,
                _dialogueAnchors,
                _gameManager,
                speaker => ReportRemoteFallback("play-banter!", speaker));
        }

        // 阻塞：立绘舞台覆盖世界；Say 拍全屏左键推进（打字中补全，否则下一句），无字动作拍吞掉点击自动播完，后方控件由 IsInputLocked 显式禁用。
        private void DrawConversationOverlay()
        {
            if (_storyStagePlayer.IsActive)
            {
                StoryStageDrawer.DrawStoryStageFrame(_storyStagePlayer.CurrentLine, _storyStagePlayer.BeatIndex);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    TryAdvanceConversation();
                    Event.current.Use();
                }
                else UsePointerEventForModal();
                return;
            }
            var line = _conversationPlayer.CurrentLine;
            if (line == null)
            {
                // 舞台不在了，画面上的负片也得跟着撤——它是舞台借后处理翻的，不是世界自己的状态。
                StoryStageDrawer.EndConversation();
                return;
            }

            bool usedRemoteFallback = StoryStageDrawer.DrawConversationLine(
                line.Speaker,
                line.Text,
                _conversationPlayer.CurrentLineIndex,
                _dialogueAnchors,
                _conversationPlayer.AllowsRemoteParticipants,
                line.Stage);

            if (usedRemoteFallback && !_warnedAboutCurrentDialogueRemoteFallback)
            {
                ReportRemoteFallback("play-dialogue!", line.Speaker);
                _warnedAboutCurrentDialogueRemoteFallback = true;
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                TryAdvanceConversation();
                Event.current.Use();
            }
            else
            {
                UsePointerEventForModal();
            }
        }

        private void ReportRemoteFallback(string command, string speaker)
        {
            bool isBanter = command == "play-banter!";
            Debug.LogWarning(
                $"{command} 说话人 '{speaker}' 无法解析到当前屏幕锚点，"
                + (isBanter ? "已自动改用场外临时卡。" : "立绘舞台仍会显示，但本行被视为场外对白。")
                + "若此人确实不在场，请改用对应的 remote 调用；"
                + "否则请检查说话人拼写和动作前的可见节点。");
        }

        // 视频过场没配到 CutsceneSequence 时的占位:居中显示 [视频] tag。
        private void DrawVideoOverlay()
        {
            if (_activeVideoTag == null)
                return;
            var style = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(22),
            };
            IMGUIStyles.DrawLabel(new Rect(0f, UIScale.VH * 0.4f, UIScale.VW, 48f), $"[视频] {_activeVideoTag}", style);
        }

        private static void UsePointerEventForModal()
        {
            if (Event.current.type == EventType.MouseDown
                || Event.current.type == EventType.MouseUp
                || Event.current.type == EventType.ScrollWheel)
            {
                Event.current.Use();
            }
        }

        private void DrawNarrationOverlay()
        {
            string text = _narrationPlayer.CurrentSubtitle;
            if (string.IsNullOrWhiteSpace(text))
                return;

            float bandH = 96f;
            var rect = new Rect(0f, UIScale.VH - bandH, UIScale.VW, bandH);

            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.76f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            var style = new GUIStyle(IMGUIStyles.ModalBody)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(22),
                wordWrap = true
            };
            style.normal.textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.65f);
            IMGUIStyles.DrawLabel(new Rect(160f, rect.y + 18f, UIScale.VW - 320f, bandH - 36f), text, style);
        }

        // 世界投射卡的活动范围。
        //
        // 以前是上下各缩进一条的横带：上边界是整条顶栏的底边，下边界给骰子那一排让路。
        // 两条线都横贯全屏，可它们挡住的东西并不横贯全屏——顶栏中段只有「你在哪、第几天」
        // 这类读的字，底栏中段干脆是空的。卡片被两条并不存在的墙夹在中间，四百来像素里
        // 放不下两张带骰位的动作卡，一间挂了四五个动作的酒馆必然叠成一摞。
        //
        // 现在按「这块地有多重要」算：交互控件永远避让；顶栏和人物头像在空间充足时也避让，
        // 放不下时按优先级逐层释放。避让仍按横向跨度计算，所以屏幕另一侧的控件不会无端
        // 挤压这张卡。顶部空白和人物画面因此是可借用的空间，不再是假墙，也不是完全不存在。
        //
        // 这不只是好看：卡片能不能各自让开、不叠成一摞，差的就是这上下多出来的一百多像素。
        private enum KeepOutPriority
        {
            Portrait = 10,
            Hud = 20,
            WorldLabel = 90,
            Hard = 100,
            // 纯排布区：只参与 IsHardBlocked / BandFor，不参与 IsUiBlocked。
            // 顶栏整条 Bar 就是这种——它占满宽度是为了把动作卡压到顶栏下面，
            // 但里面真正要点的只有返回键和右上功能组（面包屑是读的）。如果把它
            // 也算成交互遮挡，飘到顶栏高度的地点牌上半截就永远点不到了。
            Layout = 110,
        }

        private readonly struct KeepOutZone
        {
            public Rect Rect { get; }
            public KeepOutPriority Priority { get; }

            public KeepOutZone(Rect rect, KeepOutPriority priority)
            {
                Rect = rect;
                Priority = priority;
            }
        }

        private readonly struct CardKeepOut
        {
            // 卡片离安全区边缘、离控件各留一点缝，别贴着画。
            private const float EdgeInset = 6f;
            private const float BlockerGap = 8f;

            private readonly Rect _safe;
            private readonly List<KeepOutZone> _zones;

            public CardKeepOut(Rect safe, List<KeepOutZone> zones)
            {
                _safe = safe;
                _zones = zones;
            }

            public bool IsHardBlocked(Rect rect)
            {
                foreach (var zone in _zones)
                {
                    if ((zone.Priority == KeepOutPriority.Hard
                            || zone.Priority == KeepOutPriority.WorldLabel
                            || zone.Priority == KeepOutPriority.Layout)
                        && rect.Overlaps(zone.Rect))
                        return true;
                }
                return false;
            }

            public bool IsUiBlocked(Rect rect)
            {
                foreach (var zone in _zones)
                    if (zone.Priority == KeepOutPriority.Hard && rect.Overlaps(zone.Rect))
                        return true;
                return false;
            }

            public float SoftOverlapArea(Rect rect)
            {
                float area = 0f;
                foreach (var zone in _zones)
                {
                    if (zone.Priority != KeepOutPriority.Portrait) continue;
                    var blocker = zone.Rect;
                    float width = Mathf.Max(0f, Mathf.Min(rect.xMax, blocker.xMax) - Mathf.Max(rect.xMin, blocker.xMin));
                    float height = Mathf.Max(0f, Mathf.Min(rect.yMax, blocker.yMax) - Mathf.Max(rect.yMin, blocker.yMin));
                    area += width * height;
                }
                return area;
            }

            /// <summary>
            /// 给定横向跨度和所需高度，返回纵向区间。硬区永远保留；软区从高优先级开始
            /// 尝试加入，只有仍放得下时才生效，因此空间不足时人物头像最先被侵占。
            /// </summary>
            public (float Top, float Bottom) BandFor(
                float left, float right, float requiredHeight, bool includeSoft = true)
            {
                float top = _safe.y + EdgeInset;
                float bottom = _safe.yMax - EdgeInset;
                ApplyPriority(KeepOutPriority.Hard, left, right, ref top, ref bottom);
                ApplyPriority(KeepOutPriority.WorldLabel, left, right, ref top, ref bottom);
                ApplyPriority(KeepOutPriority.Layout, left, right, ref top, ref bottom);
                if (!includeSoft)
                    return (top, Mathf.Max(top, bottom));

                var priorities = new[] { KeepOutPriority.Hud, KeepOutPriority.Portrait };
                foreach (var priority in priorities)
                {
                    float candidateTop = top;
                    float candidateBottom = bottom;
                    ApplyPriority(priority, left, right, ref candidateTop, ref candidateBottom);
                    if (candidateBottom - candidateTop >= requiredHeight)
                    {
                        top = candidateTop;
                        bottom = candidateBottom;
                    }
                }
                return (top, Mathf.Max(top, bottom));
            }

            private void ApplyPriority(
                KeepOutPriority priority, float left, float right, ref float top, ref float bottom)
            {
                float middle = _safe.center.y;
                foreach (var zone in _zones)
                {
                    if (zone.Priority != priority) continue;
                    Rect blocker = zone.Rect;
                    if (blocker.width <= 0f || blocker.height <= 0f) continue;
                    if (blocker.xMax <= left || blocker.x >= right) continue;
                    if (blocker.center.y < middle)
                        top = Mathf.Max(top, blocker.yMax + BlockerGap);
                    else
                        bottom = Mathf.Min(bottom, blocker.y - BlockerGap);
                }
            }
        }

        // 本帧要避开的控件。顶栏按钮**恒定预留**（返回键有时不画，但位置照让）：
        // 让位只在某些情况下发生的话，排布就会跟着状态跳来跳去。
        private CardKeepOut BuildCardKeepOut(IEnumerable<ProjectedCardLayout> worldFixedLayouts)
        {
            var zones = new List<KeepOutZone>
            {
                // Bar 占满宽度只是为了排布（见 Layout），交互遮挡只看下面这些真正的控件。
                new(_topHud.Bar, KeepOutPriority.Layout),
                new(_topHud.FunctionPlate(!_gameManager.DisplayedSnapshot.IsInEncounter), KeepOutPriority.Hard),
                new(_topHud.Back, KeepOutPriority.Hard),
                new(_topHud.GrowthToggle, KeepOutPriority.Hard),
                new(_topHud.DossierToggle, KeepOutPriority.Hard),
                new(_topHud.HelpToggle, KeepOutPriority.Hard),
                new(_topHud.SettingsToggle, KeepOutPriority.Hard),
                new(_topHud.DebugToggle, KeepOutPriority.Hard),
                new(_pinStripRect, KeepOutPriority.Hard),
                new(_sceneBandRect, KeepOutPriority.Hard),
            };
            var hardRects = new List<Rect>();
            HandPanelDrawer.CollectTouchBlockers(_gameManager, hardRects);
            foreach (var rect in hardRects)
                zones.Add(new KeepOutZone(rect, KeepOutPriority.Hard));
            var portraits = new List<Rect>();
            HandPanelDrawer.CollectPortraitBounds(_gameManager, portraits);
            foreach (var rect in portraits)
                zones.Add(new KeepOutZone(rect, KeepOutPriority.Portrait));
            foreach (var layout in worldFixedLayouts)
            {
                var center = layout.TargetCenter;
                zones.Add(new KeepOutZone(
                    new Rect(center.x - layout.Width / 2f, center.y - layout.Height / 2f,
                        layout.Width, layout.Height), KeepOutPriority.WorldLabel));
            }
            return new CardKeepOut(UIScale.SafeArea, zones);
        }

        private class ProjectedCardLayout
        {
            public GameNode Node { get; }
            // 地点与锚定标注是固定世界标识；动作卡才参加屏幕空间的避让解算。
            public bool IsAnnotation { get; }
            public bool IsWorldFixed => IsAnnotation || Node.IsContainer;
            public string Key => Node.Name;
            public Vector2 AnchorPos { get; }
            public float Distance { get; }
            // 锚点投影几乎重合时的兜底排序键，见 SolveProjectedStacks。
            public int DeclarationOrder { get; }
            public Vector2 TargetCenter { get; }
            public Vector2 SolvedCenter { get; set; }
            public Vector2 CurrentCenter { get; set; }
            public float Width { get; }
            public float Height { get; }
            // 布局只认卡本体。判定条、结果条与标签等 card attachments 是视觉延伸，
            // 可以覆盖邻卡，却绝不能因出现或消失带动整摞卡重排。
            public float FootprintWidth => Width;
            public float TopExtent => Height / 2f;
            public float BottomExtent => Height / 2f;
            public Rect Rect => new Rect(CurrentCenter.x - Width / 2f, CurrentCenter.y - Height / 2f, Width, Height);

            public ProjectedCardLayout(
                GameNode node,
                Vector2 anchorPos,
                float distance,
                int declarationOrder,
                Vector2 targetCenter,
                Vector2 currentCenter,
                float width,
                float height,
                bool isAnnotation = false)
            {
                Node = node;
                IsAnnotation = isAnnotation;
                AnchorPos = anchorPos;
                Distance = distance;
                DeclarationOrder = declarationOrder;
                TargetCenter = targetCenter;
                SolvedCenter = targetCenter;
                CurrentCenter = currentCenter;
                Width = width;
                Height = height;
            }
        }
    }
}
