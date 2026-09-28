#nullable enable
using System.Collections.Generic;
using SSNoir.Core;
using UnityEngine;

namespace SSNoir.IMGUI.Stage
{
    // 舞台的模型：台上有谁、每个人的终态、画面处于什么状态。不画任何东西。
    //
    // 所有会变的量都是「From / Target / ChangedAt」三件套，或者一个事件时刻；读取的时候给一个 now，
    // 得到此刻的插值。这样跳过动画（把 now 推到很远）和慢放得到的是同一组终态，
    // 存档、快进、截图都不用另写一套逻辑。时间由调用方注入，模型自己不读时钟。
    public sealed class StageState
    {
        public const string Protagonist = "尼尔";
        public const string BasePose = "基础";

        // 过渡的时长与幅度。听者不是灭掉而是压暗：他还在场，只是这句不是他的。
        public const float EnterDuration = 0.20f;
        public const float PoseSwapDuration = 0.32f;
        public const float GlowDuration = 0.45f;
        public const float MoveDuration = 0.42f;
        public const float ShakeDuration = 0.36f;
        public const float RelightDuration = 0.70f;
        public const float BlackoutHold = 0.45f;
        public const float BlackoutRecover = 0.25f;
        public const float FlashDuration = 0.26f;
        public const float NegativeFade = 0.18f;
        public const float ListenerLevel = 0.36f;
        public const float MoveInOffset = 220f;
        public const float MoveBackOffset = -70f;

        // :light 的三种状态不是三个亮度数，是三种灯（画法见 LampPainter）；这里只记档位值，
        // 档位之间连续插值，所以两句之间的过渡不会跳。
        public const float GlowFaint = 0.55f;
        public const float GlowEmber = 0.50f;
        public const float GlowSurge = 1.35f;
        // surge 进入的头一瞬灯管过冲：比稳定值再亮一截，然后落回。
        public const float SurgePopDuration = 0.18f;
        // 说话人 surge 时，听者被压得更暗——谁的光占屏幕谁占上风。
        public const float DominanceDim = 0.40f;
        // 电流：pulse 是缓慢呼吸，racing 是狂飙。速度以每秒走过整副骨架长度的比例计，Sparks 是同时在线的
        // 彗星数，Tail 是每道彗星身后点亮的管子长度（uv 单位；整副骨架总长约 5）。
        public const float PulseSpeed = 0.10f, PulseSparks = 2f, PulseTail = 0.35f;
        public const float RacingSpeed = 0.9f, RacingSparks = 6f, RacingTail = 0.55f;
        public const float CurrentDuration = 0.6f;
        // 标志色变化的过渡：剧情改了颜色，几秒里染过去，不是一帧换掉。
        public const float AccentDuration = 2.5f;

        // 闪烁：灯管接触不良。不是均匀地抖，是一段写死的节奏——亮着，突然死掉一小段，
        // 打火时猛地过亮一下（>1 由光晕吃掉），再死，半亮，回来，又掉一次，最后才稳住。
        // 间隔要够长、死得要够黑，隔着一段距离也看得出「这盏灯坏了」。
        private static readonly (float seconds, float level)[] FlickerPattern =
        {
            (0.22f, 1.00f), (0.14f, 0.05f), (0.05f, 1.60f), (0.20f, 0.05f), (0.16f, 0.45f),
            (0.30f, 1.00f), (0.10f, 0.05f), (0.04f, 1.50f), (0.12f, 0.05f), (0.24f, 0.70f),
            (0.20f, 1.00f), (0.06f, 0.10f), (0.20f, 1.00f),
        };
        public static readonly float FlickerDuration = SumFlicker();

        private readonly Dictionary<string, StageActor> _actors = new();
        private readonly Dictionary<string, StageProp> _props = new();
        private readonly List<StageEffect> _effects = new();
        private string _rightActor = string.Empty;
        private float _blackoutAt = -10f;
        private float _flashAt = -10f;
        private bool _negative;
        private float _negativeChangedAt = -10f;

        public IEnumerable<StageActor> Actors => _actors.Values;
        public IEnumerable<StageProp> Props => _props.Values;
        public IEnumerable<StageEffect> Effects => _effects;
        public bool TryGetActor(string name, out StageActor actor) => _actors.TryGetValue(name, out actor!);

