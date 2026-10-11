#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 非阻塞插话播放器(客户端独占):按序逐句、计时自动推进,不锁输入、不冻结导航。
    // 计时与可见气泡状态都在前端,引擎不参与(只通过 ActionReport / DialogueCenter 投递请求)。
    public sealed class BanterPlayer
    {
        public sealed class Bubble
        {
            public DialogueLine Line = null!;
            public bool AllowsRemoteParticipants;
            // 普通 play-bubble! 锚定失败时，画面会降级为场外卡并发一次内容警告。
            // 状态跟着气泡走，避免 OnGUI 每帧重复推送同一条警告。
            public bool RemoteFallbackWarningIssued;
            public bool PreferCardAnchor;   // 对方回合的节拍：落在说话人的卡上，不找世界锚点
            public float Remaining;   // 剩余可见时间
        }

        private const float OverlapSeconds = 0.6f;   // 上一句残留,让"你一句我一句"看得到来回
        private const float VoiceTailSeconds = 0.15f; // 留出音频播放结束的极短尾部,避免切字
        private const int MaxQueued = 4;              // 队列上限,溢出丢最旧,避免连续操作后积压
        private static readonly IReadOnlyList<Bubble> NoVisibleBubbles = System.Array.Empty<Bubble>();

        private readonly DialogueVoicePlayer? _voice;
        private readonly Queue<DialogueSequence> _queue = new();
        private readonly List<Bubble> _visible = new();
        private DialogueSequence? _current;
        private int _index;
        private float _timer;
        private bool _suspended;

        public BanterPlayer(DialogueVoicePlayer? voice) => _voice = voice;

        public IReadOnlyList<Bubble> Visible => _suspended ? NoVisibleBubbles : _visible;

        public void Enqueue(DialogueSequence sequence)
        {
            if (sequence == null || sequence.Lines.Count == 0)
                throw new System.ArgumentException("banter sequence cannot be empty");
            while (_queue.Count >= MaxQueued)
                _queue.Dequeue();
            _queue.Enqueue(sequence);
        }

        /// <summary>
        /// 对方回合的节拍台词：不排队、立刻可见、落在说话人的卡上。同一拍里的几句一起冒出来。
        /// 返回最长的一句要停多久；气泡本身会比这个多留一小段（OverlapSeconds），
        /// 好让批快照落地时钟和冷静条的脉冲发生在气泡还挂着的时候——话和后果连在一起。
        /// </summary>
        public float ShowBeatLines(IReadOnlyList<DialogueLine> lines)
        {
            float longest = 0f;
            foreach (var line in lines)
            {
                float dwell = line.DwellSeconds > 0f
                    ? line.DwellSeconds
                    : Mathf.Clamp(1.2f + line.Text.Length * 0.06f, 1.5f, 5f);
                longest = Mathf.Max(longest, dwell);
                _visible.Add(new Bubble
                {
                    Line = line,
                    AllowsRemoteParticipants = false,
                    PreferCardAnchor = true,
                    Remaining = dwell + OverlapSeconds,
                });
            }
            return longest;
        }

        // Conversation 启动:隐藏并冻结当前气泡；当前序列和等待队列都保留。
        public void Suspend()
        {
            _suspended = true;
        }

        // Conversation 结束:从原来的计时位置继续。
        public void Resume() => _suspended = false;

        // 场景重置:彻底清空
        public void Reset()
        {
            _suspended = false;
            _queue.Clear();
            _visible.Clear();
            _current = null;
            _index = 0;
            _timer = 0f;
        }

        public void Update(float dt)
        {
            if (_suspended)
                return;

            for (int i = _visible.Count - 1; i >= 0; i--)
            {
                _visible[i].Remaining -= dt;
                if (_visible[i].Remaining <= 0f)
                    _visible.RemoveAt(i);
            }

            if (_current == null && _queue.Count > 0)
            {
                _current = _queue.Dequeue();
                _index = 0;
                ShowCurrentLine();
                return;
            }

            if (_current == null)
                return;

            _timer -= dt;
            if (_timer <= 0f)
            {
                _index++;
                if (_index >= _current.Lines.Count)
                    _current = null;   // 序列结束;残留气泡靠重叠时间自然消失
                else
                    ShowCurrentLine();
            }
        }

        private void ShowCurrentLine()
        {
            var line = _current!.Lines[_index];
            float textDwell = line.DwellSeconds > 0f
                ? line.DwellSeconds
                : Mathf.Clamp(1.2f + line.Text.Length * 0.06f, 1.5f, 5f);
            float voiceDuration = _voice?.Play(line.VoiceId) ?? 0f;
            // 配音是停留时间的下限。显式 dwell 仍可延长气泡，但不能把音频截断。
            float dwell = Mathf.Max(textDwell, voiceDuration + VoiceTailSeconds);
            _timer = dwell;
            _visible.Add(new Bubble
            {
                Line = line,
                AllowsRemoteParticipants = _current.AllowsRemoteParticipants,
                Remaining = dwell + OverlapSeconds,
            });
        }
    }
}
