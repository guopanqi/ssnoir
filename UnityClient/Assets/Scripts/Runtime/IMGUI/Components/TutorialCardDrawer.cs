#nullable enable
using UnityEngine;
using UnityEngine.Video;

namespace SSNoir.IMGUI
{
    /// <summary>自动教程与帮助页共用的卡片正文和静音播放器。</summary>
    public static class TutorialCardDrawer
    {
        private const float Gap = 12f;
        private const float ReplayHeight = 34f;
        private static VideoPlayer? _player;
        private static RenderTexture? _texture;
        private static string? _entryId;
        private static string? _error;
        private static bool _finished;

        private static GUIStyle BodyStyle() => new GUIStyle(IMGUIStyles.ModalBody)
        {
            fontSize = IMGUIStyles.FontSize(15),
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
        };

        private static float TextHeight(TutorialEntry entry, float width)
        {
            var style = BodyStyle();
            float height = 0f;
            foreach (var paragraph in entry.Paragraphs)
                height += style.CalcHeight(new GUIContent(paragraph), width) + Gap;
            return height;
        }

        public static float MeasureHeight(TutorialEntry entry, float width) =>
            TextHeight(entry, width) + (entry.VideoFileName.Length > 0 ? width * 9f / 16f + ReplayHeight + Gap * 2f : 0f);

        public static void Draw(TutorialEntry entry, Rect content, IMGUIInteractionContext ui)
        {
            float y = content.y;
            if (entry.VideoFileName.Length > 0)
            {
                EnsureVideo(entry);
                // 保留文字和控件的空间，视频按剩余高度缩小；所有画布使用同一组约束。
                float height = Mathf.Max(0f, Mathf.Min(content.width * 9f / 16f,
                    content.height - TextHeight(entry, content.width) - ReplayHeight - Gap * 2f));
                var videoRect = new Rect(content.x, y, content.width, height);
                GUI.DrawTexture(videoRect, Texture2D.blackTexture);
                if (_player != null && _player.isPrepared && _texture != null)
                    GUI.DrawTexture(videoRect, _texture!, ScaleMode.ScaleToFit, false);
                else
                    IMGUIStyles.DrawLabel(videoRect,
                        _error ?? (BuildRuntimeConfig.CutsceneVideosDisabled ? "此构建未包含教程视频" : "正在载入演示…"),
                        new GUIStyle(BodyStyle()) { alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = IMGUIStyles.Paper } });
                y += height + Gap;
                if (TutorialPaper.DrawPrimaryButton(new Rect(content.x, y, 96f, ReplayHeight),
                    "重播", ui, _player != null && _player.isPrepared && _error == null))
                {
                    _finished = false;
                    _player!.time = 0;
                    _player.Play();
                }
                if (_finished)
                    IMGUIStyles.DrawLabel(new Rect(content.x + 108f, y, content.width - 108f, ReplayHeight),
                        "演示已结束，可点击重播", BodyStyle());
                y += ReplayHeight + Gap;
            }
            else if (_entryId != entry.Id)
                StopVideo();

            var body = BodyStyle();
            foreach (var paragraph in entry.Paragraphs)
            {
                float height = body.CalcHeight(new GUIContent(paragraph), content.width);
                IMGUIStyles.DrawLabel(new Rect(content.x, y, content.width, height), paragraph, body);
                y += height + Gap;
            }
        }

        private static void EnsureVideo(TutorialEntry entry)
        {
            if (_entryId == entry.Id) return;
            StopVideo();
            _entryId = entry.Id;
            if (BuildRuntimeConfig.CutsceneVideosDisabled) return;
            var host = new GameObject("教程视频") { hideFlags = HideFlags.HideAndDontSave };
            _player = host.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.source = VideoSource.Url;
            _player.url = BuildRuntimeConfig.ResolveCutsceneUrl(entry.VideoFileName);
            _player.prepareCompleted += OnPrepared;
            _player.loopPointReached += OnFinished;
            _player.errorReceived += OnError;
            _player.Prepare();
        }

        private static void OnPrepared(VideoPlayer player)
        {
            // 8 位线性贴图会把暗部压成极少的色阶，回到屏幕时形成蓝绿色块。
            // 视频用 sRGB 存储以保留暗部精度，采样时由 Unity 转换。
            _texture = new RenderTexture((int)player.width, (int)player.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "SSNoir.Tutorial" };
            player.targetTexture = _texture;
            player.Play();
        }
        private static void OnFinished(VideoPlayer player) => _finished = true;
        private static void OnError(VideoPlayer player, string message)
        {
            _error = "演示载入失败，请先阅读下方说明";
            Debug.LogError($"[SSNoir] 教程视频无法播放：{player.url}\n{message}");
        }

        public static void StopVideo()
        {
            if (_player != null)
            {
                _player.prepareCompleted -= OnPrepared;
                _player.loopPointReached -= OnFinished;
                _player.errorReceived -= OnError;
                _player.Stop();
                Object.Destroy(_player.gameObject);
                _player = null;
            }
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
                _texture = null;
            }
            _entryId = null;
            _error = null;
            _finished = false;
        }
    }
}