        public void Spawn(string id, string asset, float x, float y, string layer, float now, System.Func<string, Texture2D?> loadPortrait)
        {
            if (_actors.ContainsKey(id) || _props.ContainsKey(id)) throw new System.InvalidOperationException("stage entity already exists: " + id);
            var texture = loadPortrait(asset) ?? throw new System.InvalidOperationException("stage portrait missing: " + asset);
            var actor = new StageActor(id, x < 0f, now, texture) { AssetName = asset, StageLayer = layer };
            actor.SetStageX(x, 0f, now);
            actor.SetStageY(y, 0f, now);
            _actors.Add(id, actor);
        }

        public void SpawnProp(string id, string asset, float x, float y, string layer, float now)
        {
            if (_actors.ContainsKey(id) || _props.ContainsKey(id)) throw new System.InvalidOperationException("stage entity already exists: " + id);
            var texture = Resources.Load<Texture2D>("StageProps/" + asset)
                ?? throw new System.InvalidOperationException("stage prop missing: " + asset);
            _props.Add(id, new StageProp(id, texture, x, y, layer));
        }

        public void SpawnPropAt(string id, string asset, string anchor, float dx, float dy, string layer, float now)
        {
            var position = EntityPosition(anchor, now);
            SpawnProp(id, asset, position.X + dx, position.Y + dy, layer, now);
        }

        private StagePoint EntityPosition(string id, float now)
        {
            if (_actors.TryGetValue(id, out var actor)) return new StagePoint(actor.CurrentStageX(now), actor.CurrentStageY(now));
            if (_props.TryGetValue(id, out var prop)) return new StagePoint(prop.X(now), prop.Y(now));
            throw new System.InvalidOperationException("stage entity missing: " + id);
        }

        public void Move(string id, float x, float seconds, float now)
        {
            Move(id, x, float.NaN, seconds, now);
        }

        public void Move(string id, float x, float y, float seconds, float now)
        {
            if (_actors.TryGetValue(id, out var actor))
            {
                actor.SetStagePosition(x, float.IsNaN(y) ? actor.CurrentStageY(now) : y, seconds, now);
            }
            else if (_props.TryGetValue(id, out var prop)) prop.Move(x, float.IsNaN(y) ? prop.Y(now) : y, seconds, now);
            else throw new System.InvalidOperationException("stage entity missing: " + id);
        }

        public void Path(string id, float seconds, IReadOnlyList<StagePoint> points, bool relative, float now)
        {
            if (relative)
            {
                var origin = EntityPosition(id, now);
                var absolute = new StagePoint[points.Count];
                for (int i = 0; i < points.Count; i++)
                    absolute[i] = new StagePoint(origin.X + points[i].X, origin.Y + points[i].Y);
                points = absolute;
            }
            if (_actors.TryGetValue(id, out var actor)) actor.SetStagePath(points, seconds, now);
            else if (_props.TryGetValue(id, out var prop)) prop.Path(points, seconds, now);
            else throw new System.InvalidOperationException("stage entity missing: " + id);
        }

        public void Effect(string id, string effect, float dx, float dy, float now)
        {
            float x, y;
            if (_actors.TryGetValue(id, out var actor)) { x = actor.CurrentStageX(now); y = actor.CurrentStageY(now); }
            else if (_props.TryGetValue(id, out var prop)) { x = prop.X(now); y = prop.Y(now); }
            else throw new System.InvalidOperationException("stage entity missing: " + id);
            _effects.RemoveAll(e => now - e.StartedAt > StageEffect.Duration);
            _effects.Add(new StageEffect(effect, x + dx, y + dy, now));
        }

        public void Remove(string id)
        {
            if (!_actors.Remove(id) && !_props.Remove(id)) throw new System.InvalidOperationException("stage entity missing: " + id);
        }

