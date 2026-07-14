#nullable enable
using System;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 阻塞对话播放器(客户端独占):点击推进、严格顺序。锁输入/冻结导航由渲染器据 IsActive 处理。
    // 永远是线性的——所有分支选择走 node/Scheme,不进对话播放器。
    public sealed class ConversationPlayer
    {
        private readonly DialogueVoicePlayer? _voice;
        private DialogueSequence? _current;
        private int _index;
        private Action? _onComplete;
        private bool _allowsRemoteParticipants;

        public ConversationPlayer(DialogueVoicePlayer? voice) => _voice = voice;

        public bool IsActive => _current != null;
        public DialogueLine? CurrentLine => _current != null ? _current.Lines[_index] : null;
        // 动作外对话不要求说话人此刻存在于当前场景；前端会为其绘制临时场外卡片。
        public bool AllowsRemoteParticipants => _allowsRemoteParticipants;

        public void Start(DialogueSequence sequence, Action onComplete, bool allowsRemoteParticipants = false)
        {
            if (sequence == null || sequence.Lines.Count == 0)
                throw new ArgumentException("dialogue sequence cannot be empty");
            if (_current != null)
                throw new InvalidOperationException("一段对话尚未结束,不能开始新对话(对话不可重叠)");
            _current = sequence;
            _index = 0;
            _onComplete = onComplete;
            _allowsRemoteParticipants = allowsRemoteParticipants;
            _voice?.Play(CurrentLine!.VoiceId);
        }

        // 由渲染器的全屏点击区命中时调用
        public void Advance()
        {
            if (_current == null)
                return;
            _index++;
            if (_index >= _current.Lines.Count)
            {
                var done = _onComplete;
                _current = null;
                _onComplete = null;
                _allowsRemoteParticipants = false;
                done?.Invoke();
            }
            else
            {
                _voice?.Play(CurrentLine!.VoiceId);
            }
        }

        public void Reset()
        {
            _current = null;
            _index = 0;
            _onComplete = null;
            _allowsRemoteParticipants = false;
        }
    }
}
