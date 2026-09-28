#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Core;
using UnityEngine;

namespace SSNoir.IMGUI
{
    // One timeline owns the stage for its whole lifetime. Timed commands in a beat start together;
    // dialogue alone waits for input. Audio starts on the beat and does not extend it.
    public sealed class StoryStagePlayer
    {
        private readonly DialogueVoicePlayer _voice;
        private readonly AudioSource _sound;
        private readonly List<AudioSource> _soundChannels = new();
        private StoryStageSequence? _sequence;
        private Action? _done;
        private int _index;
        private float _beatEndsAt;
        private bool _waitingForSay;

        public StoryStagePlayer(DialogueVoicePlayer voice, AudioSource sound)
        {
            _voice = voice;
            _sound = sound;
            _sound.playOnAwake = false;
            _sound.spatialBlend = 0f;
            _soundChannels.Add(_sound);
        }

        public bool IsActive => _sequence != null;
        public int BeatIndex => _index;
        public DialogueLine? CurrentLine
        {
            get
            {
                if (!_waitingForSay || _sequence == null) return null;
                return _sequence.Beats[_index].Commands[0].Line;
            }
        }

        public void Start(StoryStageSequence sequence, Action done)
        {
            if (_sequence != null) throw new InvalidOperationException("stage already playing");
            _sequence = sequence;
            _done = done;
            _index = 0;
            StoryStageDrawer.BeginConversation();
            BeginBeat();
        }

        public void Update()
        {
            foreach (var channel in _soundChannels)
                channel.volume = AudioVolumes.Sfx;
            if (_sequence != null && !_waitingForSay && Time.unscaledTime >= _beatEndsAt)
                AdvanceBeat();
        }

        public void Advance()
        {
            if (_sequence == null) return;
            // 无字动作拍自动推进：点击和 ESC 都不跳过，播完由 Update 按 _beatEndsAt 自动进下一拍。
            // 播动画时点一下就跳到终点，在舞台上是穿帮——Say 拍才等输入（打字机补全→下一句）。
            if (!_waitingForSay)
                return;
            if (!StoryStageDrawer.IsCurrentLineFullyRevealed)
                StoryStageDrawer.CompleteCurrentLine();
            else
                AdvanceBeat();
        }

        public void Reset()
        {
            _sequence = null;
            _done = null;
            foreach (var channel in _soundChannels) channel.Stop();
            StoryStageDrawer.EndConversation();
        }

        private void AdvanceBeat()
        {
            if (_sequence == null) return;
            _index++;
            if (_index < _sequence.Beats.Count)
            {
                BeginBeat();
                return;
            }
            var done = _done;
            Reset();
            done?.Invoke();
        }

        private void BeginBeat()
        {
            if (_sequence == null) return;
            float longest = 0f;
            _waitingForSay = false;
            foreach (var command in _sequence.Beats[_index].Commands)
            {
                switch (command.Kind)
                {
                    case StoryStageCommandKind.Spawn:
                        StoryStageDrawer.StageSpawn(command.Id, command.Asset, command.X, command.Y, command.Layer);
                        break;
                    case StoryStageCommandKind.Prop:
                        StoryStageDrawer.StageProp(command.Id, command.Asset, command.X, command.Y, command.Layer);
                        break;
                    case StoryStageCommandKind.PropAt:
                        StoryStageDrawer.StagePropAt(command.Id, command.Asset, command.Anchor, command.X, command.Y, command.Layer);
                        break;
                    case StoryStageCommandKind.Move:
                        StoryStageDrawer.StageMove(command.Id, command.X, command.Y, command.Seconds);
                        longest = Mathf.Max(longest, command.Seconds);
                        break;
                    case StoryStageCommandKind.Path:
                        StoryStageDrawer.StagePath(command.Id, command.Points, command.Seconds, command.Relative);
                        longest = Mathf.Max(longest, command.Seconds);
                        break;
                    case StoryStageCommandKind.Remove:
                        StoryStageDrawer.StageRemove(command.Id);
                        break;
                    case StoryStageCommandKind.Pose:
                        StoryStageDrawer.StagePose(command.Id, command.Asset);
                        break;
                    case StoryStageCommandKind.Light:
                        StoryStageDrawer.StageLight(command.Id, command.Asset);
                        break;
                    case StoryStageCommandKind.Effect:
                        StoryStageDrawer.StageEffect(command.Id, command.Asset, command.X, command.Y);
                        break;
                    case StoryStageCommandKind.Sound:
                        var clip = Resources.Load<AudioClip>("StageSounds/" + command.Asset)
                            ?? throw new InvalidOperationException("stage sound missing: " + command.Asset);
                        PlaySound(clip, command.X);
                        break;
                    case StoryStageCommandKind.Say:
                        if (command.Line == null) throw new InvalidOperationException("stage say missing line");
                        _waitingForSay = true;
                        StoryStageDrawer.BeginStageLine(command.Line, _index);
                        _voice.Play(command.Line.VoiceId);
                        break;
                    case StoryStageCommandKind.Pause:
                        longest = Mathf.Max(longest, command.Seconds);
                        break;
                }
            }
            _beatEndsAt = Time.unscaledTime + longest;
        }

        private void PlaySound(AudioClip clip, float x)
        {
            AudioSource? channel = null;
            foreach (var candidate in _soundChannels)
                if (!candidate.isPlaying) { channel = candidate; break; }
            if (channel == null)
            {
                channel = _sound.gameObject.AddComponent<AudioSource>();
                channel.playOnAwake = false;
                channel.spatialBlend = 0f;
                _soundChannels.Add(channel);
            }
            channel.panStereo = Mathf.Clamp(x / 10f, -1f, 1f);
            channel.volume = AudioVolumes.Sfx;
            channel.clip = clip;
            channel.Play();
        }
    }
}