        public void Pose(string id, string pose, float now, System.Func<string, string, Texture2D?> loadPose)
        {
            if (!_actors.TryGetValue(id, out var actor)) throw new System.InvalidOperationException("stage actor missing: " + id);
            var texture = loadPose(actor.AssetName, pose) ?? throw new System.InvalidOperationException("stage pose missing: " + actor.AssetName + "_" + pose);
            actor.SwitchPose(pose, texture, now, 0.12f);
        }

        public void Light(string id, string state, float now)
        {
            if (!_actors.TryGetValue(id, out var actor)) throw new System.InvalidOperationException("stage actor missing: " + id);
            actor.SetGlow(state switch { "faint" => GlowFaint, "ember" => GlowEmber, "surge" => GlowSurge, _ => 1f }, now);
            actor.SetWarmth(state == "ember" ? 1f : 0f, now);
        }

        public void Reset()
        {
            _actors.Clear();
            _props.Clear();
            _effects.Clear();
            _rightActor = string.Empty;
            _blackoutAt = -10f;
            _flashAt = -10f;
            _negative = false;
            _negativeChangedAt = -10f;
        }

        // 一句新台词落下：画面指示旁白也能下（它管整个屏幕），人物指示只落到说话人身上。
        // loadPortrait / loadPose 由调用方提供，模型不碰 Resources。
        public void ApplyLine(
            string speaker,
            bool isNarration,
            DialogueStageCue cue,
            float now,
            System.Func<string, Texture2D?> loadPortrait,
            System.Func<string, string, Texture2D?> loadPose,
            System.Action<string> warnMissing)
        {
            if (cue.Screen != null)
            {
                bool negative = cue.Screen == "negative";
                if (negative != _negative)
                {
                    _negative = negative;
                    _negativeChangedAt = now;
                }
            }
            if (cue.Flash) _flashAt = now;
            if (cue.Blackout) _blackoutAt = now;
            if (isNarration) return;

            bool justEntered = false;
            if (!_actors.TryGetValue(speaker, out var actor))
            {
                justEntered = true;
                var baseTexture = loadPortrait(speaker);
                actor = new StageActor(speaker, speaker == Protagonist, now, baseTexture);
                if (actor.Missing) warnMissing(speaker);
                _actors[speaker] = actor;
            }

            // 右边只站一个人：新来的对手把上一个顶下台。主角永远在左。
            if (!actor.OnLeft && _rightActor != speaker)
            {
                if (_rightActor.Length > 0)
                    _actors.Remove(_rightActor);
                _rightActor = speaker;
                actor.EnteredAt = now;
            }

            ApplyActorCue(actor, cue, now, justEntered, loadPortrait, loadPose, warnMissing);

            // :other 落到台上另一个人身上。他还没上台就没人可指，只报一声。
            if (cue.Other != null)
            {
                StageActor? other = null;
                foreach (var candidate in _actors.Values)
                    if (candidate.Name != speaker) other = candidate;
                if (other == null)
                    warnMissing(speaker + " 的 :other（台上没有别人）");
                else
                    ApplyActorCue(other, cue.Other, now, false, loadPortrait, loadPose, warnMissing);
            }
        }

        private static void ApplyActorCue(
            StageActor actor,
            DialogueStageCue cue,
            float now,
            bool justEntered,
            System.Func<string, Texture2D?> loadPortrait,
            System.Func<string, string, Texture2D?> loadPose,
            System.Action<string> warnMissing)
        {
            if (cue.Pose != null && cue.Pose != actor.Pose && !actor.Missing)
            {
                var poseTexture = cue.Pose == BasePose ? loadPortrait(actor.Name) : loadPose(actor.Name, cue.Pose);
                if (poseTexture == null)
                    warnMissing(actor.Name + "_" + cue.Pose);
                else
                    actor.SwitchPose(cue.Pose, poseTexture, justEntered ? -10f : now);
            }

            if (cue.Light != null)
            {
                actor.SetGlow(cue.Light switch { "faint" => GlowFaint, "ember" => GlowEmber, "surge" => GlowSurge, _ => 1f }, now);
                actor.SetWarmth(cue.Light == "ember" ? 1f : 0f, now);
            }
            if (cue.Current != null)
                actor.SetCurrent(cue.Current switch { "pulse" => 1f, "racing" => 2f, _ => 0f }, now);
            if (cue.Move != null)
                actor.SetOffset(cue.Move == "in" ? MoveInOffset : MoveBackOffset, now);
            if (cue.Shake) actor.ShakeAt = now;
            if (cue.Flicker) actor.FlickerAt = now;
            if (cue.Relight) actor.RelightAt = now;
        }

