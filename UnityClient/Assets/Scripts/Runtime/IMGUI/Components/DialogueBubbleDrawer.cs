#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 非阻塞 banter 的世界气泡：解析说话人锚点 → 在其上方画气泡。
    // 阻塞 play-dialogue! 由 DialogueStageDrawer 使用立绘舞台呈现，不与这里共用外观。
    public static class DialogueBubbleDrawer
    {
        private const float BubbleWidth = 320f;
        private const float BubbleTailHalfWidth = 10f;
        // 等距视角里人形只有几十像素高，气泡尾巴指过去也难看出是谁；有立绘的说话人
        // 在气泡左侧带一张头像。头像是 PhotoBlack 底 + 霓虹头部，和纸层上的照片块同一套。
        private const float AvatarSize = 44f;
        private const float AvatarGap = 10f;

        // 旁白说话人：内容里用「世界」写不属于任何人的叙述句。它没有身体，也就没有锚点，
        // 画成一张不署名的叙述纸条贴在底部，而不是当作一个解析不到的角色报错。
        private const string NarratorSpeaker = "世界";
        private const float NarrationWidth = 680f;
        // 底栏空地窄到这个数以下，就不硬塞了——一行放不下十来个字，折行比抬上去还难读。
        private const float MinGapWidth = 300f;
        // 暗带在字上下各留这么多，斜坡才在字外完成。
        private const float FadePadding = 34f;
        // 压住白描线要的是「暗」，不是「黑块」：中段 62% 已经够，再深就成了黑条。
        private static readonly Color NarrationScrim = new Color(0.031f, 0.039f, 0.059f, 0.62f);

        // 非阻塞:把 BanterPlayer 当前可见的每个气泡画在各自说话人锚点上方。
        public static void DrawBanter(
            BanterPlayer banter,
            DialogueAnchors anchors,
            SSNoirGameManager gameManager,
            System.Action<string> reportRemoteFallback)
        {
            // BanterPlayer 故意让上一句多留 0.6 秒（OverlapSeconds），好让"你一句我一句"
            // 看得到来回——那对人物气泡成立，因为它们各自挂在各自的锚点上。旁白不成立：
            // 它没有锚点，每一句都画在同一个落点，重叠期里两句字幕就会糊在一起。
            // 所以旁白只画最新的那一句，上一句到点就走。
            int newestNarration = -1;
            for (int i = 0; i < banter.Visible.Count; i++)
                if (banter.Visible[i].Line.Speaker == NarratorSpeaker)
                    newestNarration = i;

            for (int i = 0; i < banter.Visible.Count; i++)
            {
                var bubble = banter.Visible[i];
                if (bubble.Line.Speaker == NarratorSpeaker && i != newestNarration)
                    continue;

                bool usedRemoteFallback = DrawBubble(
                    bubble.Line.Speaker,
                    bubble.Line.Text,
                    anchors,
                    gameManager,
                    bubble.AllowsRemoteParticipants,
                    fallsBackToRemoteParticipant: true);
                if (usedRemoteFallback && !bubble.RemoteFallbackWarningIssued)
                {
                    reportRemoteFallback(bubble.Line.Speaker);
                    bubble.RemoteFallbackWarningIssued = true;
                }
            }
        }

        private static bool DrawBubble(
            string speaker,
            string text,
            DialogueAnchors anchors,
            SSNoirGameManager gameManager,
            bool allowsRemoteParticipant = false,
            bool fallsBackToRemoteParticipant = false)
        {
            if (speaker == NarratorSpeaker)
            {
                DrawNarration(text, gameManager);
                return false;
            }

            bool usedRemoteFallback = false;
            if (!anchors.TryResolve(speaker, out var anchor))
            {
                if (!allowsRemoteParticipant && !fallsBackToRemoteParticipant)
                {
                    throw new System.InvalidOperationException(
                        $"对话说话人无法解析到屏幕锚点: '{speaker}'"
                        + "(必须是在场队员、当前可见的场景节点,或你正身处其中的容器。"
                        + $"不属于任何人的叙述句用说话人「{NarratorSpeaker}」;"
                        + "确实不在场的人开口,内容里显式改用对应的 remote 调用!)");
                }

                usedRemoteFallback = !allowsRemoteParticipant;
                anchor = DrawRemoteParticipantCard(speaker);
            }

            // 对话气泡是"递到面前的一张纸"：Paper 底 + PaperInk 字 + 硬投影
            var bodyStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                fontSize = IMGUIStyles.FontSize(16),
                alignment = TextAnchor.UpperLeft,
            };
            bodyStyle.normal.textColor = IMGUIStyles.PaperInk;

            var avatar = NeonPortraitLibrary.Load(speaker);
            float columnX = avatar != null ? 12f + AvatarSize + AvatarGap : 12f;
            float textW = BubbleWidth - columnX - 12f;
            float textH = bodyStyle.CalcHeight(new GUIContent(text), textW);
            float h = Mathf.Max(26f + textH + 12f, avatar != null ? 8f + AvatarSize + 8f : 0f);

            float x = Mathf.Clamp(anchor.center.x - BubbleWidth / 2f, 8f, UIScale.VW - BubbleWidth - 8f);
            float y = anchor.y - h - 10f;
            if (y < 8f)
                y = anchor.yMax + 10f;   // 上方没空间就画到锚点下方
            var rect = new Rect(x, y, BubbleWidth, h);

            // 尾巴不钉在气泡正中，而是指向说话者矩形与气泡之间最近的一对边界点。
            // 人物卡偏到气泡一侧、或气泡因顶边空间不足翻到人物下方时，它仍然准确地"说"自谁。
            GetTail(anchor, rect, out var tailBaseA, out var tailBaseB, out var tailTip);

            var shadowOffset = new Vector2(5f, 6f);
            IMGUIStyles.DrawShadow(rect, shadowOffset, 0.50f);
            DrawTriangle(tailBaseA + shadowOffset, tailBaseB + shadowOffset, tailTip + shadowOffset,
                new Color(0f, 0f, 0f, 0.50f));
            GUI.color = IMGUIStyles.Paper;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawTriangle(tailBaseA, tailBaseB, tailTip, IMGUIStyles.Paper);
            Color outline = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.68f);
            IMGUIStyles.DrawOutline(rect, 1f, outline);
            IMGUIStyles.DrawLine(tailBaseA, tailTip, outline);
            IMGUIStyles.DrawLine(tailTip, tailBaseB, outline);

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(14),
                alignment = TextAnchor.MiddleLeft,
            };
            IMGUIStyles.ApplyStrongFont(nameStyle);
            nameStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            if (avatar != null)
                DrawAvatar(new Rect(rect.x + 12f, rect.y + 8f, AvatarSize, AvatarSize), avatar);
            IMGUIStyles.DrawLabel(new Rect(rect.x + columnX, rect.y + 6f, textW, 20f), speaker, nameStyle);
            IMGUIStyles.DrawLabel(new Rect(rect.x + columnX, rect.y + 26f, textW, textH), text, bodyStyle);
            return usedRemoteFallback;
        }

        // 霓虹图黑等于透明，直接铺在纸上就只剩一团淡蓝；先垫一块照片黑，头才立得起来。
        private static void DrawAvatar(Rect rect, Texture2D neon)
        {
            GUI.color = IMGUIStyles.PhotoBlack;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            var uv = NeonPortraitLibrary.HeadCrop;
            // 叠两遍：Alpha From Grayscale 下蓝管偏透，理由同 HUD 半身像。
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(rect, neon, uv, true);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTextureWithTexCoords(rect, neon, uv, true);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f,
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.68f));
        }

        // 返回气泡边上的尾巴底边与落在说话人边缘的尖端。Rect.ClosestPoint 的语义不适合
        // "点在矩形内"的情形（它会返回点本身），因此这里明确投到四条边中距离最短的一条。
        private static void GetTail(Rect speaker, Rect bubble, out Vector2 baseA, out Vector2 baseB, out Vector2 tip)
        {
            tip = ClosestPointOnEdge(speaker, bubble.center);
            Vector2 baseCenter = ClosestPointOnEdge(bubble, tip);

            if (Mathf.Approximately(baseCenter.x, bubble.x) || Mathf.Approximately(baseCenter.x, bubble.xMax))
            {
                float y = Mathf.Clamp(baseCenter.y, bubble.y + BubbleTailHalfWidth, bubble.yMax - BubbleTailHalfWidth);
                baseA = new Vector2(baseCenter.x, y - BubbleTailHalfWidth);
                baseB = new Vector2(baseCenter.x, y + BubbleTailHalfWidth);
            }
            else
            {
                float x = Mathf.Clamp(baseCenter.x, bubble.x + BubbleTailHalfWidth, bubble.xMax - BubbleTailHalfWidth);
                baseA = new Vector2(x - BubbleTailHalfWidth, baseCenter.y);
                baseB = new Vector2(x + BubbleTailHalfWidth, baseCenter.y);
            }
        }

        private static Vector2 ClosestPointOnEdge(Rect rect, Vector2 point)
        {
            float left = Mathf.Abs(point.x - rect.x);
            float right = Mathf.Abs(point.x - rect.xMax);
            float top = Mathf.Abs(point.y - rect.y);
            float bottom = Mathf.Abs(point.y - rect.yMax);
            float min = Mathf.Min(left, right, top, bottom);

            if (Mathf.Approximately(min, top))
                return new Vector2(Mathf.Clamp(point.x, rect.x, rect.xMax), rect.y);
            if (Mathf.Approximately(min, bottom))
                return new Vector2(Mathf.Clamp(point.x, rect.x, rect.xMax), rect.yMax);
            if (Mathf.Approximately(min, left))
                return new Vector2(rect.x, Mathf.Clamp(point.y, rect.y, rect.yMax));
            return new Vector2(rect.xMax, Mathf.Clamp(point.y, rect.y, rect.yMax));
        }

        // 尾巴以前是 GL 直接画的：绕开 GUI 的矩阵、分组偏移和裁剪栈，全靠这里自己
        // 用 GUIToScreenPoint 补一次换算——气泡哪天被放进分组或滚动区就会画错地方，
        // 而且永远不会被裁掉。现在交给 ShapeDrawer 用贴图画，走的是和其他控件同一条路。
        private static void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
            => ShapeDrawer.DrawTriangle(a, b, c, color);

        // 旁白：不署名、不指向任何人——它是画外音，不是谁递过来的一张纸。
        //
        // 以前它是一张 Paper 底 + PaperInk 字的卡片，还带硬投影。那读起来是「界面里多了
        // 一个东西」：城市是白描线，纸片也是白的，两块白挤在一起，玩家看见的是一张卡，
        // 而不是听见一句旁白。现在换成电影字幕的做法——底部居中的白字，底下垫一条上下
        // 淡出的暗带。暗带没有边，压得住浅色线稿，又不给画面加一个框。
        //
        // 试过的另外两种，别再换回去：纯白字不垫底，压到白色楼体上就糊；实心黑框读成
        // 播放器的字幕框，边界比字还显眼。
        private static void DrawNarration(string text, SSNoirGameManager gameManager)
        {
            var bodyStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                fontSize = IMGUIStyles.FontSize(19),
                alignment = TextAnchor.LowerCenter,
            };
            bodyStyle.normal.textColor = IMGUIStyles.Paper;

            Rect safe = UIScale.SafeArea;

            // 首选落点是底栏两簇之间那块空地——画面最下沿，最像电影字幕，也最不挡东西。
            // 同伴一多，左边的骰池就往右长，那块地会被吃掉；这时字幕整条抬到底栏上方去。
            // 门槛按「一行放不放得下几个字」定，而不是按人数：人数是原因，宽度才是理由。
            Rect gap = HandPanelDrawer.BottomGap(gameManager);
            bool inGap = gap.width >= MinGapWidth;

            float width = inGap
                ? gap.width - 24f
                : Mathf.Min(NarrationWidth, safe.width - 64f);
            float textH = bodyStyle.CalcHeight(new GUIContent(text), width);

            // 两种落点都是「底边钉住，往上长」：字幕跳来跳去比挡住东西更让人分心。
            // 挤在空地里时行宽窄，同一句话会多折一两行——那正是它该做的，不是往外溢。
            float baseline = inGap
                ? gap.yMax
                : safe.yMax - HandPanelDrawer.ReservedHeight - 20f;
            float x = inGap
                ? gap.x + (gap.width - width) / 2f
                : safe.x + (safe.width - width) / 2f;
            var textRect = new Rect(x, baseline - textH, width, textH);

            // 暗带比字宽、比字高：淡出的两端要在字之外完成，否则字的头尾自己在变暗。
            // 挤在空地里时暗带也收进空地，免得从骰子和物品底下透出来一条横杠。
            float fadeX = inGap ? gap.x : safe.x;
            float fadeW = inGap ? gap.width : safe.width;
            var fade = new Rect(fadeX, textRect.y - FadePadding, fadeW, textH + FadePadding * 2f);
            ShapeDrawer.DrawVerticalFade(fade, NarrationScrim);

            // 再垫一层贴着字的投影：暗带只保证「底下是暗的」，笔画边缘要靠它才立起来。
            var shadowStyle = new GUIStyle(bodyStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            IMGUIStyles.DrawLabel(new Rect(textRect.x + 1f, textRect.y + 2f, textRect.width, textRect.height), text, shadowStyle);
            IMGUIStyles.DrawLabel(textRect, text, bodyStyle);
        }

        // 显式场外对话/插话的未在场说话人，以一张临时侧边卡进入画面。
        // 这不是场景节点，不参与交互或导航，只提供清晰的对话锚点。
        private static Rect DrawRemoteParticipantCard(string speaker)
        {
            const float cardW = 176f;
            const float cardH = 148f;
            // 放在左侧中段，避开左下角的主角行动簇；气泡仍从卡片上方冒出。
            var card = new Rect(28f, UIScale.VH * 0.42f, cardW, cardH);

            IMGUIStyles.DrawShadow(card, new Vector2(5f, 6f), 0.50f);
            GUI.color = new Color(0.024f, 0.031f, 0.047f, 0.96f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawDoubleOutline(card, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f));

            // 有霓虹立绘就让人真的露面：卡片本来只写个名字，和场上其他人物的表现不一致。
            var neon = NeonPortraitLibrary.Load(speaker);
            bool hasPortrait = neon != null;
            if (hasPortrait)
            {
                var uv = NeonPortraitLibrary.BustCrop;
                float bustH = card.height - 36f;
                float bustW = bustH * (uv.width / uv.height);
                var bust = new Rect(card.center.x - bustW / 2f, card.y + 6f, bustW, bustH);

                var bleed = new Rect(bust.x - 4f, bust.y - 4f, bust.width + 8f, bust.height + 8f);
                GUI.color = new Color(0.30f, 0.58f, 1f, 0.18f);
                GUI.DrawTextureWithTexCoords(bleed, neon, uv, true);
                // 叠两遍：Alpha From Grayscale 下蓝管偏透，理由同 HUD 半身像。
                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(bust, neon, uv, true);
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTextureWithTexCoords(bust, neon, uv, true);
                // 腰部硬切口抹回暗处，名字压在渐变上。
                var hem = new Rect(bust.x - 8f, bust.yMax - bust.height * 0.30f, bust.width + 16f, bust.height * 0.30f);
                GUI.color = new Color(0.024f, 0.031f, 0.047f, 0.96f);
                GUI.DrawTexture(hem, NeonPortraitLibrary.VerticalFade());
                GUI.color = Color.white;
            }

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(hasPortrait ? 16 : 20),
            };
            IMGUIStyles.ApplyStrongFont(titleStyle);
            float nameY = hasPortrait ? card.yMax - 30f : card.y + 42f;
            IMGUIStyles.DrawLabel(new Rect(card.x + 12f, nameY, card.width - 24f, 42f), speaker, titleStyle);

            return card;
        }
    }
}
