#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 手牌 HUD。新组织（2026-07 迭代）：不再是底部一整条 HUD 栏，而是拆成两个
    // 独立、无外框的角落簇——左下「人物 / 行动池」，右下「物品 / 功能」。
    //
    // 统一语言：悬浮靠阴影，操作靠描边。要点/要拖的器物（骰子、物品、按钮）才描一圈
    // 安静的边并加硬投影托起；名字、压力、生命体征、标题等直接落在场景上（Paper 亮字，
    // 对深蓝图纸对比足够）。没有底栏、没有把两簇统一在一起的外框。
    //
    // 每个行动者是一个「靠间距聚拢的簇」，自底向上：行动骰 → 名字 → 压力。
    // 所有行动者共享同一条压力/名字/骰池基线；只有主角（最左第一个）从压力线往上
    // 多长出队伍伤势（为快照级属性）。
    public static class HandPanelDrawer
    {
        // 手牌方块：骰子与物品共用同一族方块（同尺寸、同底纹、同交互状态），只是内容不同。
        // 卡里的骰位（ActionNodeDrawer.DieSlot）跟着同一个数走——
        // 拖过去的东西和接它的坑必须一样大。
        public const float TokenSize = 50f;
        private const float TokenSpacing = TokenSize + 8f;

        private const float BottomMargin = 22f;   // 底边到安全区底的留白
        private const float ClusterGap   = 28f;    // 两个行动者簇之间的间距

        // 左右两簇贴的是安全区，不是画布——刘海屏横屏时左右各会被挖掉一块。
        // 留白随设备走：桌面 40px 的边距搬到手机那块小得多的虚拟画布上就是一大条空地。
        private const float SideMargin = 16f;

        // 立起来的半身像高度（主角；同伴略矮）。
        // 它是装饰，不能按固定虚拟像素走：手机的虚拟画布只有 480 上下，190 的半身像会占掉
        // 四成屏高，把城市整个挡住。按屏高收敛，桌面上仍然是原来的 190。
        private static float BustHeight => Mathf.Min(190f, UIScale.VH * 0.22f);
        private const float BustOverlap = 26f;    // 半身像下缘压进数值行的深度

        /// <summary>
        /// 底部两簇占掉的高度。世界内容（卡片网格等）不该压到这条线以下——
        /// 以前渲染器那边写死一个 175，屏幕一换就对不上了。半身像不算在内：它本来就是
        /// 故意浮在世界上方的。
        /// </summary>
        public static float ReservedHeight =>
            BottomMargin + TokenSize + 4f + VitalRowH * 2f + 4f + 18f + 8f;

        /// <summary>
        /// 底部两簇里**真的要用手指点或拖**的那几块地方：左下的行动骰、右下的物品与功能键。
        /// 世界投射卡按这几个矩形避让。
        ///
        /// 不是一条横贯全屏的下边界——那正是卡片叠成一摞的原因之一。底栏中段本来就是空的，
        /// 冷静条、伤势条、名字、半身像也都是读的不是点的，卡片飘到那上面一点不影响。
        /// 真正不能盖的只有这几个方块。
        ///
        /// 量和画共用同一套宽度（<see cref="ClusterWidth"/> / <see cref="FunctionWidth"/>），
        /// 免得「让开的地方」和「实际画的地方」各算一遍、改一处忘了另一处。
        /// </summary>
        public static void CollectTouchBlockers(SSNoirGameManager gameManager, List<Rect> into)
        {
            Rect safe = UIScale.SafeArea;
            float baseline = safe.yMax - BottomMargin;
            // 选中的方块会上浮 4px，避让范围跟着抬这一点。
            float blockTop = baseline - TokenSize - 4f;
            float blockH = TokenSize + 4f;
            var snapshot = gameManager.DisplayedSnapshot;

            // 左下：在场行动者的骰池，自左向右一簇挨一簇。
            float x = safe.x + SideMargin;
            float clustersLeft = x;
            bool leadDrawn = false;
            foreach (var actor in snapshot.Actors)
            {
                if (!actor.OnStage)
                    continue;
                x += ClusterWidth(actor, isLead: !leadDrawn) + ClusterGap;
                leadDrawn = true;
            }
            if (leadDrawn)
                into.Add(new Rect(clustersLeft, blockTop, x - ClusterGap - clustersLeft, blockH));

            // 右下：功能键贴着右缘，物品排在它左侧。
            float functionW = FunctionWidth(gameManager);
            float right = safe.xMax - SideMargin;
            float left = right - functionW;
            int itemCount = 0;
            foreach (var kvp in snapshot.Inventory)
                if (kvp.Value > 0) itemCount++;
            if (itemCount > 0)
                left -= 28f + (itemCount - 1) * TokenSpacing + TokenSize;
            into.Add(new Rect(left, blockTop, right - left, blockH));
        }

        // 一个行动者簇占多宽。这里必须按固定骰位数而不是当前剩余骰数：骰子投入行动后会
        // 从 ActionDice 列表移除，但 SlotId 仍是身体状态与空间身份的归属；若簇随剩余数收缩，
        // 后面的同伴会横跳，剩下的骰也会看起来换了位置。
        private static float ClusterWidth(ActorSnapshot actor, bool isLead)
        {
            int slotCount = actor.ActionSlotCount;
            float diceW = slotCount > 0 ? (slotCount - 1) * TokenSpacing + TokenSize : 0f;
            return Mathf.Max(Mathf.Max(isLead ? 176f : 150f, diceW), 172f);
        }

        // 遭遇里功能键是三块（抽烟 / 喝酒 / 休息），世界里只有一块（回家）。
        private static float FunctionWidth(SSNoirGameManager gameManager)
            => gameManager.SceneManager.CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase)
                ? 92f
                : 180f;

        private const float NameRowH   = 20f;   // 只在没有半身像时才占位
        private const float VitalRowH  = 22f;   // 冷静 / 伤势条那一行；字比以前大一号
        private const float VitalLabelW = 40f;  // 两条共用同一个标签宽度，左端才对得齐

        // 手牌黑方块：不透明纯黑一档 + 描边 + 硬投影，让它从深蓝图纸上浮起来。
        private static readonly Color CardBlockBg = new Color(0.024f, 0.031f, 0.047f, 1f);
        private static readonly Color DisabledResourceBg = new Color(0.024f, 0.031f, 0.047f, 0.55f);
        private static readonly Color DisabledResourceText = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
        private static readonly Color Paper70 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);
        private static readonly Color Paper35 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
        private static readonly Color Paper25 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
        private static readonly Color Paper14 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.14f);

        // ── 入口 ───────────────────────────────────────────────────────

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors = null)
        {
            float baseline = UIScale.SafeArea.yMax - BottomMargin;   // 行动骰底边
            DrawCharacters(baseline, gameManager, ui, anchors, drawPortraits: false, drawForeground: true);
            DrawItemsAndFunctions(baseline, gameManager, ui);
        }

        /// <summary>
        /// 人物半身像是环境层，不承载任何可操作或需优先阅读的信息。
        /// 在世界卡与它们的 attachment 之前先画，保证判定条、结果条和骰池不会被肖像遮住。
        /// </summary>
        public static void DrawPortraits(SSNoirGameManager gameManager)
        {
            float baseline = UIScale.SafeArea.yMax - BottomMargin;
            DrawCharacters(baseline, gameManager, default, anchors: null, drawPortraits: true, drawForeground: false);
        }

        // ── 左下：人物簇 ───────────────────────────────────────────────

        private static void DrawCharacters(
            float baseline,
            SSNoirGameManager gameManager,
            IMGUIInteractionContext ui,
            DialogueAnchors? anchors,
            bool drawPortraits,
            bool drawForeground)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            float x = UIScale.SafeArea.x + SideMargin;
            int flatDieOffset = 0;
            bool leadDrawn = false;

            for (int i = 0; i < snapshot.Actors.Count; i++)
            {
                var actor = snapshot.Actors[i];
                // 只画本场登场的人；未登场者没有骰子也不能被指挥，整簇不画。
                if (!actor.OnStage)
                {
                    flatDieOffset += actor.ActionDice.Count;
                    continue;
                }

                bool isLead = !leadDrawn;   // 最左第一个在场角色 = 主角，头顶挂队伍生命体征
                leadDrawn = true;

                // 主角与同伴之间一条淡分隔线：两簇本来只靠间距分开，人多了容易读成一片。
                if (drawForeground && !isLead)
                {
                    float sepX = x - ClusterGap * 0.5f;
                    DrawClusterSeparator(sepX, baseline);
                }

                float clusterW = DrawCluster(
                    x, baseline, actor, flatDieOffset, isLead, snapshot, gameManager, ui, anchors,
                    drawPortraits, drawForeground);
                x += clusterW + ClusterGap;
                flatDieOffset += actor.ActionDice.Count;
            }
        }

        // 一个行动者簇：自底向上 行动骰 →（名字）→ 冷静 →（带伤时的伤势）→ 状态说明。返回簇宽度。
        private static float DrawCluster(
            float x, float baseline, ActorSnapshot actor, int flatDieOffset, bool isLead,
            PresentationSnapshot snapshot, SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors,
            bool drawPortraits, bool drawForeground)
        {
            int diceCount = actor.ActionDice.Count;
            float clusterW = ClusterWidth(actor, isLead);

            // 半身像已经说明这是谁，就不再写名字——省下的一行让条子整体往下坐，上方更宽裕。
            var neon = NeonPortraitLibrary.Load(actor.Name);
            bool drawName = neon == null;
            // 完好的人身上什么都没有：伤势条只在真的带着伤时才占位置。
            bool showInjury = isLead && snapshot.InjurySeverity > 0;

            float diceY = baseline - TokenSize;
            float nameY = diceY - NameRowH - 4f;
            float composureY = (drawName ? nameY : diceY - 4f) - VitalRowH - 2f;
            float injuryY = showInjury ? composureY - VitalRowH - 2f : composureY;
            string statusText = DescribeSlotStatuses(actor);
            // 伤势是队伍级的，只挂在主角这一簇上；它和骰池状态共用同一行说明文字。
            if (showInjury)
            {
                string injuryText = $"{snapshot.InjuryPart}伤 · {snapshot.InjurySkillName}{snapshot.InjurySkillPenalty}"
                                  + (snapshot.InjuryCostsActionDie ? " · −1颗骰" : "");
                statusText = statusText.Length > 0 ? injuryText + " ｜ " + statusText : injuryText;
            }
            // 旧伤没有条子可画——它不会涨也不会退，只是一行永远在那儿的字。
            if (isLead && snapshot.ScarSummary.Length > 0)
                statusText = statusText.Length > 0
                    ? statusText + " ｜ " + snapshot.ScarSummary
                    : snapshot.ScarSummary;
            bool hasStatus = statusText.Length > 0;
            float statusY = hasStatus ? injuryY - 18f : injuryY;
            float topY = statusY;

            // ── 半身像先落笔：人物从 HUD 里立起来，条子和骰子随后画上去压住它下缘，
            // 像栏杆后面站着的人。不给它画框——一有边框就变回图标了。
            if (neon != null)
            {
                float bustHeight = isLead ? BustHeight : BustHeight * 0.86f;
                var uv = NeonPortraitLibrary.BustCrop;
                float bustWidth = bustHeight * (uv.width / uv.height);
                // 下缘故意压到冷静条那一行，让数值叠在人身上，而不是各占一块地。
                //
                // 站位一律以**冷静条**为准，不以 statusY 为准：冷静是每个人都有的一行，
                // 而伤势条和状态文字只有主角带着伤时才存在——跟着 statusY 走，主角就会因为
                // 自己身上多两行字而整个人比同伴高出一截，看起来像站在台阶上。
                // 人像大小仍有主次之分（同伴 0.86），但脚下是同一条线。
                float bustBottom = composureY + BustOverlap;
                var bust = new Rect(x - 6f, bustBottom - bustHeight, bustWidth, bustHeight);
                if (drawPortraits)
                    DrawNeonBust(bust, neon, uv);
                topY = bust.y;
            }

            if (!drawForeground)
                return clusterW;

            // ── 名字：只有没有半身像的人才需要写出来，否则是重复信息。
            if (drawName)
            {
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = IMGUIStyles.FontSize(18),
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = IMGUIStyles.TextPrimary },
                };
                IMGUIStyles.ApplyStrongFont(nameStyle);
                IMGUIStyles.DrawLabel(new Rect(x, nameY, clusterW, NameRowH), actor.Name, nameStyle);
            }

            DrawComposureBar(x, composureY, 172f, actor.Composure);
            if (showInjury)
            {
                // 伤势是队伍级的，只挂在主角这一簇上。
                DrawInjuryBar(x, injuryY, 172f, snapshot);
            }
            if (hasStatus)
            {
                // 这行字压在霓虹半身像和城市描线上——两者都是高频细线，12px 赭黄落上去就没了。
                // 先铺一条贴着字宽的暗底把背后的线切断，再用纸白写字：HUD 上唯一要读的信息
                // 不能靠运气跟背景错开。
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = IMGUIStyles.FontSize(13),
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = IMGUIStyles.TextPrimary },
                };
                var content = new GUIContent(statusText);
                float textW = statusStyle.CalcSize(content).x;
                var textRect = new Rect(x, statusY, textW + 4f, 18f);
                var oldColor = GUI.color;
                GUI.color = new Color(0.004f, 0.007f, 0.016f, 0.86f);
                GUI.DrawTexture(new Rect(textRect.x - 5f, textRect.y, textRect.width + 10f, textRect.height),
                                Texture2D.whiteTexture);
                GUI.color = oldColor;
                IMGUIStyles.DrawLabel(textRect, statusText, statusStyle);
            }

            // ── 行动骰（手牌方块；选中金描边+上浮+金字；已放入卡槽降为禁用亮度）
            for (int d = 0; d < diceCount; d++)
            {
                float dieX = x + actor.ActionDiceSlotIds[d] * TokenSpacing;
                DrawDie(new Rect(dieX, diceY, TokenSize, TokenSize), actor.ActionDice[d], flatDieOffset + d, gameManager, ui);
            }

            anchors?.RegisterActor(actor.Id, actor.Name, new Rect(x, topY, clusterW, baseline - topY));
            return clusterW;
        }

        // 立在 HUD 里的霓虹半身像。没有边框、没有底板——衬托靠人物背后那团椭圆暗晕，
        // 它没有边界，所以人像是「从暗处走出来」而不是「贴在一块牌子上」。
        // 刻意不做呼吸和闪烁：常驻 HUD 上的动画会一直勾眼睛，那是对白舞台该干的事。
        private static void DrawNeonBust(Rect rect, Texture2D neon, Rect uv)
        {
            // 背后的暗晕：霓虹靠亮度对比活着，底必须压黑，否则贴在深蓝图纸上会糊。
            var halo = NeonPortraitLibrary.RadialFalloff();
            var glowArea = new Rect(
                rect.center.x - rect.width * 1.05f,
                rect.center.y - rect.height * 0.72f,
                rect.width * 2.1f,
                rect.height * 1.44f);
            GUI.color = new Color(0.004f, 0.007f, 0.016f, 0.86f);
            GUI.DrawTexture(glowArea, halo);
            var core = new Rect(
                rect.center.x - rect.width * 0.62f,
                rect.center.y - rect.height * 0.52f,
                rect.width * 1.24f,
                rect.height * 1.04f);
            GUI.color = new Color(0.004f, 0.007f, 0.016f, 0.80f);
            GUI.DrawTexture(core, halo);

            // 溢光一层：贴图本身带辉光，稍微放大压淡地垫一遍就够，不必像舞台那样叠三层。
            var bleed = new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f);
            GUI.color = new Color(0.30f, 0.58f, 1f, 0.18f);
            GUI.DrawTextureWithTexCoords(bleed, neon, uv, true);
            // 灯管本体叠两遍，理由同对白舞台：Alpha From Grayscale 下蓝管偏透。
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(rect, neon, uv, true);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTextureWithTexCoords(rect, neon, uv, true);

            // 腰部是硬切口，用一段竖直渐变把它抹回暗处，不能让灯管断得那么直。
            var hem = new Rect(rect.x - 8f, rect.yMax - rect.height * 0.30f, rect.width + 16f, rect.height * 0.30f);
            GUI.color = new Color(0.004f, 0.007f, 0.016f, 0.96f);
            GUI.DrawTexture(hem, NeonPortraitLibrary.VerticalFade());
            GUI.color = Color.white;
        }

        private static string DescribeSlotStatuses(ActorSnapshot actor)
        {
            var labels = new List<string>();
            foreach (var status in actor.ActiveActionSlotStatuses)
                labels.Add(DescribeSlotStatus(status));
            foreach (var status in actor.PendingActionSlotStatuses)
                labels.Add(DescribeSlotStatus(status));
            // 同一枚状态铺在多个骰位上时（恒定点数）只说一次，不逐位重复。
            var unique = new List<string>();
            foreach (var label in labels)
                if (!unique.Contains(label)) unique.Add(label);
            return string.Join("  ", unique);
        }

        private static string DescribeSlotStatus(ActionSlotStatus status)
        {
            return status.FixedDieValue != null
                ? $"{status.Label}·恒{status.FixedDieValue.Value}"
                : $"{status.Label}{status.DiePenalty:+#;-#}";
        }

        // 冷静条（主角专用）：冷静是纯缓冲，中间没有档位，所以不画阈值刻度线。
        // 玩家要读的只有"还剩几格"和"见底"——见底之后每一点消耗都变成伤势。
        // 两簇之间的竖直分隔线。只覆盖所有行动者共享的那几行（冷静 → 名字 → 骰池），
        // 不往上蹭主角独有的伤势行，否则线会长得没有道理。按「有半身像、不画名字」的
        // 常规布局取顶端；没有立绘的人多出一行名字，线短一点点，不值得为此把布局传进来。
        private static void DrawClusterSeparator(float x, float baseline)
        {
            float topY = baseline - TokenSize - 4f - VitalRowH - 2f;
            GUI.color = Paper14;
            GUI.DrawTexture(new Rect(x, topY, 1f, baseline - topY), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawComposureBar(float x, float y, float w, int composure)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.78f) },
            };
            IMGUIStyles.DrawLabel(new Rect(x, y, VitalLabelW, VitalRowH), "冷静", labelStyle);

            int max = TeamState.MaxComposure;
            Color fill = composure <= 0 ? IMGUIStyles.SealRed
                       : composure <= 1 ? IMGUIStyles.OddsNeutral
                       : IMGUIStyles.TextPrimary;

            float barX = x + VitalLabelW + 8f;
            float barW = w - (VitalLabelW + 8f) - 40f;
            float barH = 9f;
            float barY = y + (VitalRowH - barH) / 2f;
            const float cellGap = 2f;
            float cellW = (barW - cellGap * (max - 1)) / max;

            for (int i = 0; i < max; i++)
            {
                var cell = new Rect(barX + i * (cellW + cellGap), barY, cellW, barH);
                GUI.color = i < composure ? fill : Paper14;
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            var valStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = fill },
            };
            // 见底比 "0/2" 说得清楚：再扛一次就进身体。
            IMGUIStyles.DrawLabel(new Rect(barX + barW + 4f, y, 38f, VitalRowH),
                composure > 0 ? $"{composure}/{max}" : "见底", valStyle);
        }

        private static void DrawDie(Rect dieRect, int val, int globalIdx, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            bool isSlotted  = gameManager.IsDieSlotted(globalIdx);
            bool isSelected = gameManager.SelectedResource != null
                           && gameManager.SelectedResource.Type == "die"
                           && gameManager.SelectedResource.SourceIndex == globalIdx;
            bool hover = !isSlotted && ui.CanHover(dieRect);

            if (isSlotted)
            {
                GUI.color = DisabledResourceBg;
                GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(dieRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.20f));
                var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = IMGUIStyles.FontSize(22) };
                dimStyle.normal.textColor = Paper25;
                IMGUIStyles.DrawLabel(dieRect, val.ToString(), dimStyle);
                return;
            }

            bool disabled = ui.IsLocked;
            // 骰子与物品同一族方块：骰值居中（无下方标签）。
            DrawHandBlock(dieRect, val.ToString(), null, isSelected, hover, disabled);

            if (!disabled && ui.WasClicked(dieRect))
            {
                gameManager.BeginDieDrag(globalIdx, val, ui.Mouse);
                Event.current.Use();
            }
        }

        // 手牌方块（骰子 / 物品共用）：黑方块 + 描边 + 硬投影；大字（骰值或物品符号）在上/中，
        // 下方可选小标签（物品的数量/金额）。选中：金描边 + 上浮 + 金字；禁用：整体降到 35%。
        private static void DrawHandBlock(Rect rect, string big, string? small, bool selected, bool hover, bool disabled)
        {
            var drawRect = selected && !disabled
                ? new Rect(rect.x, rect.y - 4f, rect.width, rect.height)
                : rect;
            Color border = disabled
                ? Paper35
                : selected
                    ? IMGUIStyles.Gold
                    : hover
                        ? IMGUIStyles.Paper
                        : Paper70;

            if (!disabled)
                IMGUIStyles.DrawShadow(drawRect, new Vector2(2f, 2f), 0.45f);
            GUI.color = disabled ? DisabledResourceBg : CardBlockBg;
            GUI.DrawTexture(drawRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(drawRect, !disabled && (selected || hover) ? 2f : 1f, border);

            Color content = disabled ? DisabledResourceText : selected ? IMGUIStyles.Gold : IMGUIStyles.Paper;
            bool hasSmall = !string.IsNullOrEmpty(small);

            var bigStyle = new GUIStyle(IMGUIStyles.SlotLabel)
            {
                fontSize = IMGUIStyles.FontSize(24),
                alignment = hasSmall ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter,
                normal = { textColor = content }
            };
            IMGUIStyles.ApplyStrongFont(bigStyle);
            var bigRect = hasSmall ? new Rect(drawRect.x, drawRect.y + 3f, drawRect.width, 28f) : drawRect;
            IMGUIStyles.DrawLabel(bigRect, big, bigStyle);

            if (hasSmall)
            {
                var smallStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = IMGUIStyles.FontSize(13),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = disabled ? DisabledResourceText : (selected ? IMGUIStyles.Gold : IMGUIStyles.Paper) }
                };
                IMGUIStyles.ApplyStrongFont(smallStyle);
                // 方块高 50；旧的 y+34 / h18 实际画到方块外 2px，数量会压住底栏标题。
                IMGUIStyles.DrawLabel(new Rect(drawRect.x, drawRect.y + 31f, drawRect.width, 16f), small!, smallStyle);
            }
        }

        // 伤势细条：格数即刻度，越满越糟——和冷静条方向相反。重伤线用一道留白分开，
        // 让"再挨几下就跨过去"直接看得见。完好时整条不画，由调用方决定。
        private static void DrawInjuryBar(float x, float y, float w, PresentationSnapshot snapshot)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.78f) },
            };
            IMGUIStyles.DrawLabel(new Rect(x, y, VitalLabelW, VitalRowH), "伤势", labelStyle);

            int max = snapshot.InjuryMaxSeverity;
            int severity = Mathf.Clamp(snapshot.InjurySeverity, 0, max);
            Color fill = snapshot.InjuryCostsActionDie ? IMGUIStyles.SealRed
                       : severity > 0 ? IMGUIStyles.OddsNeutral
                       : IMGUIStyles.TextPrimary;

            float barX = x + VitalLabelW + 8f;
            float barW = w - (VitalLabelW + 8f) - 40f;
            float barH = 9f;
            float barY = y + (VitalRowH - barH) / 2f;
            const float cellGap = 2f;
            const float boundaryGap = 6f;   // 重伤线：以留白替代刻度线
            float cellW = (barW - cellGap * (max - 1) - (boundaryGap - cellGap)) / max;
            for (int i = 0; i < max; i++)
            {
                float offset = i * (cellW + cellGap)
                             + (i >= Injury.SevereThreshold ? boundaryGap - cellGap : 0f);
                GUI.color = i < severity ? fill : Paper14;
                GUI.DrawTexture(new Rect(barX + offset, barY, cellW, barH), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            var valStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = fill },
            };
            IMGUIStyles.DrawLabel(new Rect(barX + barW + 4f, y, 38f, VitalRowH), $"{severity}/{max}", valStyle);
        }

        // ── 右下：物品 + 功能 ──────────────────────────────────────────

        private static void DrawItemsAndFunctions(float baseline, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            bool isInEncounter = !gameManager.SceneManager.CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
            float functionW = FunctionWidth(gameManager);
            // 功能键按 64 画在手机上是一块很大的砖；跟手牌方块取齐，一排读起来才是一排。
            float functionH = TokenSize;
            float functionX = UIScale.SafeArea.xMax - SideMargin - functionW;
            float functionY = baseline - functionH;
            var sectionStyle = new GUIStyle(IMGUIStyles.SectionLabel)
            {
                fontSize = IMGUIStyles.FontSize(17),
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.82f) }
            };
            IMGUIStyles.ApplyStrongFont(sectionStyle);
            IMGUIStyles.DrawLabel(new Rect(functionX, functionY - 20f, functionW, 18f), "功能", sectionStyle);

            if (isInEncounter)
            {
                var encounterSnapshot = gameManager.DisplayedSnapshot;
                bool hasSmoke = encounterSnapshot.Inventory.TryGetValue("香烟", out int smoke) && smoke > 0;
                bool hasDrink = encounterSnapshot.Inventory.TryGetValue("酒", out int drink) && drink > 0;
                var smokeRect = new Rect(functionX, functionY, 52f, functionH);
                var drinkRect = new Rect(functionX + 60f, functionY, 52f, functionH);
                var restRect = new Rect(functionX + 120f, functionY, 60f, functionH);
                if (DrawFunctionBlock(smokeRect, "抽 烟", ui, !hasSmoke) && hasSmoke)
                    gameManager.OnUseEncounterConsumable("香烟");
                else if (DrawFunctionBlock(drinkRect, "喝 酒", ui, !hasDrink) && hasDrink)
                    gameManager.OnUseEncounterConsumable("酒");
                else if (DrawFunctionBlock(restRect, "休 息", ui, false))
                    gameManager.OnEndTurnClicked();
            }
            else
            {
                var homeRect = new Rect(functionX, functionY, functionW, functionH);
                if (DrawFunctionBlock(homeRect, "回 家", ui, false))
                    gameManager.NavigateToHome();
            }

            // 物品：黑方块 + 白色符号大字 + 下方小字标签，排在功能键左侧、从右往左贴住。
            var snapshot = gameManager.DisplayedSnapshot;
            var items = new List<(string Name, int Qty)>();
            foreach (var kvp in snapshot.Inventory)
                if (kvp.Value > 0) items.Add((kvp.Key, kvp.Value));
            if (items.Count == 0) return;

            float itemsW = (items.Count - 1) * TokenSpacing + TokenSize;
            float itemsRightEdge = functionX - 28f;
            float startX = itemsRightEdge - itemsW;
            float itemY = baseline - TokenSize;
            IMGUIStyles.DrawLabel(new Rect(startX, itemY - 20f, 80f, 18f), "物品", sectionStyle);

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemRect = new Rect(startX + i * TokenSpacing, itemY, TokenSize, TokenSize);
                int remaining = gameManager.GetRemainingItemQty(item.Name);
                bool isSelected = gameManager.SelectedResource != null
                               && gameManager.SelectedResource.Type == "item"
                               && gameManager.SelectedResource.ItemName == item.Name;
                bool hover = ui.CanHover(itemRect);

                if (remaining <= 0)
                {
                    DrawItemBlock(itemRect, item.Name, isSelected, hover, remaining, disabled: true);
                }
                else
                {
                    bool disabled = ui.IsLocked;
                    DrawItemBlock(itemRect, item.Name, isSelected, hover, remaining, disabled);
                    if (!disabled && ui.WasClicked(itemRect))
                    {
                        gameManager.BeginItemDrag(item.Name, item.Qty, ui.Mouse);
                        Event.current.Use();
                    }
                }
            }
        }

        private static bool DrawFunctionBlock(Rect rect, string label, IMGUIInteractionContext ui, bool unavailable)
        {
            bool disabled = ui.IsLocked || unavailable;
            bool hover = !disabled && ui.CanHover(rect);
            if (!disabled)
                IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), 0.45f);
            GUI.color = disabled ? DisabledResourceBg : CardBlockBg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            Color border = disabled ? Paper35 : (hover ? IMGUIStyles.Paper : Paper70);
            IMGUIStyles.DrawOutline(rect, hover ? 2f : 1f, border);
            var style = new GUIStyle(IMGUIStyles.ExecuteLabel)
            {
                fontSize = IMGUIStyles.FontSize(16),
                normal = { textColor = disabled ? DisabledResourceText : IMGUIStyles.Paper }
            };
            IMGUIStyles.DrawLabel(rect, label, style);
            if (!disabled && ui.WasTapped(rect))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        // 物品：与骰子共用手牌方块，大字=类别符号，小标签=数量/金额（金钱用 $，其它取首字）。
        private static void DrawItemBlock(Rect itemRect, string name, bool isSelected, bool hover, int remaining, bool disabled)
        {
            string smallLabel = name == "金钱" ? $"${remaining}" : $"x{remaining}";
            DrawHandBlock(itemRect, ItemSymbol(name), smallLabel, isSelected, hover, disabled);
        }

        private static string ItemSymbol(string name)
        {
            return name switch
            {
                "金钱" => "$",
                "酒" => "酒",
                "香烟" => "烟",
                "药品" => "药",
                _ => name.Length > 0 ? name.Substring(0, 1) : "?"
            };
        }
    }
}