        // ── 画面 ──

        // 负片程度 0..1：0 是黑底白管，1 是白底黑管。切换时短暂过渡，不是一帧翻过去——
        // 翻过去那一下由 flash 去做，两个可以叠着用。
        public float NegativeAmount(float now)
        {
            float t = Mathf.Clamp01((now - _negativeChangedAt) / NegativeFade);
            return _negative ? t : 1f - t;
        }

        // 白闪的剩余强度，0 表示没有在闪。
        public float FlashAlpha(float now)
        {
            float t = now - _flashAt;
            if (t < 0f || t >= FlashDuration) return 0f;
            return 1f - Mathf.Pow(t / FlashDuration, 0.6f);
        }

        // 黑场：整台亮度的乘数。黑住那一拍是 0，然后所有灯一起回来。
        public float BlackoutLevel(float now)
        {
            float t = now - _blackoutAt;
            if (t < 0f) return 1f;
            if (t < BlackoutHold) return 0f;
            return Mathf.Clamp01((t - BlackoutHold) / BlackoutRecover);
        }

        // ── 人物 ──

        public static float FlickerLevel(StageActor actor, float now) => FlickerLevelAt(now - actor.FlickerAt);

        // 闪烁开始后 t 秒的亮度；声音合成也读它，画面和声音是同一张节奏表。
        public static float FlickerLevelAt(float t)
        {
            if (t < 0f || t >= FlickerDuration) return 1f;
            foreach (var step in FlickerPattern)
            {
                if (t < step.seconds) return step.level;
                t -= step.seconds;
            }
            return 1f;
        }

        // 闪烁进行到第几拍（-1 = 不在闪烁）。画面用它决定这一拍死的是哪几根管子。
        public static int FlickerStep(StageActor actor, float now)
        {
            float t = now - actor.FlickerAt;
            if (t < 0f || t >= FlickerDuration) return -1;
            for (int i = 0; i < FlickerPattern.Length; i++)
            {
                if (t < FlickerPattern[i].seconds) return i;
                t -= FlickerPattern[i].seconds;
            }
            return -1;
        }

        // 死拍里整块招牌不是全黑，而是大半管子灭、少数还亮着：接触不良是管子的事，不是电闸的事。
        public const float FlickerDeadLevel = 0.15f;
        public const float FlickerSurvivorLevel = 0.80f;
        public const float FlickerDeadShare = 0.70f;

        // 一震：幅度先大后小的快速左右抖动。
        public static float ShakeOffset(StageActor actor, float now)
        {
            float t = now - actor.ShakeAt;
            if (t < 0f || t >= ShakeDuration) return 0f;
            float decay = 1f - t / ShakeDuration;
            return Mathf.Sin(t * 70f) * 14f * decay * decay;
        }

        // 残影强度：震得最厉害的头几帧最重，跟着抖动一起衰减。
        public static float ShakeGhost(StageActor actor, float now)
        {
            float t = now - actor.ShakeAt;
            if (t < 0f || t >= ShakeDuration) return 0f;
            float decay = 1f - t / ShakeDuration;
            return decay * decay;
        }

        // 燃：0 是全灭，1 是全亮；中间是从脚到头扫上来的进度。
        public static float RelightProgress(StageActor actor, float now)
        {
            float t = now - actor.RelightAt;
            if (t < 0f || t >= RelightDuration) return 1f;
            return t / RelightDuration;
        }

        public static float EaseOut(float changedAt, float duration, float now)
        {
            float t = Mathf.Clamp01((now - changedAt) / duration);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float SumFlicker()
        {
            float total = 0f;
            foreach (var step in FlickerPattern) total += step.seconds;
            return total;
        }
    }

