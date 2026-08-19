#nullable enable
using UnityEngine;
using SSNoir.Core;
using SSNoir.IMGUI;

namespace SSNoir
{
    /// <summary>
    /// 标题菜单：进游戏之前停在世界视角上的那一层。
    ///
    /// 它是一个模式，不是一个面板——和影幕同一个道理：菜单在的时候世界照常渲染（那就是背景，
    /// 活的，不是一张图），但一个游戏控件都不出现，镜头也不接受拖拽。所以它排在 OnGUI 最前面
    /// 画完就 return，并且算进 IsInputLocked。
    ///
    /// 「新游戏」之后发生什么，这里一个字都不知道：它只负责把世界重置回第一天，然后交给剧本里
    /// 的 '开场动作 全局。客户端不认识"来访"，也不认识"有人敲门"——换开场、换章节，改的都是
    /// .scm，不是这个文件。
    /// </summary>
    public class TitleScreen
    {
        // 片名占位。定下来之后改这一处。
        private const string Title = "SSNOIR";

        private const float ColumnX = 0.10f;   // 文字栏左边距（屏宽占比）
        private const float TitleY = 0.30f;    // 片名基线（屏高占比）
        private const float ItemHeight = 44f;
        private const float ItemSpacing = 4f;

        private readonly SSNoirGameManager _gameManager;

        private bool _isActive;

        // 最近一次存档：读档走的是它，不是 SaveManager.DefaultSavePath。那条路径只是拿来
        // 推导存档目录的（槽位文件名由它的 dirname 拼出来），本身从来没有人往里写过。
        private string? _latestSlotPath;
        private string _latestSaveTime = string.Empty;

        public TitleScreen(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
        }

        public bool IsActive => _isActive;

        /// <summary>
        /// 升起菜单，并挑出最近的那个存档槽。
        ///
        /// 这里读一次文件，不每帧读——OnGUI 一秒要跑几十遍，而 GetSaveTime 是要把整个存档
        /// 解析成 JSON 的。存档时间是 "2026-08-06 10:39:15" 这种写法，直接按字典序比就是
        /// 按时间比。
        /// </summary>
        public void Open()
        {
            _isActive = true;
            _latestSlotPath = null;
            _latestSaveTime = string.Empty;

            for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
            {
                string path = SaveManager.GetSlotFilePath(slot);
                string time = SaveManager.GetSaveTime(path);   // 没有存档时返回空串
                if (string.IsNullOrEmpty(time)
                    || string.CompareOrdinal(time, _latestSaveTime) <= 0)
                    continue;

                _latestSaveTime = time;
                _latestSlotPath = path;
            }
        }

        public void Close()
        {
            _isActive = false;
        }

        // ── 三个入口 ──────────────────────────────────────────────────────

        /// <summary>
        /// 新游戏：重置世界，然后把开场交给剧本。
        ///
        /// 菜单先退场再重置，顺序不能反——开场动作可能当场就拉起过场，影幕要占满屏幕，
        /// 菜单不能还压在上面。
        /// </summary>
        private void NewGame()
        {
            Close();
            _gameManager.RestartGame();
            _gameManager.RunOpeningAction();
        }

        /// <summary>读档：读进来就是玩到一半的世界，没有开场。</summary>
        private void Continue()
        {
            Close();
            _gameManager.LoadGame(_latestSlotPath);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── 绘制 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 画菜单。由 IMGUIWorldRenderer 在所有游戏控件之前调用，调完那边就直接 return。
        /// </summary>
        public void Draw(Vector2 mouse)
        {
            var ui = new IMGUIInteractionContext(mouse, isLocked: false);

            float vw = UIScale.VW;
            float vh = UIScale.VH;

            float x = vw * ColumnX;
            float titleY = vh * TitleY;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = IMGUIStyles.FontSize(58),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.Paper },
            };
            IMGUIStyles.ApplyStrongFont(titleStyle);
            IMGUIStyles.DrawLabel(new Rect(x, titleY, vw * 0.6f, 76f), Title, titleStyle);

            // 片名下面一条金发丝，和游戏内各处分隔线同一个手势。
            IMGUIStyles.DrawLine(
                new Vector2(x, titleY + 88f),
                new Vector2(x + 220f, titleY + 88f),
                new Color(IMGUIStyles.Gold.r, IMGUIStyles.Gold.g, IMGUIStyles.Gold.b, 0.75f),
                1f);

            float itemY = titleY + 128f;

            if (DrawItem(new Rect(x, itemY, 300f, ItemHeight), "新游戏", ui, enabled: true))
            {
                NewGame();
                return;
            }
            itemY += ItemHeight + ItemSpacing;

            // 没有存档时留着但灰掉：位置固定，玩家才知道自己缺的是什么，而不是以为没这功能。
            // 存档时间跟在后面——读档读的是最近那一个，得让人知道自己接的是哪一天。
            if (DrawItem(new Rect(x, itemY, 300f, ItemHeight), "从存档加载", ui,
                    enabled: _latestSlotPath != null, hint: _latestSaveTime))
            {
                Continue();
                return;
            }
            itemY += ItemHeight + ItemSpacing;

            // 浏览器里没有"退出游戏"这回事，Application.Quit() 是个空操作，摆上去只会骗人。
#if !UNITY_WEBGL || UNITY_EDITOR
            if (DrawItem(new Rect(x, itemY, 300f, ItemHeight), "退出游戏", ui, enabled: true))
            {
                Quit();
                return;
            }
#endif
        }

        /// <summary>
        /// 一条菜单文字。不画框——标题界面上的按钮框会把画面变成一个设置面板。
        /// 悬停时左边推出一根金竖条，文字转金。
        /// </summary>
        private static bool DrawItem(
            Rect rect, string label, IMGUIInteractionContext ui, bool enabled, string hint = "")
        {
            bool hovered = enabled && ui.CanHover(rect);

            if (hovered)
            {
                GUI.color = IMGUIStyles.Gold;
                GUI.DrawTexture(new Rect(rect.x - 14f, rect.y + 10f, 3f, rect.height - 20f),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = IMGUIStyles.FontSize(22),
                alignment = TextAnchor.MiddleLeft,
                normal =
                {
                    textColor = !enabled
                        ? IMGUIStyles.TextDisabled
                        : hovered ? IMGUIStyles.Gold : IMGUIStyles.TextPrimary,
                },
            };
            IMGUIStyles.ApplyStrongFont(style);
            IMGUIStyles.DrawLabel(rect, label, style);

            if (!string.IsNullOrEmpty(hint))
            {
                var hintStyle = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = IMGUIStyles.FontSize(13),
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = IMGUIStyles.TextDisabled },
                };
                IMGUIStyles.DrawLabel(new Rect(rect.x + 140f, rect.y, 220f, rect.height), hint, hintStyle);
            }

            if (!enabled || !ui.WasTapped(rect))
                return false;

            // 吃掉这次点击：菜单下面就是世界，漏过去会当成一次世界点击。
            Event.current.Use();
            return true;
        }
    }
}
