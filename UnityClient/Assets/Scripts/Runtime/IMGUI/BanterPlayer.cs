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
            public float Remaining;   // 剩余可见时间
        }

        private const float OverlapSeconds = 0.6f;   // 上一句残留,让"你一句我一句"看得到来回
        private const int MaxQueued = 4;              // 队列上限,溢出丢最旧,避免连续操作后积压

        private readonly DialogueVoicePlayer? _voice;
        private readonly Queue<DialogueSequence> _queue = new();
        private readonly List<Bubble> _visible = new();
        private DialogueSequence? _current;
        private int _index;
        private float _timer;
        private bool _suspended;

        public BanterPlayer(DialogueVoicePlayer? voice) => _voice = voice;

        public IReadOnlyList<Bubble> Visible => _visible;

        public void Enqueue(DialogueSequence sequence)
        {
            if (sequence == null || sequence.Lines.Count == 0)
                throw new System.ArgumentException("banter sequence cannot be empty");
            if (_suspended)
                return;   // 对话聚焦时,杂音直接丢弃
            while (_queue.Count >= MaxQueued)
                _queue.Dequeue();
            _queue.Enqueue(sequence);
        }

        // Conversation 启动:清空并暂停
        public void Suspend()
        {
            _suspended = true;
            _queue.Clear();
            _visible.Clear();
            _current = null;
        }

        // Conversation 结束:恢复接受新台词(旧的已被清掉,不复活)
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
            float dwell = line.DwellSeconds > 0f
                ? line.DwellSeconds
                : Mathf.Clamp(1.2f + line.Text.Length * 0.06f, 1.5f, 5f);
            _timer = dwell;
            _visible.Add(new Bubble { Line = line, Remaining = dwell + OverlapSeconds });
            _voice?.Play(line.VoiceId);
        }
    }
}