    // 台上一个人物的全部状态。
    public sealed class StageActor
    {
        public string Name { get; }
        public string AssetName { get; set; } = string.Empty;
        public string StageLayer { get; set; } = "middle";
        public bool UsesStageX { get; private set; }
        public bool OnLeft { get; }
        public bool Missing { get; }         // 连基础立绘都没有：画缺图人物牌
        public float EnteredAt;

        public Texture2D? Texture { get; private set; }           // 当前姿势的灯管
        public Texture2D? PreviousTexture { get; private set; }   // 上一姿势，正在灭
        public string Pose { get; private set; } = string.Empty;
        public float PoseChangedAt { get; private set; } = -10f;
        private float _poseSwapDuration = StageState.PoseSwapDuration;

        private float _glowFrom = 1f, _glowTarget = 1f, _glowChangedAt = -10f;
        private float _warmthFrom, _warmthTarget, _warmthChangedAt = -10f;
        private float _currentFrom, _currentTarget, _currentChangedAt = -10f;
        // 标志色：From/Target/ChangedAt 三件套，目标由外面（剧情）给。
        private Color _accentFrom, _accentTarget;
        private float _accentChangedAt = -10f;
        private bool _accentSet;
        private float _offsetFrom, _offsetTarget, _offsetChangedAt = -10f;
        private readonly StageMotion _stageMotion = new();
        public float ShakeAt = -10f;
        public float FlickerAt = -10f;
        public float RelightAt = -10f;

        public StageActor(string name, bool onLeft, float now, Texture2D? baseTexture)
        {
            Name = name;
            AssetName = name;
            OnLeft = onLeft;
            EnteredAt = now;
            Texture = baseTexture;
            Missing = baseTexture == null;
        }

        public void SetStageX(float target, float seconds, float now)
        {
            _stageMotion.Move(target, _stageMotion.Y(now), seconds, now);
            UsesStageX = true;
        }

        public void SetStagePosition(float x, float y, float seconds, float now)
        {
            _stageMotion.Move(x, y, seconds, now);
            UsesStageX = true;
        }

        public void SetStageY(float target, float seconds, float now)
        {
            _stageMotion.Move(_stageMotion.X(now), target, seconds, now);
            UsesStageX = true;
        }

        public void SetStagePath(IReadOnlyList<StagePoint> points, float seconds, float now)
        {
            _stageMotion.Path(points, seconds, now);
            UsesStageX = true;
        }

        // 舞台走位是匀速直线：跑步进出场用 EaseOut 会前冲后爬、越跑越慢。
        // 灯光、姿势、逼近退位仍走 EaseOut——那些是亮灯和做派，不是赶路。
        public float CurrentStageX(float now) => _stageMotion.X(now);
        public float CurrentStageY(float now) => _stageMotion.Y(now);

        // 刚上台的人传 changedAt = -10：直接以指定姿势点亮，不先闪一下招牌姿势再切过去。
        public void SwitchPose(string pose, Texture2D texture, float changedAt, float swapDuration = StageState.PoseSwapDuration)
        {
            PreviousTexture = changedAt < 0f ? null : Texture;
            Texture = texture;
            Pose = pose;
            PoseChangedAt = changedAt;
            _poseSwapDuration = swapDuration;
        }

        // 过渡从当前插值位置起算，连续两句都在动也不会跳。
        public void SetGlow(float target, float now)
        {
            _glowFrom = CurrentGlow(now);
            _glowTarget = target;
            _glowChangedAt = now;
        }

        public void SetOffset(float target, float now)
        {
            _offsetFrom = CurrentOffset(now);
            _offsetTarget = target;
            _offsetChangedAt = now;
        }

        public float CurrentGlow(float now) =>
            Mathf.Lerp(_glowFrom, _glowTarget, StageState.EaseOut(_glowChangedAt, StageState.GlowDuration, now));

        // surge 刚进入那一瞬的过冲，之后为 0。
        public float SurgePop(float now)
        {
            if (_glowTarget < StageState.GlowSurge) return 0f;
            float t = now - _glowChangedAt;
            if (t < 0f || t >= StageState.SurgePopDuration) return 0f;
            return 1f - t / StageState.SurgePopDuration;
        }

        public void SetWarmth(float target, float now)
        {
            _warmthFrom = CurrentWarmth(now);
            _warmthTarget = target;
            _warmthChangedAt = now;
        }

