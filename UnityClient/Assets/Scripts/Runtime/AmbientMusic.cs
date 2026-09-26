#nullable enable
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 城市配乐：认全局键 <c>音乐</c>。每次快照落地时由 <see cref="SSNoirGameManager"/> 喂一次当前值，
    /// 同一个值重复喂不会重头播，排期也不会被打断。
    ///
    /// 三种行为，命名即归属（Resources/Music/ 下按前缀组池，加歌自动收录）：
    /// - #f：城市默认声（城市-*）。随机一张播一阵（PlayMin/MaxSeconds），淡出静一阵
    ///   （GapMin/MaxSeconds），再换一张，不连播同一张。偶尔才响；以后要跟场景/情绪走，
    ///   从 PlayNextTrack 的选曲处接。
    /// - 唱片 id（唱片-*）：点哪张就一直循环哪张，盖过默认声。抬起唱针（#f）回到默认声。
    /// - "随机播放"：在唱片-* 里一首接一首随机（不连播同一张），播完一首的长度就切，
    ///   不留静场——这是唱片机的点唱机，和默认声的"偶尔"不是一回事。
    ///
    /// 两个 AudioSource 轮换：旧的淡出、新的淡入，切歌与进出静场都走同一条路，
    /// 同一时间最多两条在响。没有第二种状态来源。
    /// </summary>
    public class AmbientMusic : MonoBehaviour
    {
        private const string ResourcesFolder = "Music/";
        private const string RecordPrefix = "唱片-";
        private const string CityPrefix = "城市-";
        private const string ShuffleId = "随机播放";
        private const float FadeSeconds = 1.6f;
        // 音量不归这里管：读 MusicVolume.Value（设置面板四档，跟着存档）。

        // 默认声的时间表：每首播 90–150 秒，静 25–55 秒。播的是整首循环，
        // 不是单遍——单曲 20–40 秒，单遍一切城市就像在切台。
        private const float PlayMinSeconds = 90f;
        private const float PlayMaxSeconds = 150f;
        private const float GapMinSeconds = 25f;
        private const float GapMaxSeconds = 55f;

        private AudioSource _a = null!;
        private AudioSource _b = null!;
        private AudioSource _current = null!;
        private string? _currentId;
        private float _fade = 1f;

        // 排期：默认声与唱片随机各走各的计时，定点播放时两个都关。
        private bool _playlistOn;
        private bool _shuffleOn;
        private string[] _pool = System.Array.Empty<string>();
        private string? _track;
        private float _schedTimer;
        private bool _inGap;

        private void Awake()
        {
            _a = gameObject.AddComponent<AudioSource>();
            _b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _a, _b })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
            }
            _current = _a;
        }

        /// <summary>把当前应当响着的曲子告诉它；同一个值重复喂什么都不发生。</summary>
        public void Apply(string? clipId)
        {
            if (string.IsNullOrEmpty(clipId))
                clipId = null;
            if (clipId == null)
            {
                // 初始 _currentId 也是 null，不能拿它当"已经在播"的证据，得看排期开没开。
                if (_playlistOn)
                    return;
                EnterPlaylist();
                return;
            }
            if (string.Equals(clipId, _currentId, System.StringComparison.Ordinal))
                return;

            _playlistOn = false;
            _shuffleOn = false;
            if (string.Equals(clipId, ShuffleId, System.StringComparison.Ordinal))
            {
                EnterShuffle();
            }
            else
            {
                _currentId = clipId;
                SwapTo(LoadClip(clipId));
            }
        }

        /// <summary>起城市默认声：池子是城市-*，播一阵、静一阵。</summary>
        private void EnterPlaylist()
        {
            _pool = LoadPool(CityPrefix, "城市默认声");
            _playlistOn = true;
            _currentId = null;
            _track = null;
            PlayNextTrack();
            _inGap = false;
            _schedTimer = Random.Range(PlayMinSeconds, PlayMaxSeconds);
        }

        /// <summary>起唱片随机：池子是唱片-*，播完一首的长度就切，不留静场。</summary>
        private void EnterShuffle()
        {
            _pool = LoadPool(RecordPrefix, "唱片随机");
            _shuffleOn = true;
            _currentId = ShuffleId;
            _track = null;
            PlayNextTrack();
            _schedTimer = CurrentLength();
        }

        /// <summary>换池子里下一张，不连播同一张（只有一张时没得选）。</summary>
        private void PlayNextTrack()
        {
            string next = _pool[Random.Range(0, _pool.Length)];
            if (_pool.Length > 1)
            {
                while (string.Equals(next, _track, System.StringComparison.Ordinal))
                    next = _pool[Random.Range(0, _pool.Length)];
            }
            _track = next;
            SwapTo(LoadClip(next));
        }

        private float CurrentLength()
        {
            return _current.clip != null ? _current.clip.length : 60f;
        }

        private static string[] LoadPool(string prefix, string purpose)
        {
            var clips = Resources.LoadAll<AudioClip>(ResourcesFolder);
            var names = new System.Collections.Generic.List<string>(clips.Length);
            foreach (var c in clips)
            {
                if (c != null && !string.IsNullOrEmpty(c.name)
                    && c.name.StartsWith(prefix, System.StringComparison.Ordinal))
                    names.Add(c.name);
            }
            if (names.Count == 0)
            {
                string message = $"[SSNoir] {purpose}要起播，但 Resources/{ResourcesFolder} 里没有'{prefix}*'的唱片。";
                Debug.LogError(message);
                throw new System.InvalidOperationException(message);
            }
            return names.ToArray();
        }

        private static AudioClip LoadClip(string clipId)
        {
            var clip = Resources.Load<AudioClip>(ResourcesFolder + clipId);
            if (clip == null)
            {
                string message = $"[SSNoir] 全局键 音乐 = '{clipId}'，但 Resources/{ResourcesFolder}{clipId} 不存在。";
                Debug.LogError(message);
                throw new System.InvalidOperationException(message);
            }
            return clip;
        }

        /// <summary>
        /// 切源：旧的淡出、新的淡入。clip 为 null 就是进静场——排期的静一阵
        /// 和点唱片、抬唱针走的都是这一条，没有第二种淡出。
        /// </summary>
        private void SwapTo(AudioClip? clip)
        {
            var next = _current == _a ? _b : _a;
            next.Stop();
            next.clip = clip;
            if (clip != null)
            {
                next.volume = 0f;
                next.Play();
            }
            _current = next;
            _fade = 0f;
        }

        private void Update()
        {
            // 音量每帧都写：淡入淡出只管 _fade，面板改 MusicVolume 要当场生效，
            // 不能只在淡入那 1.6 秒里写一次。
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / FadeSeconds);
            var previous = _current == _a ? _b : _a;
            _current.volume = _current.clip != null ? MusicVolume.Value * _fade : 0f;
            previous.volume = MusicVolume.Value * (1f - _fade);
            if (_fade >= 1f && previous.isPlaying)
            {
                previous.Stop();
                previous.clip = null;
            }

            // 定点播放时排期全关；点唱片/抬唱针/切随机都会重进对应状态。
            if (!_playlistOn && !_shuffleOn)
                return;
            _schedTimer -= Time.unscaledDeltaTime;
            if (_schedTimer > 0f)
                return;
            if (_shuffleOn)
            {
                PlayNextTrack();
                _schedTimer = CurrentLength();
                return;
            }
            if (!_inGap)
            {
                SwapTo(null);
                _inGap = true;
                _schedTimer = Random.Range(GapMinSeconds, GapMaxSeconds);
            }
            else
            {
                PlayNextTrack();
                _inGap = false;
                _schedTimer = Random.Range(PlayMinSeconds, PlayMaxSeconds);
            }
        }
    }
}
