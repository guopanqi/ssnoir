#nullable enable
using System;
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
            if (_sequence != null && !_waitingForSay && Time.unscaledTime >= _beatEndsAt)
                AdvanceBeat();
        }

        public void Advance()
        {
            if (_sequence == null) return;
            if (!_waitingForSay)
            {
                foreach (var command in _sequence.Beats[_index].Commands)
                    if (command.Kind == StoryStageCommandKind.Move)
                        StoryStageDrawer.StageMove(command.Id, command.X, 0f);
                AdvanceBeat();
                return;
            }
            if (!StoryStageDrawer.IsCurrentLineFullyRevealed)
                StoryStageDrawer.CompleteCurrentLine();
            else
                AdvanceBeat();
        }

        public void Reset()
        {
            _sequence = null;
            _done = null;
            _sound.Stop();
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
                        StoryStageDrawer.StageSpawn(command.Id, command.Asset, command.X, command.Layer);
                        break;
                    case StoryStageCommandKind.Move:
                        StoryStageDrawer.StageMove(command.Id, command.X, command.Seconds);
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
                    case StoryStageCommandKind.Sound:
                        var clip = Resources.Load<AudioClip>("StageSounds/" + command.Asset)
                            ?? throw new InvalidOperationException("stage sound missing: " + command.Asset);
                        _sound.panStereo = Mathf.Clamp(command.X / 10f, -1f, 1f);
                        _sound.PlayOneShot(clip);
                        break;
                    case StoryStageCommandKind.Say:
                        if (command.Line == null) throw new InvalidOperationException("stage say missing line");
                        _waitingForSay = true;
                        _voice.Play(command.Line.VoiceId);
                        break;
                    case StoryStageCommandKind.Pause:
                        longest = Mathf.Max(longest, command.Seconds);
                        break;
                }
            }
            _beatEndsAt = Time.unscaledTime + longest;
        }
    }
}