        public float CurrentWarmth(float now) =>
            Mathf.Lerp(_warmthFrom, _warmthTarget, StageState.EaseOut(_warmthChangedAt, StageState.GlowDuration, now));

        // 电流档：0 不走，1 pulse，2 racing；中间是过渡。
        public void SetCurrent(float target, float now)
        {
            _currentFrom = CurrentCurrent(now);
            _currentTarget = target;
            _currentChangedAt = now;
        }

        public float CurrentCurrent(float now) =>
            Mathf.Lerp(_currentFrom, _currentTarget, StageState.EaseOut(_currentChangedAt, StageState.CurrentDuration, now));

        // 标志色的目标由剧情决定；每帧把最新目标喂进来，变了就开始过渡。
        public void TargetAccent(Color target, float now)
        {
            if (!_accentSet)
            {
                _accentSet = true;
                _accentFrom = _accentTarget = target;
                return;
            }
            if (_accentTarget == target) return;
            _accentFrom = CurrentAccent(now);
            _accentTarget = target;
            _accentChangedAt = now;
        }

        public Color CurrentAccent(float now) =>
            Color.Lerp(_accentFrom, _accentTarget, StageState.EaseOut(_accentChangedAt, StageState.AccentDuration, now));

        public float CurrentOffset(float now) =>
            Mathf.Lerp(_offsetFrom, _offsetTarget, StageState.EaseOut(_offsetChangedAt, StageState.MoveDuration, now));

        // 换姿势的进度；到 1 时把旧姿势放掉。
        public float PoseSwap(float now)
        {
            float swap = StageState.EaseOut(PoseChangedAt, _poseSwapDuration, now);
            if (swap >= 1f) PreviousTexture = null;
            return swap;
        }
    }

    // 人物和道具共用位置曲线；Bézier 的首尾控制点就是起点与终点。
    public sealed class StageMotion
    {
        private StagePoint[] _points = { new StagePoint(0f, 0f), new StagePoint(0f, 0f) };
        private float _startedAt;
        private float _duration;

        public void Move(float x, float y, float seconds, float now)
            => Path(new[] { new StagePoint(X(now), Y(now)), new StagePoint(x, y) }, seconds, now);

        public void Path(IReadOnlyList<StagePoint> points, float seconds, float now)
        {
            _points = new StagePoint[points.Count];
            for (int i = 0; i < points.Count; i++) _points[i] = points[i];
            _startedAt = now;
            _duration = seconds;
        }

        public float X(float now) => Position(now).X;
        public float Y(float now) => Position(now).Y;

        private StagePoint Position(float now)
        {
            float t = _duration <= 0f ? 1f : Mathf.Clamp01((now - _startedAt) / _duration);
            var work = (StagePoint[])_points.Clone();
            for (int remaining = work.Length - 1; remaining > 0; remaining--)
                for (int i = 0; i < remaining; i++)
                    work[i] = new StagePoint(Mathf.Lerp(work[i].X, work[i + 1].X, t),
                        Mathf.Lerp(work[i].Y, work[i + 1].Y, t));
            return work[0];
        }
    }

    public sealed class StageProp
    {
        private readonly StageMotion _motion = new();
        public string Id { get; }
        public Texture2D Texture { get; }
        public string Layer { get; }
        public StageProp(string id, Texture2D texture, float x, float y, string layer)
        {
            Id = id; Texture = texture; Layer = layer;
            _motion.Move(x, y, 0f, 0f);
        }
        public float X(float now) => _motion.X(now);
        public float Y(float now) => _motion.Y(now);
        public void Move(float x, float y, float seconds, float now) => _motion.Move(x, y, seconds, now);
        public void Path(IReadOnlyList<StagePoint> points, float seconds, float now) => _motion.Path(points, seconds, now);
    }

    public sealed class StageEffect
    {
        public const float Duration = 0.20f;
        public string Name { get; }
        public float X { get; }
        public float Y { get; }
        public float StartedAt { get; }
        public StageEffect(string name, float x, float y, float startedAt)
        { Name = name; X = x; Y = y; StartedAt = startedAt; }
    }
}
