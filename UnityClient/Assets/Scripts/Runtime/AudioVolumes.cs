#nullable enable

namespace SSNoir
{
    /// <summary>
    /// 三路音量：背景音乐 / 音效 / 对白。0–1，跟着存档走
    /// （_sceneManager.Settings 里 musicVolume / sfxVolume / dialogueVolume，double 存取，
    /// 读出来不是 double 就当存档坏了直接中断，和 reduceMotion 同一套规矩）。
    /// 面板上是 关/低/中/高 四档分段控件。
    /// 默认：背景音乐低、音效低、对白关。
    /// </summary>
    public static class AudioVolumes
    {
        public static readonly float[] Levels = { 0f, 0.2f, 0.4f, 0.65f };

        public const int MusicDefaultLevel = 1;
        public const int SfxDefaultLevel = 1;
        public const int DialogueDefaultLevel = 0;

        public static float MusicDefault => Levels[MusicDefaultLevel];
        public static float SfxDefault => Levels[SfxDefaultLevel];
        public static float DialogueDefault => Levels[DialogueDefaultLevel];

        public static float Music { get; set; } = Levels[MusicDefaultLevel];
        public static float Sfx { get; set; } = Levels[SfxDefaultLevel];
        public static float Dialogue { get; set; } = Levels[DialogueDefaultLevel];

        public static int MusicLevelIndex => NearestLevelIndex(Music);
        public static int SfxLevelIndex => NearestLevelIndex(Sfx);
        public static int DialogueLevelIndex => NearestLevelIndex(Dialogue);

        public static void SetMusicLevel(int index) => Music = LevelAt(index);
        public static void SetSfxLevel(int index) => Sfx = LevelAt(index);
        public static void SetDialogueLevel(int index) => Dialogue = LevelAt(index);

        public static void ResetToDefaults()
        {
            Music = MusicDefault;
            Sfx = SfxDefault;
            Dialogue = DialogueDefault;
        }

        /// <summary>当前值离哪一档最近——读档回来的任意浮点数都能落回某一档。</summary>
        public static int NearestLevelIndex(float value)
        {
            int best = 0;
            for (int i = 1; i < Levels.Length; i++)
            {
                if (System.Math.Abs(Levels[i] - value) < System.Math.Abs(Levels[best] - value))
                    best = i;
            }
            return best;
        }

        public static float LevelAt(int index) =>
            Levels[System.Math.Clamp(index, 0, Levels.Length - 1)];
    }
}
