#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 非阻塞 banter 的世界气泡：解析说话人锚点 → 在其上方画气泡。
    // 阻塞 play-dialogue! 由 StoryStageDrawer 使用立绘舞台呈现，不与这里共用外观。
    public static class DialogueBubbleDrawer
    {
        private const float BubbleWidth = 320f;
        private const float BubbleTailHalfWidth = 10f;
        // 等距视角里人形只有几十像素高，气泡尾巴指过去也难看出是谁；有立绘的说话人
        // 在气泡左侧带一张头像。头像是 PhotoBlack 底 + 霓虹头部，和纸层上的照片块同一套。
        private const float AvatarSize = 44f;
        private const float AvatarGap = 10f;

        // 旁白说话人：内容里用「世界」写不属于任何人的叙述句。它没有身体，也就没有锚点，
        // 画成不署名的统一字幕，贴底并避让两侧 UI，而不是当作一个解析不到的角色报错。
        public const string NarratorSpeaker = "世界";
        // 非阻塞:把 BanterPlayer 当前可见的每个气泡画在各自说话人锚点上方。
        public static void DrawBanter(
            BanterPlayer banter,
            DialogueAnchors anchors,
            SSNoirGameManager gameManager,
            System.Action<string> reportRemoteFallback,
            WorldLabelLayout.Space? space,
            WorldLabelLayout.PlacementState placements, bool suppressNarration)
        {
            // 不同人物保留短暂重叠；同一人物连续发言把上一句作为上文向上移。
            // BanterPlayer 故意让上一句多留一段时间，好让"你一句我一句"
            // 看得到来回——那对人物气泡成立，因为它们各自挂在各自的锚点上。旁白不成立：
            // 它没有锚点，每一句都画在同一个落点，重叠期里两句字幕就会糊在一起。
            // 所以旁白只画最新的那一句，上一句到点就走。
            var living = new System.Collections.Generic.List<string>();
            foreach (var active in banter.Visible)
                if (active.Line.Speaker != NarratorSpeaker)
                    living.Add("banter:" + active.StackId);
            placements.KeepOnly(living);

            int newestNarration = -1;
            for (int i = 0; i < banter.Visible.Count; i++)
                if (banter.Visible[i].Line.Speaker == NarratorSpeaker)
                    newestNarration = i;

            for (int i = 0; i < banter.Visible.Count; i++)
            {
                var bubble = banter.Visible[i];
                NeonPortraitLibrary.DeclareAppearance(bubble.Line.Speaker, bubble.Line.Appearance);
                if (bubble.Line.Speaker == NarratorSpeaker && (suppressNarration || i != newestNarration))
                    continue;

                // 每组只由最新一句负责绘制；旧句作为无尾巴的上文一起画。
                BanterPlayer.Bubble? previous = null;
                bool hasNewer = false;
                for (int j = 0; j < banter.Visible.Count; j++)
                {
                    var other = banter.Visible[j];
                    if (other == bubble || other.StackId != bubble.StackId) continue;
                    if (other.LayoutId > bubble.LayoutId) hasNewer = true;
                    else previous = other;
                }
                if (hasNewer) continue;
                bool usedRemoteFallback;
                // 行级锚点（line 的 :at）：身份和位置分开，气泡落在锚点上、署说话人的名。
                // 锚点写错必须当场暴露：若再按说话人解析，气泡会悄悄落到错的地方。
                if (!string.IsNullOrEmpty(bubble.Line.Anchor))
                {
                    // 显式 :at 指向场景锚点，不依赖本帧有哪些卡片进入可视区域。
                    var worldAnchor = gameManager.ResolveAnchor(bubble.Line.Anchor);
                    if (worldAnchor == null)
                    {
                        throw new System.InvalidOperationException(
                            $"banter 行级锚点无法解析: anchor='{bubble.Line.Anchor}' speaker='{bubble.Line.Speaker}'"
                            + "（锚点名须与场景中的 NodeAnchor 一致）");
                    }
                    var camera = Camera.main;
                    if (camera == null)
                        throw new System.InvalidOperationException("banter 世界锚点投影需要主相机");
                    var headScreen = camera.WorldToScreenPoint(
                        worldAnchor.transform.position + Vector3.up * DialogueAnchors.BanterHeadHeight);
                    var headPoint = UIScale.WorldPointToVirtual(headScreen);
                    var anchorRect = new Rect(headPoint.x - 12f, headPoint.y - 12f, 24f, 24f);
                    usedRemoteFallback = false;  // 行级锚点已明确落点，不存在降级
                    DrawBubbleAt(
                        bubble.Line.Speaker,
                        bubble.Line.DisplaySpeaker,
                        bubble.Line.Text,
                        anchorRect, space, placements, "banter:" + bubble.StackId,
                        previous: previous, age: bubble.Age);
                    bubble.RemoteFallbackWarningIssued = true;  // 已有明确落点，不再报降级警告
                }
                else
                {
                    usedRemoteFallback = DrawBubble(
                        bubble.Line.Speaker,
                        bubble.Line.DisplaySpeaker,
                        bubble.Line.Text,
                        anchors,
                        gameManager,
                        bubble.AllowsRemoteParticipants,
                        fallsBackToRemoteParticipant: true,
                        preferCardAnchor: bubble.PreferCardAnchor,
                        space: space,
                        placements: placements,
                        bubbleKey: "banter:" + bubble.StackId, previous: previous, age: bubble.Age);
                }
                if (usedRemoteFallback && !bubble.RemoteFallbackWarningIssued)
                {
                    reportRemoteFallback(bubble.Line.Speaker);
                    bubble.RemoteFallbackWarningIssued = true;
                }
            }
        }

        private static bool DrawBubble(
            string speaker,
            string displaySpeaker,
            string text,
            DialogueAnchors anchors,
            SSNoirGameManager gameManager,
            bool allowsRemoteParticipant = false,
            bool fallsBackToRemoteParticipant = false,
            bool preferCardAnchor = false,
            WorldLabelLayout.Space? space = null,
            WorldLabelLayout.PlacementState? placements = null,
            string bubbleKey = "", BanterPlayer.Bubble? previous = null, float age = 0f)
        {
            if (speaker == NarratorSpeaker)
            {
                DrawNarration(text, gameManager);
                return false;
            }

            bool usedRemoteFallback = false;
            if (!anchors.TryResolve(speaker, preferCardAnchor, out var anchor))
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
                anchor = DrawRemoteParticipantCard(speaker, displaySpeaker);
                space?.Reserve(anchor, WorldLabelLayout.ZoneKind.Soft);
            }

            DrawBubbleAt(speaker, displaySpeaker, text, anchor, space, placements, bubbleKey,
                showAvatar: !usedRemoteFallback && anchors.IsCardAnchor(speaker, preferCardAnchor) == false
                    && !allowsRemoteParticipant, previous: previous, age: age);
            return usedRemoteFallback;
        }

        // 气泡绘制本体：落点已定，只管画。行级锚点（line 的 :at）直接走这里，
        // 不经过说话人解析——身份和位置在调用方已经分开。
        private static void DrawBubbleAt(
            string speaker,
            string displaySpeaker,
            string text,
            Rect anchor,
            WorldLabelLayout.Space? space,
            WorldLabelLayout.PlacementState? placements,
            string bubbleKey,
            bool showAvatar = true, BanterPlayer.Bubble? previous = null, float age = 0f)
        {
            // 对话气泡是"递到面前的一张纸"：Paper 底 + PaperInk 字 + 硬投影
            var bodyStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                fontSize = IMGUIStyles.FontSize(18),
                alignment = TextAnchor.UpperLeft,
            };
            bodyStyle.normal.textColor = IMGUIStyles.PaperInk;

            var avatar = showAvatar ? NeonPortraitLibrary.Load(speaker) : null;
            float columnX = avatar != null ? 12f + AvatarSize + AvatarGap : 12f;
            float textW = BubbleWidth - columnX - 12f;
            float textH = bodyStyle.CalcHeight(new GUIContent(text), textW);
            float h = Mathf.Max(26f + textH + 12f, avatar != null ? 8f + AvatarSize + 8f : 0f);

            float x = Mathf.Clamp(anchor.center.x - BubbleWidth / 2f, 8f, UIScale.VW - BubbleWidth - 8f);
            float y = anchor.y - h - 10f;
            if (y < 8f)
                y = anchor.yMax + 10f;   // 上方没空间就画到锚点下方
            var rect = new Rect(x, y, BubbleWidth, h);
            if (space != null)
            {
                if (placements == null)
                    throw new System.InvalidOperationException("Banter layout state is required");
                rect = placements.PlaceBanter(space, new WorldLabelLayout.Request(
                    bubbleKey, WorldLabelLayout.Kind.Bubble, anchor.center, rect.center,
                    new Vector2(BubbleWidth, h)));
            }

            float progress = previous == null ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .32f));
            if (previous != null)
            {
                float oldTextH = bodyStyle.CalcHeight(new GUIContent(previous.Line.Text), textW);
                float oldH = Mathf.Max(26f + oldTextH + 12f, avatar != null ? 8f + AvatarSize + 8f : 0f);
                var oldRect = new Rect(rect.x, rect.yMax - oldH - (h + 8f) * progress, BubbleWidth, oldH);
                // 顶部放不下上文就略去它，不挪动当前句和指向人物的尾巴。
                if (oldRect.y >= 8f)
                {
                    space?.Reserve(oldRect, WorldLabelLayout.ZoneKind.Hard);
                    DrawBubblePaper(oldRect, anchor, false, .85f);
                    DrawBubbleText(oldRect, previous.Line.DisplaySpeaker, previous.Line.Text,
                        bodyStyle, avatar, columnX, textW, oldTextH, .85f);
                }
            }
            // 尾巴从当前框的固定底边指向人物；只有新句文字渐显，上文向上让位。
            DrawBubblePaper(rect, anchor, true, previous == null ? 1f : progress);
            DrawBubbleText(rect, displaySpeaker, text, bodyStyle, avatar, columnX, textW, textH,
                previous == null ? 1f : progress);
        }

        private static void DrawBubblePaper(Rect rect, Rect anchor, bool tail, float alpha)
        {
            var savedColor = GUI.color;
            GetTail(anchor, rect, out var tailBaseA, out var tailBaseB, out var tailTip);
            var shadowOffset = new Vector2(5f, 6f);
            IMGUIStyles.DrawShadow(rect, shadowOffset, .5f * alpha);
            if (tail) DrawTriangle(tailBaseA + shadowOffset, tailBaseB + shadowOffset,
                tailTip + shadowOffset, new Color(0f, 0f, 0f, .5f));
            GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (tail) DrawTriangle(tailBaseA, tailBaseB, tailTip,
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f));
            Color outline = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, .68f * alpha);
            IMGUIStyles.DrawOutline(rect, 1f, outline);
            if (tail)
            {
                var tailOutline = new Color(outline.r, outline.g, outline.b, .68f);
                IMGUIStyles.DrawLine(tailBaseA, tailTip, tailOutline);
                IMGUIStyles.DrawLine(tailTip, tailBaseB, tailOutline);
            }
            GUI.color = savedColor;
        }

        private static void DrawBubbleText(Rect rect, string displaySpeaker, string text,
            GUIStyle bodyStyle, Texture2D? avatar, float columnX, float textW, float textH, float alpha)
        {
            var savedColor = GUI.color;
            GUI.color = new Color(savedColor.r, savedColor.g, savedColor.b, savedColor.a * alpha);
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.MiddleLeft,
            };
            IMGUIStyles.ApplyStrongFont(nameStyle);
            nameStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            if (avatar != null)
            {
                DrawAvatar(new Rect(rect.x + 12f, rect.y + 8f, AvatarSize, AvatarSize), avatar);
                GUI.color = new Color(savedColor.r, savedColor.g, savedColor.b, savedColor.a * alpha);
            }
            IMGUIStyles.DrawLabel(new Rect(rect.x + columnX, rect.y + 6f, textW, 20f),
                displaySpeaker, nameStyle);
            IMGUIStyles.DrawLabel(new Rect(rect.x + columnX, rect.y + 26f, textW, textH), text, bodyStyle);
            GUI.color = savedColor;
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
            // 世界气泡通常在说话人头顶。碰撞避让会把卡片横向推开；这时若只按两矩形的
            // 最近距离选边，人物与卡片纵向重叠时会错误地从左右侧伸尾巴。上下关系优先，
            // 只有说话人与卡片几乎齐平时才退回左右侧判定。
            float verticalOffset = speaker.center.y - bubble.center.y;
            if (Mathf.Abs(verticalOffset) > BubbleTailHalfWidth)
            {
                bool speakerBelow = verticalOffset > 0f;
                float baseY = speakerBelow ? bubble.yMax : bubble.y;
                float x = Mathf.Clamp(speaker.center.x,
                    bubble.x + BubbleTailHalfWidth, bubble.xMax - BubbleTailHalfWidth);
                baseA = new Vector2(x - BubbleTailHalfWidth, baseY);
                baseB = new Vector2(x + BubbleTailHalfWidth, baseY);

                float tipY;
                if (speakerBelow)
                    tipY = speaker.y < bubble.yMax
                        ? Mathf.Max(speaker.yMax, bubble.yMax + BubbleTailHalfWidth)
                        : speaker.y;
                else
                    tipY = speaker.yMax > bubble.y
                        ? Mathf.Min(speaker.y, bubble.y - BubbleTailHalfWidth)
                        : speaker.yMax;
                tip = new Vector2(Mathf.Clamp(x, speaker.x, speaker.xMax), tipY);
                return;
            }

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
            // 点在矩形外时，先将它夹到矩形范围：得到的才是最近边界点。
            // 仅比较到四条无限直线的距离，会把左侧人物误判成上边，尾角反向。
            if (point.x < rect.x || point.x > rect.xMax || point.y < rect.y || point.y > rect.yMax)
                return new Vector2(Mathf.Clamp(point.x, rect.x, rect.xMax),
                    Mathf.Clamp(point.y, rect.y, rect.yMax));

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

        // 「世界」气泡与 play-narration! 使用同一字幕组件与贴底避让规则。
        private static void DrawNarration(string text, SSNoirGameManager gameManager)
            => StoryCaptionDrawer.DrawWorldNarration(text, HandPanelDrawer.BottomGap(gameManager));

        // 显式场外对话/插话的未在场说话人，以一张临时侧边卡进入画面。
        // 这不是场景节点，不参与交互或导航，只提供清晰的对话锚点。
        private static Rect DrawRemoteParticipantCard(string speaker, string displaySpeaker)
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
            IMGUIStyles.DrawLabel(new Rect(card.x + 12f, nameY, card.width - 24f, 42f),
                displaySpeaker, titleStyle);

            return card;
        }
    }
}
