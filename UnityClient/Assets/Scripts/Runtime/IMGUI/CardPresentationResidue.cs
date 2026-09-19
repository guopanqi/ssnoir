#nullable enable
using System.Collections.Generic;
using SSNoir.Core;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 一次结算的结果条。挂在宿主卡下面，停几秒自己走，点一下也走；它只是个附件，不锁任何输入。
    /// 宿主在新快照里已经没了（钟归零、次数用完、父节点消失）就画一张它结算后模样的
    /// 残卡替它站着——钟是归零后的钟——残卡随结果条一起走。宿主此刻不在屏幕上（换了场景、
    /// 镜头已经走了）就画成屏幕中央的浮条。
    /// </summary>
    public sealed class CardPresentationResidue
    {
        public string HostNodeName { get; set; } = string.Empty;
        public RollOutcome? RollOutcome { get; set; }
        public int? FateDieValue { get; set; }
        public int PreparedValue { get; set; }
        public List<ActionEffectRecord> Effects { get; set; } = new List<ActionEffectRecord>();
        // 结算后的钟。宿主卡画的还是旧快照，钟要按结果显示，不然「推了一格」只写在效果行里，
        // 卡上的钟还停在推之前。空表示没有对应节点（随身卡、休息）。
        public List<GameClock>? SettledClocks { get; set; }
        // 宿主在屏幕底部（随身卡、休息键）时结果条往上挂，往下就出屏了。
        public bool AttachAbove { get; set; }

        // 结算前那张宿主卡。宿主从新快照消失后，用它加 SettledClocks 画残卡。
        public GameNode? SourceNode { get; set; }
        // 残卡站的位置：结算那一帧宿主画在哪。有锚点就跟着锚点走（GhostOffset 是
        // 卡相对锚点投影的偏移），镜头动了它也不会飘在半空。
        public Rect? GhostRect { get; set; }
        public Transform? GhostAnchor { get; set; }
        public Vector2 GhostOffset { get; set; }

        // 结果首次可见（动画落定、切到 residue）的时刻；用于「结果从命运条下方揭开」的过渡。首帧惰性写入。
        public float RevealStartTime { get; set; } = 0f;

        // 烧绳子：停留进入倒计时后从 1 烧到 0，烧完结果条连同旧快照一起收掉。
        // 没进倒计时（对白还在放）时不烧。
        public float FuseStartedAt { get; set; } = -1f;
        public float FuseSeconds { get; set; }
        public float FuseRemaining01 => FuseStartedAt < 0f || FuseSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(1f - (Time.unscaledTime - FuseStartedAt) / FuseSeconds);
        public bool FuseBurntOut => FuseStartedAt >= 0f && Time.unscaledTime - FuseStartedAt >= FuseSeconds;
    }
}
