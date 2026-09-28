#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SSNoir.Core;
using SSNoir.IMGUI.Stage;

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
        private enum PortraitCondition { Stable, LowComposure, LightInjury, SevereInjury }

        // 手牌方块：骰子与物品共用同一族方块（同尺寸、同底纹、同交互状态），只是内容不同。
        // 卡里的骰位（ActionNodeDrawer.DieSlot）跟着同一个数走——
        // 拖过去的东西和接它的坑必须一样大。
        public const float TokenSize = 50f;
        private const float TokenSpacing = TokenSize + 8f;

        // 底边到安全区底的留白。曾经是 22——比左右两侧的 16、顶栏上方的 10 都大，
        // 底下那条空地是三条边里最宽的一条，白占了视野。安全区本身已经躲开了手势条和
        // 圆角，这里不需要再替它留一份。收到 12：仍比骰子方块的落点余量宽，
        // 但整块底栏往下压了 10 个虚拟像素，全部还给城市。
        private const float BottomMargin = 12f;
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

            // 休息键上方的随身消费卡也会接收拖放，世界卡必须让开完整卡面。
            if (gameManager.DisplayedSnapshot.IsInEncounter)
            {
                var carry = ActiveEncounterActionNodes(gameManager);
                for (int i = 0; i < carry.Count; i++)
                    into.Add(EncounterActionCardRect(carry, i, right, blockTop + 4f, gameManager));
            }
        }

        /// <summary>
        /// 人物半身像不承载交互，只是布局上的软占位：空间充足时世界卡让开，空间不足时
        /// 可以覆盖。这里与 DrawCluster 共用同一组几何常量，避免布局器猜头像画在哪里。
        /// </summary>
        public static void CollectPortraitBounds(SSNoirGameManager gameManager, List<Rect> into)
        {
            float baseline = UIScale.SafeArea.yMax - BottomMargin;
            float x = UIScale.SafeArea.x + SideMargin;
            bool leadDrawn = false;
            foreach (var actor in gameManager.DisplayedSnapshot.Actors)
            {
                if (!actor.OnStage) continue;
                bool isLead = !leadDrawn;
                var neon = NeonPortraitLibrary.Load(actor.Name);
                if (neon != null)
                {
                    float diceY = baseline - TokenSize;
                    float composureY = diceY - 4f - VitalRowH - 2f;
                    float bustHeight = isLead ? BustHeight : BustHeight * 0.86f;
                    var uv = NeonPortraitLibrary.BustCrop;
                    float bustWidth = bustHeight * (uv.width / uv.height);
                    float bustBottom = composureY + BustOverlap;
                    into.Add(new Rect(x - 6f, bustBottom - bustHeight, bustWidth, bustHeight));
                }
                x += ClusterWidth(actor, isLead) + ClusterGap;
                leadDrawn = true;
            }
        }

        /// <summary>
        /// 底栏两簇之间那块空地：左下骰池的右缘到右下物品簇的左缘，高度是骰子那一行。
        ///
        /// 旁白字幕坐在这里。同伴一多，左簇往右长，这块地会被吃掉——所以它可能返回一个
        /// 很窄甚至反向的矩形，调用方按自己的最小宽度决定是用它还是躲到底栏上方去。
        /// 几何只算在这一个文件里：字幕那边再照着 TokenSize / SideMargin 算一遍，
        /// 迟早会和实际画的位置对不上。
        /// </summary>
        public static Rect BottomGap(SSNoirGameManager gameManager)
        {
            Rect safe = UIScale.SafeArea;
            float baseline = safe.yMax - BottomMargin;
            float top = baseline - TokenSize - 4f;
            var snapshot = gameManager.DisplayedSnapshot;

            float left = safe.x + SideMargin;
            bool leadDrawn = false;
            foreach (var actor in snapshot.Actors)
            {
                if (!actor.OnStage)
                    continue;
                left += ClusterWidth(actor, isLead: !leadDrawn) + ClusterGap;
                leadDrawn = true;
            }
            if (leadDrawn)
                left -= ClusterGap;

            float right = safe.xMax - SideMargin - FunctionWidth(gameManager);
            int itemCount = 0;
            foreach (var kvp in snapshot.Inventory)
                if (kvp.Value > 0) itemCount++;
            if (itemCount > 0)
                right -= 28f + (itemCount - 1) * TokenSpacing + TokenSize;

            return new Rect(left, top, right - left, baseline - top);
        }

        // ── 教程要指的那几块地方 ─────────────────────────────────────
        //
        // 高亮框由这里给，不由教程那边照着常量再算一遍：底栏的几何只有这个文件知道。
        // 它们是「圈出来给人看」的框，不是命中区，所以宁可稍微宽一点。

        /// <summary>左下角所有在场行动者的行动骰那一排。</summary>
        public static Rect ActionDiceRect(SSNoirGameManager gameManager)
        {
            Rect safe = UIScale.SafeArea;
            float baseline = safe.yMax - BottomMargin;
            float x = safe.x + SideMargin;
            float w = 0f;
            bool leadDrawn = false;
            foreach (var actor in gameManager.DisplayedSnapshot.Actors)
            {
                if (!actor.OnStage) continue;
                w += ClusterWidth(actor, isLead: !leadDrawn) + ClusterGap;
                leadDrawn = true;
            }
            if (!leadDrawn) return Rect.zero;
            return new Rect(x, baseline - TokenSize - 4f, w - ClusterGap, TokenSize + 4f);
        }

        public static bool TryGetActionDieRect(
            SSNoirGameManager gameManager, string actorId, int slotId, out Rect rect)
        {
            Rect safe = UIScale.SafeArea;
            float baseline = safe.yMax - BottomMargin;
            float x = safe.x + SideMargin;
            bool leadDrawn = false;
            foreach (var actor in gameManager.DisplayedSnapshot.Actors)
            {
                if (!actor.OnStage) continue;
                if (string.Equals(actor.Id, actorId, StringComparison.OrdinalIgnoreCase))
                {
                    rect = new Rect(x + slotId * TokenSpacing, baseline - TokenSize, TokenSize, TokenSize);
                    return true;
                }
                x += ClusterWidth(actor, isLead: !leadDrawn) + ClusterGap;
                leadDrawn = true;
            }
            rect = Rect.zero;
            return false;
        }

        public static void DrawMovingDie(Rect rect, int value)
        {
            DrawHandBlock(rect, value.ToString(), null, selected: false, hover: false, disabled: false);
        }

        /// <summary>主角骰子上方那两条读数（冷静，带伤时还有伤势）。</summary>
        public static Rect LeadVitalsRect(SSNoirGameManager gameManager)
        {
            Rect safe = UIScale.SafeArea;
            float baseline = safe.yMax - BottomMargin;
            float diceY = baseline - TokenSize;
            var snapshot = gameManager.DisplayedSnapshot;
            ActorSnapshot? lead = null;
            foreach (var actor in snapshot.Actors)
                if (actor.OnStage) { lead = actor; break; }
            if (lead == null) return Rect.zero;

            // 两条读数一起圈：有伤时是「冷静 + 伤势」，没伤时上面那半是名字，圈进去也无妨。
            float h = VitalRowH * 2f + 6f;
            return new Rect(safe.x + SideMargin, diceY - 6f - h,
                ClusterWidth(lead, isLead: true), h);
        }

        /// <summary>右下角那个功能键（城里是「回家」，交锋里是「休息」）。</summary>
        public static Rect FunctionKeyRect(SSNoirGameManager gameManager)
        {
            Rect safe = UIScale.SafeArea;
            float baseline = safe.yMax - BottomMargin;
            float functionW = FunctionWidth(gameManager);
            return new Rect(safe.xMax - SideMargin - functionW, baseline - TokenSize - 4f,
                functionW, TokenSize + 4f);
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

        // 功能键两边都只有一块：交锋里是「休息」，世界里是「回家」。
        // 功能键保持紧凑；随身消费动作已经改成上方的标准卡，不再决定这一列的宽度。
        private const float FunctionBlockW = 108f;
        private static float FunctionWidth(SSNoirGameManager gameManager) => FunctionBlockW;

        private const float NameRowH   = 20f;   // 只在没有半身像时才占位
        private const float VitalRowH  = 22f;   // 冷静 / 伤势条那一行；字比以前大一号
        private const float VitalLabelW = 40f;  // 两条共用同一个标签宽度，左端才对得齐

        // 手牌黑方块：不透明纯黑一档 + 描边 + 硬投影，让它从深蓝图纸上浮起来。
        private static readonly Color CardBlockBg = new Color(0.024f, 0.031f, 0.047f, 1f);
        private static readonly Color DisabledResourceBg = new Color(0.024f, 0.031f, 0.047f, 0.55f);
        private static readonly Color DisabledResourceText = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
        private static readonly Color Paper55 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f);
        private static readonly Color Paper35 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
        private static readonly Color Paper25 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
        private static readonly Color Paper14 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.14f);

        // ── 入口 ───────────────────────────────────────────────────────

        // 本帧画在手牌区里的动作宿主（休息键、随身卡）的矩形，按动作名记。它们不在卡片层，
        // 判定条和结果条要挂上去只能从这里找。
        public static readonly Dictionary<string, Rect> DrawnActionRects = new(StringComparer.OrdinalIgnoreCase);

        public static void Draw(
            SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors = null,
            IReadOnlyList<SlottedResource>? pendingAutoActionDice = null)
        {
            DrawnActionRects.Clear();
            float baseline = UIScale.SafeArea.yMax - BottomMargin;   // 行动骰底边
            DrawCharacters(baseline, gameManager, ui, anchors, drawPortraits: false, drawForeground: true,
                pendingAutoActionDice: pendingAutoActionDice);
            DrawItemsAndFunctions(baseline, gameManager, ui);
            DrawOpponentActingVeil(gameManager);
        }

        // 对方回合：骰子那一排蒙一层暗——它们还在手上，但这一刻不是你的。
        // 冷静条、名字、伤势不蒙：那正是这段时间里要看的东西（谁打了你、掉了多少）。
        // 和顶上的阶段横条是同一件事的两面：横条说谁在动，这层暗说你动不了。
        private static void DrawOpponentActingVeil(SSNoirGameManager gameManager)
        {
            if (!gameManager.IsOpponentActing)
                return;
            var dice = ActionDiceRect(gameManager);
            if (dice.width <= 0f)
                return;
            var veil = new Rect(dice.x - 6f, dice.y - 6f, dice.width + 12f, dice.height + 10f);
            var oldColor = GUI.color;
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.62f);
            GUI.DrawTexture(veil, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        /// <summary>
        /// 人物半身像是环境层，不承载任何可操作或需优先阅读的信息。
        /// 在世界卡与它们的 attachment 之前先画，保证判定条、结果条和骰池不会被肖像遮住。
        /// </summary>
        public static void DrawPortraits(SSNoirGameManager gameManager)
        {
            float baseline = UIScale.SafeArea.yMax - BottomMargin;
            DrawCharacters(baseline, gameManager, default, anchors: null, drawPortraits: true, drawForeground: false,
                pendingAutoActionDice: null);
        }

        // ── 左下：人物簇 ───────────────────────────────────────────────

        private static void DrawCharacters(
            float baseline,
            SSNoirGameManager gameManager,
            IMGUIInteractionContext ui,
            DialogueAnchors? anchors,
            bool drawPortraits,
            bool drawForeground,
            IReadOnlyList<SlottedResource>? pendingAutoActionDice)
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
                    drawPortraits, drawForeground, pendingAutoActionDice);
                x += clusterW + ClusterGap;
                flatDieOffset += actor.ActionDice.Count;
            }
        }

        // 一个行动者簇：自底向上 行动骰 →（名字）→ 冷静 →（带伤时的伤势）→ 状态说明。返回簇宽度。
        private static float DrawCluster(
            float x, float baseline, ActorSnapshot actor, int flatDieOffset, bool isLead,
            PresentationSnapshot snapshot, SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors,
            bool drawPortraits, bool drawForeground, IReadOnlyList<SlottedResource>? pendingAutoActionDice)
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
                string injuryText = snapshot.InjuryCostsActionDie
                    ? $"{snapshot.InjuryPart}伤 · 重伤 · −1颗骰"
                    : $"{snapshot.InjuryPart}伤 · {snapshot.InjurySkillName}{snapshot.InjurySkillPenalty}";
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
                    DrawNeonBust(bust, neon, uv, PortraitConditionOf(actor, isLead, snapshot),
                        gameManager.VitalLossAge(actor.Id));
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

            DrawComposureBar(x, composureY, 172f, actor, gameManager);
            if (showInjury)
            {
                // 伤势是队伍级的，只挂在主角这一簇上。
                DrawInjuryBar(x, injuryY, 172f, snapshot, gameManager);
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
            NoteDiceArrivals(actor, snapshot);
            for (int d = 0; d < diceCount; d++)
            {
                float dieX = x + actor.ActionDiceSlotIds[d] * TokenSpacing;
                DrawDie(new Rect(dieX, diceY, TokenSize, TokenSize), actor.ActionDice[d], flatDieOffset + d,
                        RollProgress(actor, d), SettleGlow(actor, d), gameManager, ui);
            }

            // 空掉的骰位要留在原地。骰子投出去以后 ActionDice 就少一条，但那一格是这个人
            // 「今天有几次出手」的量器——把它抹掉，四颗变三颗，玩家看见的是"我有三颗骰子"，
            // 而不是"我用掉了一颗"。代价（用掉的那一次）必须留下痕迹，否则花出去的东西
            // 在界面上从来没存在过。
            //
            // 空位画成一个没有底、没有影子的浅框：它是纸上的一个坑，不是一块可以拿的器物。
            // 重伤封掉的那一位另有画法（叉号，见 DrawDisabledDie）——"用掉了"和"用不了"
            // 是两回事，不能长得一样。
            int injuredSlotId = isLead && snapshot.InjuryCostsActionDie && actor.ActionSlotCount > 0
                ? actor.ActionSlotCount - 1
                : -1;
            for (int slotId = 0; slotId < actor.ActionSlotCount; slotId++)
            {
                if (ContainsActionSlot(actor.ActionDiceSlotIds, slotId))
                    continue;
                var slotRect = new Rect(x + slotId * TokenSpacing, diceY, TokenSize, TokenSize);
                // 这颗骰子已经被一个还没出场的 auto-action 提前扣走了（见调用方注释）；
                // 在玩家看见那张卡之前，这一格该看着还在手上，不是已经用掉了。
                int reservedValue = FindPendingReservedValue(pendingAutoActionDice, actor.Id, slotId);
                if (reservedValue > 0)
                    DrawMovingDie(slotRect, reservedValue);
                else if (slotId == injuredSlotId)
                    DrawDisabledDie(slotRect);
                else
                    DrawSpentDieSlot(slotRect);
            }

            anchors?.RegisterActor(actor.Id, actor.Name, new Rect(x, topY, clusterW, baseline - topY));
            return clusterW;
        }

        // 立在 HUD 里的霓虹半身像。没有边框、没有底板——衬托靠人物背后那团椭圆暗晕，
        // 它没有边界，所以人像是「从暗处走出来」而不是「贴在一块牌子上」。
        // 刻意不做呼吸和闪烁：常驻 HUD 上的动画会一直勾眼睛，那是对白舞台该干的事。
        internal static void DrawNeonBust(Rect rect, Texture2D neon, Rect uv)
            => DrawNeonBust(rect, neon, uv, PortraitCondition.Stable, float.PositiveInfinity);

        private static void DrawNeonBust(
            Rect rect, Texture2D neon, Rect uv, PortraitCondition condition = PortraitCondition.Stable,
            float damageAge = float.PositiveInfinity)
        {
            float conditionLevel = condition switch
            {
                PortraitCondition.LowComposure => 0.82f,
                PortraitCondition.LightInjury => 0.68f,
                PortraitCondition.SevereInjury => 0.50f,
                _ => 1f,
            };
            // 常驻状态只改变亮度；只有真实受击后的短窗口才走接触不良节奏。
            float level = conditionLevel
                * Mathf.Clamp(StageState.FlickerLevelAt(damageAge), 0.05f, 1.35f);

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
            GUI.color = new Color(0.30f, 0.58f, 1f, 0.18f * level);
            GUI.DrawTextureWithTexCoords(bleed, neon, uv, true);
            // 灯管本体叠两遍，理由同对白舞台：Alpha From Grayscale 下蓝管偏透。
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(level));
            GUI.DrawTextureWithTexCoords(rect, neon, uv, true);
            GUI.color = new Color(1f, 1f, 1f, 0.55f * Mathf.Clamp(level, 0f, 1.5f));
            GUI.DrawTextureWithTexCoords(rect, neon, uv, true);

            // 腰部是硬切口，用一段竖直渐变把它抹回暗处，不能让灯管断得那么直。
            var hem = new Rect(rect.x - 8f, rect.yMax - rect.height * 0.30f, rect.width + 16f, rect.height * 0.30f);
            GUI.color = new Color(0.004f, 0.007f, 0.016f, 0.96f);
            GUI.DrawTexture(hem, NeonPortraitLibrary.VerticalFade());
            GUI.color = Color.white;
        }

        private static PortraitCondition PortraitConditionOf(
            ActorSnapshot actor, bool isLead, PresentationSnapshot snapshot)
        {
            if (isLead && snapshot.InjurySeverity >= Injury.SevereThreshold)
                return PortraitCondition.SevereInjury;
            if (isLead && snapshot.InjurySeverity > 0)
                return PortraitCondition.LightInjury;
            if (actor.MaxComposure > 0 && actor.Composure * 3 <= actor.MaxComposure)
                return PortraitCondition.LowComposure;
            return PortraitCondition.Stable;
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

        private static void DrawComposureBar(
            float x, float y, float w, ActorSnapshot actor, SSNoirGameManager gameManager)
        {
            int composure = actor.Composure;
            int max = actor.MaxComposure;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.78f) },
            };
            IMGUIStyles.DrawLabel(new Rect(x, y, VitalLabelW, VitalRowH), UiText.Get("冷静"), labelStyle);

            // 阈值随上限走：见底是红的，剩三分之一以内是警告色。
            Color fill = composure <= 0 ? IMGUIStyles.SealRed
                       : composure * 3 <= max ? IMGUIStyles.OddsNeutral
                       : IMGUIStyles.TextPrimary;

            float barX = x + VitalLabelW + 8f;
            float barW = w - (VitalLabelW + 8f) - 40f;
            float barH = 9f;
            float barY = y + (VitalRowH - barH) / 2f;
            const float cellGap = 2f;
            float cellW = (barW - cellGap * (max - 1)) / max;

            // 值一变整条（格子 + 读数）绕中心弹一下，格子再一格一格过曝。
            float pop = gameManager.ComposurePop(actor.Id);
            Matrix4x4 savedMatrix = GUI.matrix;
            if (pop > 0f)
                GUIUtility.ScaleAroundPivot(Vector2.one * (1f + pop * 0.14f),
                    new Vector2(barX + (barW + 42f) * 0.5f, y + VitalRowH * 0.5f));

            for (int i = 0; i < max; i++)
            {
                var cell = new Rect(barX + i * (cellW + cellGap), barY, cellW, barH);
                DrawVitalCell(cell, i < composure, fill, gameManager.GetComposureCellPulse(actor.Id, i));
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
                composure > 0 ? $"{composure}/{max}" : UiText.Get("见底"), valStyle);
            if (pop > 0f)
                GUI.matrix = savedMatrix;
        }

        // 冷静 / 伤势条上的一格。两条轴共用这一个画法——它们的方向相反，但"变好变坏"
        // 该长成同一个样子（见 SSNoirGameManager 的脉冲注释）。
        //
        //   变坏：这一格过曝到白，0.25 秒硬落回本色。一下子的冲击。
        //   变好：这一格从本色柔柔渐隐一层光，0.4 秒。松一口气。
        //   接力未到：这一格先按**旧样子**画，等轮到它再带着闪光换过来。
        private static void DrawVitalCell(
            Rect cell, bool filledNow, Color fill, SSNoirGameManager.VitalPulse pulse)
        {
            bool filled = pulse.Pending ? !filledNow : filledNow;
            GUI.color = filled ? fill : Paper14;
            GUI.DrawTexture(cell, Texture2D.whiteTexture);

            if (pulse.Strength > 0f)
            {
                bool loss = pulse.Tone == SSNoirGameManager.VitalPulseTone.Loss;
                Color flash = loss ? Color.white : fill;
                float alpha = pulse.Strength * (loss ? 1f : 0.7f);
                GUI.color = new Color(flash.r, flash.g, flash.b, alpha);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        // ── 骰子落定 ───────────────────────────────────────────────────
        //
        // 拿到新骰子要有一个「停下来」的动作。卡里的判定条已经是这个语言：六格里有一格
        // 亮着，那一格就是结果。手牌这边原来是数字凭空换掉——玩家看不见"这颗骰子刚刚掷过"，
        // 只看见 HUD 变了个字。
        //
        // 所以让它从 1 起纵向滚，先匀速转、后减速停在点数上。
        //
        // 手感全在<b>两段</b>上：老虎机不是一条缓出曲线从头减到尾——那样一开始快得只剩闪烁，
        // 最后一小截才看得见在动。真正像的是「先以一个看得清的速度匀速转一阵，再慢下来
        // 咬住那一格」。所以 RollHoldFrac 那段是匀速的，之后才缓出，而且两段的速度在接缝处
        // 对齐（RollHoldDist 由此推出），否则接缝上会突然一顿或一窜。
        private const float RollDuration = 0.7f;
        private const float RollStagger  = 0.08f;   // 一颗接一颗停，不是齐刷刷落地
        private const int   RollSpins    = 2;
        private const float RollHoldFrac = 0.45f;   // 前 45% 的时间匀速转
        // 接缝处速度相等：a/h = 3(1−a)/(1−h)，h=0.45 解得 a≈0.71。改 RollHoldFrac 要一起改这个。
        private const float RollHoldDist = 0.71f;   // 匀速段吃掉 71% 的行程

        // 停住那一下的金色回闪。骰子落定是玩家该看见的一个事件，不是一个静止结果。
        private const float SettleFlash = 0.3f;

        private struct DieRollState
        {
            public int Value;
            public float StartedAt;
            public float SeenAt;
        }

        private static readonly Dictionary<string, DieRollState> _dieRolls = new Dictionary<string, DieRollState>();
        private static readonly Dictionary<string, int> _lastDiceCount = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _lastRollDay = new Dictionary<string, int>();

        // 骰位号是稳定的空间身份（见 ActionDiceSlotIds），所以拿它当键：
        // 骰子投出去时这一条消失，补上时又出现——那正好就是"获得了新骰子"。
        private static string RollKey(ActorSnapshot actor, int index)
            => actor.Id + ":" + actor.ActionDiceSlotIds[index];

        /// <summary>
        /// 这一帧哪几颗是新的。三种都算：
        /// 一是<b>换了一天</b>——每天早上那一把无条件重掷，哪怕昨天剩下的骰子点数一模一样；
        /// 二是这个人的骰子总数变多了（交锋里一轮一补，那一轮没有日期可依）；
        /// 三是这个骰位刚刚有了骰子，或者点数换了。
        ///
        /// 只靠"点数变了"是不够的：今天掷出 4、昨天也是 4，那一颗就会一动不动地待在那儿，
        /// 而它其实是新的一颗。日期和总数这两条是补这个洞的。
        /// </summary>
        private static void NoteDiceArrivals(ActorSnapshot actor, PresentationSnapshot snapshot)
        {
            float now = Time.time;
            int count = actor.ActionDice.Count;

            _lastDiceCount.TryGetValue(actor.Id, out int lastCount);
            bool replenished = count > lastCount;
            _lastDiceCount[actor.Id] = count;

            bool newDay = _lastRollDay.TryGetValue(actor.Id, out int lastDay) && lastDay != snapshot.WorldDay;
            _lastRollDay[actor.Id] = snapshot.WorldDay;

            bool refreshed = replenished || newDay;

            for (int d = 0; d < count; d++)
            {
                string key = RollKey(actor, d);
                int val = actor.ActionDice[d];
                if (_dieRolls.TryGetValue(key, out var state) && state.Value == val && !refreshed)
                {
                    state.SeenAt = now;
                    _dieRolls[key] = state;
                    continue;
                }
                _dieRolls[key] = new DieRollState { Value = val, StartedAt = now, SeenAt = now };
            }

            PruneStaleRolls(now);
        }

        /// <summary>0 = 还没开始滚，1 = 已经停住。第 d 颗晚 <see cref="RollStagger"/> 起步。</summary>
        private static float RollProgress(ActorSnapshot actor, int index)
        {
            if (!_dieRolls.TryGetValue(RollKey(actor, index), out var state))
                return 1f;
            float elapsed = Time.time - state.StartedAt - index * RollStagger;
            return Mathf.Clamp01(elapsed / RollDuration);
        }

        /// <summary>刚停住那一下的金色回闪：1 = 正落定，0 = 已经凉了。</summary>
        private static float SettleGlow(ActorSnapshot actor, int index)
        {
            if (!_dieRolls.TryGetValue(RollKey(actor, index), out var state))
                return 0f;
            float since = Time.time - (state.StartedAt + index * RollStagger + RollDuration);
            if (since < 0f || since > SettleFlash) return 0f;
            return 1f - since / SettleFlash;
        }

        /// <summary>
        /// 新回合的骰子是否还在滚动或落定回闪。回合开始的自动行动必须等这一段结束，
        /// 否则玩家会看见骰子一边重掷、一边被动作卡抓走，先后关系就倒了。
        /// </summary>
        public static bool HasUnsettledDice(PresentationSnapshot snapshot)
        {
            float now = Time.time;
            foreach (var actor in snapshot.Actors)
            {
                for (int index = 0; index < actor.ActionDice.Count; index++)
                {
                    if (_dieRolls.TryGetValue(RollKey(actor, index), out var state)
                        && now < state.StartedAt + index * RollStagger + RollDuration + SettleFlash)
                        return true;
                }
            }
            return false;
        }

        private static void PruneStaleRolls(float now)
        {
            if (_dieRolls.Count < 64) return;
            var stale = new List<string>();
            foreach (var pair in _dieRolls)
                if (now - pair.Value.SeenAt > 30f)
                    stale.Add(pair.Key);
            foreach (string key in stale)
                _dieRolls.Remove(key);
        }

        /// <summary>匀速一段，再三次方缓出；接缝处两段速度相等，所以中间不会顿一下。</summary>
        private static float RollEase(float t)
        {
            if (t <= RollHoldFrac)
                return RollHoldDist * (t / RollHoldFrac);
            float u = (t - RollHoldFrac) / (1f - RollHoldFrac);
            return RollHoldDist + (1f - RollHoldDist) * (1f - Mathf.Pow(1f - u, 3f));
        }

        /// <summary>
        /// 方块里那一列滚动的数字。从 1 起，转 <see cref="RollSpins"/> 圈之后正好落在点数上：
        /// 终点步数 ≡ val−1 (mod 6)，所以停下来那一格一定是它。
        /// 缓出用三次方——尾巴上那一下慢，"停住"才看得出来是停住，而不是被切断。
        /// </summary>
        private static void DrawRollingDigits(Rect rect, int finalValue, float t, bool disabled)
        {
            float eased = RollEase(Mathf.Clamp01(t));
            int endStep = RollSpins * 6 + Mathf.Clamp(finalValue - 1, 0, 5);
            float pos = eased * endStep;
            float cell = rect.height;

            var style = new GUIStyle(IMGUIStyles.SlotLabel)
            {
                fontSize = IMGUIStyles.FontSize(24),
                alignment = TextAnchor.MiddleCenter
            };
            IMGUIStyles.ApplyStrongFont(style);
            Color baseColor = disabled ? DisabledResourceText : IMGUIStyles.Paper;

            GUI.BeginClip(rect);
            int first = Mathf.FloorToInt(pos) - 1;
            for (int k = first; k <= first + 2; k++)
            {
                if (k < 0) continue;
                float dy = (k - pos) * cell;
                // 离中心越远越淡：一列数字滚过去，眼睛只跟得住中间那个。
                float fade = 1f - Mathf.Clamp01(Mathf.Abs(k - pos)) * 0.7f;
                style.normal.textColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade);
                IMGUIStyles.DrawLabel(new Rect(0f, dy, rect.width, cell), (k % 6 + 1).ToString(), style);
            }
            GUI.EndClip();
        }

        /// <summary>
        /// 落定那一下：描边和数字一起被推向金色，然后凉回去。
        /// 是回闪不是高亮——它一亮就在灭，所以读起来是"刚刚发生了一件事"，
        /// 而不是"这颗骰子被选中了"（那是金描边常驻的意思，两者不能撞）。
        /// </summary>
        private static void DrawSettleFlash(Rect rect, int val, float settle)
        {
            float k = settle * settle;   // 头上那一下亮，尾巴收得快
            var gold = IMGUIStyles.Gold;

            IMGUIStyles.DrawOutline(rect, 2f, new Color(gold.r, gold.g, gold.b, k));

            var style = new GUIStyle(IMGUIStyles.SlotLabel)
            {
                fontSize = IMGUIStyles.FontSize(24),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(gold.r, gold.g, gold.b, k) }
            };
            IMGUIStyles.ApplyStrongFont(style);
            IMGUIStyles.DrawLabel(rect, val.ToString(), style);
        }

        private static void DrawDie(Rect dieRect, int val, int globalIdx, float roll, float settle,
                                    SSNoirGameManager gameManager, IMGUIInteractionContext ui)
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

            // 还在滚：方块照画，只是中间那个数还没定下来。这半秒里它不接手——
            // 让玩家抓一颗点数还在跳的骰子，等于让他赌自己手快。
            if (roll < 1f)
            {
                DrawHandBlock(dieRect, string.Empty, null, false, false, disabled);
                DrawRollingDigits(dieRect, val, roll, disabled);
                return;
            }

            // 骰子与物品同一族方块：骰值居中（无下方标签）。
            DrawHandBlock(dieRect, val.ToString(), null, isSelected, hover, disabled);
            if (settle > 0f && !disabled && !isSelected)
                DrawSettleFlash(dieRect, val, settle);

            if (!disabled && ui.WasClicked(dieRect))
            {
                gameManager.BeginDieDrag(globalIdx, val, ui.Mouse);
                Event.current.Use();
            }
        }

        private static bool ContainsActionSlot(IReadOnlyList<int> slotIds, int slotId)
        {
            for (int i = 0; i < slotIds.Count; i++)
                if (slotIds[i] == slotId)
                    return true;
            return false;
        }

        // 骰值恰好也是它在骰子上的面数，天然大于 0；用 0 当"没找到"的哨兵不需要另开一个 bool。
        private static int FindPendingReservedValue(
            IReadOnlyList<SlottedResource>? pendingAutoActionDice, string actorId, int slotId)
        {
            if (pendingAutoActionDice == null)
                return 0;
            for (int i = 0; i < pendingAutoActionDice.Count; i++)
            {
                var die = pendingAutoActionDice[i];
                if (die.DieIndex == slotId && string.Equals(die.ActorId, actorId, StringComparison.OrdinalIgnoreCase))
                    return die.Value;
            }
            return 0;
        }

        // 用掉了的骰位：一个空框。比骰子暗得多，也不描第二道边、不投影——
        // 一眼看过去它属于底板那一层，数得出来但不抢手。
        private static void DrawSpentDieSlot(Rect rect)
        {
            IMGUIStyles.DrawOutline(rect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.18f));
        }

        // 伤势封掉的骰位：一道封条。
        //
        // 原来是个红叉。叉是记号笔画上去的东西——它属于表格和待办清单，不属于这张图纸；
        // 而且两笔交叉在 50 见方的方块里挤成一团，远看只是一个红点。
        // 封条不一样：它是<b>贴上去的一件东西</b>，斜着压过整格，一眼看出这一格被人封了，
        // 不是自己空的。城里贴封条的说法本来就在（拳场那扇门），HUD 用同一个动作。
        //
        // 底子和"用掉了"的空位是同一张：一样的浅框、一样没有底和影子。区别只有那一道红。
        // 同族、一处不同——这才读得出"这两格都没有骰子，但不是同一个原因"。
        private static void DrawDisabledDie(Rect rect)
        {
            var seal = IMGUIStyles.SealRed;
            IMGUIStyles.DrawOutline(rect, 1f, new Color(seal.r, seal.g, seal.b, 0.30f));

            // 斜带压过整格，两端留一点，让它像贴在框上而不是画在框里。
            const float inset = 5f;
            var a = new Vector2(rect.x + inset, rect.yMax - inset);
            var b = new Vector2(rect.xMax - inset, rect.y + inset);
            IMGUIStyles.DrawLine(a, b, new Color(seal.r, seal.g, seal.b, 0.22f), 9f);   // 纸带
            IMGUIStyles.DrawLine(a, b, new Color(seal.r, seal.g, seal.b, 0.70f), 2f);   // 带子上那道印
        }

        // 手牌方块（骰子 / 物品共用）：黑方块 + 描边 + 硬投影；大字（骰值或物品符号）在上/中，
        // 下方可选小标签（物品的数量/金额）。选中：金描边 + 上浮 + 金字；禁用：整体降到 35%。
        // capacitySegments > 0 时，方块底下那一行不写数量，改画一排容量刻度（见 DrawCapacityGauge）。
        private static void DrawHandBlock(
            Rect rect, string big, string? small, bool selected, bool hover, bool disabled,
            int capacitySegments = 0, int capacityFilled = 0, string? iconItem = null)
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
                        : Paper55;

            if (!disabled)
                IMGUIStyles.DrawShadow(drawRect, new Vector2(2f, 2f), 0.45f);
            GUI.color = disabled ? DisabledResourceBg : CardBlockBg;
            GUI.DrawTexture(drawRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(drawRect, !disabled && (selected || hover) ? 2f : 1f, border);

            Color content = disabled ? DisabledResourceText : selected ? IMGUIStyles.Gold : IMGUIStyles.TextPrimary;
            bool hasGauge = capacitySegments > 0;
            bool hasSmall = hasGauge || !string.IsNullOrEmpty(small);

            var bigStyle = new GUIStyle(IMGUIStyles.SlotLabel)
            {
                fontSize = IMGUIStyles.FontSize(24),
                alignment = hasSmall ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter,
                normal = { textColor = content }
            };
            IMGUIStyles.ApplyStrongFont(bigStyle);
            var bigRect = hasSmall ? new Rect(drawRect.x, drawRect.y + 3f, drawRect.width, 28f) : drawRect;
            // 有图标的物品画剪影，没有的（以及骰子）照旧写字。
            if (!ItemIconLibrary.TryDraw(bigRect, iconItem, content, IconScale))
                IMGUIStyles.DrawLabel(bigRect, big, bigStyle);

            if (hasGauge)
            {
                DrawCapacityGauge(
                    new Rect(drawRect.x + 7f, drawRect.y + 36f, drawRect.width - 14f, 5f),
                    capacitySegments, capacityFilled, content, disabled);
            }
            else if (hasSmall)
            {
                var smallStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = IMGUIStyles.FontSize(13),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = disabled ? DisabledResourceText : (selected ? IMGUIStyles.Gold : IMGUIStyles.TextPrimary) }
                };
                IMGUIStyles.ApplyStrongFont(smallStyle);
                // 方块高 50；旧的 y+34 / h18 实际画到方块外 2px，数量会压住底栏标题。
                IMGUIStyles.DrawLabel(new Rect(drawRect.x, drawRect.y + 31f, drawRect.width, 16f), small!, smallStyle);
            }
        }

        // 伤势细条：格数即刻度，越满越糟——和冷静条方向相反。重伤线用一道留白分开，
        // 让"再挨几下就跨过去"直接看得见。完好时整条不画，由调用方决定。
        private static void DrawInjuryBar(
            float x, float y, float w, PresentationSnapshot snapshot, SSNoirGameManager gameManager)
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
            float pop = gameManager.InjuryPop();
            Matrix4x4 savedMatrix = GUI.matrix;
            if (pop > 0f)
                GUIUtility.ScaleAroundPivot(Vector2.one * (1f + pop * 0.14f),
                    new Vector2(barX + (barW + 42f) * 0.5f, y + VitalRowH * 0.5f));
            for (int i = 0; i < max; i++)
            {
                float offset = i * (cellW + cellGap)
                             + (i >= Injury.SevereThreshold ? boundaryGap - cellGap : 0f);
                DrawVitalCell(new Rect(barX + offset, barY, cellW, barH),
                    i < severity, fill, gameManager.GetInjuryCellPulse(i));
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
            if (pop > 0f)
                GUI.matrix = savedMatrix;
        }

        // ── 右下：物品 + 功能 ──────────────────────────────────────────

        private static void DrawItemsAndFunctions(float baseline, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            bool isInEncounter = gameManager.DisplayedSnapshot.IsInEncounter;
            float functionW = FunctionWidth(gameManager);
            // 功能键按 64 画在手机上是一块很大的砖；跟手牌方块取齐，一排读起来才是一排。
            float functionH = TokenSize;
            float functionX = UIScale.SafeArea.xMax - SideMargin - functionW;
            float functionY = baseline - functionH;
            // 名字在方块上方；物品、回家与左侧行动骰共享同一条底线。
            float itemY = baseline - TokenSize;

            if (isInEncounter)
            {
                // 抽烟、喝酒是随身动作节点（engine.scm 的 carry-nodes，由 SceneManager 补进
                // 每一场交锋），在休息键上方画成带物品槽、行动骰槽和执行钮的标准卡。
                // 功能区只剩“结束回合”：它交出主动权，不吃任何资源。
                var restRect = new Rect(functionX, functionY, functionW, functionH);
                DrawnActionRects["结束回合"] = restRect;
                // 结束回合也是一次真的执行，进度在功能键上显示。
                // 它不是卡、没有执行钮，进度得自己画，否则场上唯一没有时间流逝的动作就是它。
                // 这个键只做一件事：交出主动权。按下去它就灰着，新一手交回手上才亮——
                // 现在是谁的回合由画面说（世界变冷、在动的卡亮），不由它写字。
                // 结算中和刚结算完的短冷却里都按不下去（CanRest）：连点不会把第二天也睡掉。
                if (DrawFunctionBlock(restRect, "结束回合", ui, !gameManager.CanRest))
                    gameManager.OnEndTurnClicked();
            }
            else
            {
                var homeRect = new Rect(functionX, functionY, functionW, functionH);
                // 已经在家就不该再点：NavigateToHome 本来就会直接返回，按钮只是把这说出来。
                bool alreadyAtHome = gameManager.NavigationStack.Any(node => node.Name == "家");
                if (DrawFunctionBlock(homeRect, "回 家", ui, alreadyAtHome))
                    gameManager.NavigateToHome();
            }

            // 物品排在功能键左侧，从右往左贴住。
            var snapshot = gameManager.DisplayedSnapshot;
            var items = new List<(string Name, int Qty)>();
            foreach (var kvp in snapshot.Inventory)
                if (kvp.Value > 0) items.Add((kvp.Key, kvp.Value));
            if (items.Count == 0) return;

            float itemsW = (items.Count - 1) * TokenSpacing + TokenSize;
            float itemsRightEdge = functionX - 28f;
            float startX = itemsRightEdge - itemsW;

            // 带用法的物品也保持可拖；玩家要把它明确放进随身消费卡的物品槽。
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemRect = new Rect(startX + i * TokenSpacing, itemY, TokenSize, TokenSize);
                int remaining = gameManager.GetRemainingItemQty(item.Name);
                bool isSelected = gameManager.SelectedResource != null
                               && gameManager.SelectedResource.Type == "item"
                               && gameManager.SelectedResource.ItemName == item.Name;
                bool hover = ui.CanHover(itemRect);
                float gainPulse = gameManager.GetItemGainPulse(item.Name);

                if (remaining <= 0)
                {
                    DrawItemBlock(itemRect, item.Name, isSelected, hover, remaining, disabled: true,
                        gainPulse: gainPulse, capacity: ItemCapacity(gameManager, item.Name));
                }
                else
                {
                    bool disabled = ui.IsLocked;
                    DrawItemBlock(itemRect, item.Name, isSelected, hover, remaining, disabled, gainPulse,
                        ItemCapacity(gameManager, item.Name));
                    if (!disabled && ui.WasClicked(itemRect))
                    {
                        // 拖拽态显示的是物品栏当前可用的数量；已放入槽位的数量不应又出现在 ghost 上。
                        gameManager.BeginItemDrag(item.Name, remaining, ui.Mouse);
                        Event.current.Use();
                    }
                }
            }

            if (isInEncounter)
                DrawEncounterActionCards(functionX + functionW, functionY, gameManager, ui);
        }

        // ── 随身消费动作（右下角的动作卡）─────────────────────────
        // 烟、酒复用普通动作卡：物品槽和行动骰槽都是八角消费槽，填满后出现执行按钮。
        // 唯一特殊之处是摆在随身区，不进入交锋自己的卡片网格。
        // 物品名在方块上方。
        private const float ItemNameRowH = 15f;

        private const float CarryCardW = FunctionBlockW;
        private const float CompactSlotSize = 44f;
        private const float CompactCardBaseH = 76f;
        private const float CompactExecuteH = 30f;
        private const float CompactExecuteGap = 5f;
        // 无需求槽的卡（关系支援）：只有标题 + 执行钮，不留骰位行的空位。
        // 标题区 3+22、下方留 6 间隙、执行钮 30、底边 5。
        private const float CompactNoSlotCardH = 3f + 22f + 6f + CompactExecuteH + 5f;
        private const float CarryCardGap = 12f;
        // 非场景交锋动作共用一列。物品动作只在持有对应物品时出现；关系支援已经由
        // engine.scm 按当前携带者筛过，用掉后保留禁用卡，让玩家看见本场次数已经耗尽。
        private static List<GameNode> ActiveEncounterActionNodes(SSNoirGameManager gameManager)
        {
            var list = new List<GameNode>();
            foreach (var node in gameManager.DisplayedSnapshot.CarryNodes)
            {
                if (!string.IsNullOrEmpty(node.SupportId))
                    list.Add(node);
                // 已经放进这张卡的最后一件物品仍属于玩家，直到按下执行才消费；不能用
                // GetRemainingItemQty 判断，否则最后一根烟一落槽，整张卡会带着两个槽一起消失。
                else if (gameManager.DisplayedSnapshot.Inventory.TryGetValue(node.CarryItemId, out var owned)
                         && owned > 0)
                    list.Add(node);
            }
            return list;
        }

        private static Rect EncounterActionCardRect(
            IReadOnlyList<GameNode> nodes, int index, float right, float bottom,
            SSNoirGameManager gameManager)
        {
            float y = bottom - CarryCardGap;
            for (int i = 0; i <= index; i++)
            {
                int requireCount = nodes[i].Requires?.Count ?? 0;
                bool hasRequirements = requireCount > 0;
                var slots = gameManager.GetSlotsForNode(nodes[i].Name);
                bool armed = !hasRequirements || (slots != null && slots.Any(slot => slot != null));
                float h = hasRequirements
                    ? CompactCardBaseH + (armed ? CompactExecuteGap + CompactExecuteH : 0f)
                    : CompactNoSlotCardH;
                y -= h;
                if (i == index)
                    return new Rect(right - CarryCardW, y, CarryCardW, h);
                y -= CarryCardGap;
            }
            throw new InvalidOperationException("随身消费卡索引越界");
        }

        private static void DrawEncounterActionCards(
            float right, float bottom, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var nodes = ActiveEncounterActionNodes(gameManager);
            for (int i = 0; i < nodes.Count; i++)
                DrawEncounterActionCard(nodes, i, right, bottom, gameManager, ui);
        }

        private static void DrawEncounterActionCard(
            IReadOnlyList<GameNode> nodes, int index, float right, float bottom,
            SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var node = nodes[index];
            var rect = EncounterActionCardRect(nodes, index, right, bottom, gameManager);
            DrawnActionRects[node.Name] = rect;
            if (!string.IsNullOrEmpty(node.CarryItemId)
                && (node.Requires == null || node.Requires.Count != 2))
                throw new InvalidOperationException(
                    $"随身消费动作「{node.Name}」必须正好有物品和行动骰两个需求槽（见 engine.scm 的 随身动作）");

            var slots = gameManager.GetSlotsForNode(node.Name) ?? new List<SlottedResource?>();
            var execution = gameManager.GetExecutionState(node.Name);
            bool disabled = ui.IsLocked || node.Disabled;

            GUI.color = disabled ? DisabledResourceBg : CardBlockBg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f,
                !disabled && ui.CanHover(rect) ? IMGUIStyles.Paper : Paper35);

            var titleStyle = new GUIStyle(IMGUIStyles.SectionLabel)
            {
                fontSize = IMGUIStyles.FontSize(16),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = disabled ? DisabledResourceText : IMGUIStyles.Paper }
            };
            IMGUIStyles.ApplyStrongFont(titleStyle);
            IMGUIStyles.DrawLabel(new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, 22f), node.DisplayTitle, titleStyle);

            var requirements = node.Requires ?? new List<SSNoir.Core.ActionCost>();
            if (!string.IsNullOrEmpty(node.SupportId) && requirements.Count != 0)
                throw new InvalidOperationException(
                    $"关系支援「{node.Name}」不应有需求槽（见 engine.scm 的 support-frank），实际有 {requirements.Count} 个。");
            if (string.IsNullOrEmpty(node.SupportId) && string.IsNullOrEmpty(node.CarryItemId))
                throw new InvalidOperationException(
                    $"非场景交锋动作「{node.Name}」必须声明 :carry-item 或 :support。");
            int count = requirements.Count;
            float slotsW = count * CompactSlotSize + Mathf.Max(0, count - 1) * 6f;
            float slotX = rect.center.x - slotsW * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var slotRect = new Rect(slotX + i * (CompactSlotSize + 6f), rect.y + 27f,
                    CompactSlotSize, CompactSlotSize);
                var resource = i < slots.Count ? slots[i] : null;
                var interaction = ActionNodeDrawer.DrawStandaloneSlot(
                    slotRect, node, i, requirements[i], resource, disabled, ui, gameManager);
                if (interaction.ClickedSlotIndex != -1)
                    gameManager.OnSlotClicked(node, i);
                if (interaction.DroppedSlotIndex != -1 && gameManager.TryPlaceSelectedResource(node, i))
                    gameManager.MarkResourceDropHandled();
            }

            bool allFilled = count == 0 || (slots.Count == count && slots.All(slot => slot != null));
            bool armed = count == 0 || slots.Any(slot => slot != null) || execution.IsExecuting;
            if (!armed) return;

            var executeRect = new Rect(rect.x + 5f, rect.yMax - CompactExecuteH - 5f,
                rect.width - 10f, CompactExecuteH);
            if (execution.IsExecuting)
                DrawFunctionProgress(executeRect, execution.Progress, "执行中");
            else if (DrawFunctionBlock(executeRect, "执 行", ui, disabled || !allFilled))
                gameManager.ExecuteNodeAction(node);
        }


        // 休息和紧凑动作的执行中状态共用同一套语言：Ink 底、从左向右的金色填充、稳定描边。
        // 外扩呼吸框属于聚焦/目标提示，不用于这种固定功能区里的进度反馈。
        private static void DrawFunctionProgress(Rect rect, float progress, string label = "结算中")
        {
            progress = Mathf.Clamp01(progress);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = IMGUIStyles.Gold;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * progress, rect.height - 4f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.Gold);

            var style = new GUIStyle(IMGUIStyles.ExecuteLabel)
            {
                fontSize = IMGUIStyles.FontSize(16),
                normal = { textColor = progress > 0.5f ? IMGUIStyles.GoldOnDark : IMGUIStyles.Paper },
            };
            IMGUIStyles.DrawLabel(rect, ShownFunctionLabel(label), style);
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
            Color border = disabled ? Paper35 : (hover ? IMGUIStyles.Paper : Paper55);
            IMGUIStyles.DrawOutline(rect, hover ? 2f : 1f, border);
            var style = new GUIStyle(IMGUIStyles.ExecuteLabel)
            {
                fontSize = IMGUIStyles.FontSize(16),
                normal = { textColor = disabled ? DisabledResourceText : IMGUIStyles.TextPrimary }
            };
            IMGUIStyles.DrawLabel(rect, ShownFunctionLabel(label), style);
            if (!disabled && ui.WasTapped(rect))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private static string ShownFunctionLabel(string label) => label switch
        {
            "结束回合" => UiText.Get("结束回合"),
            "回 家" => UiText.Get("回 家"),
            "执 行" => UiText.Get("执 行"),
            "执行中" => UiText.Get("执行中"),
            "结算中" => UiText.Get("结算中"),
            _ => label,
        };

        // 这件东西带不带上限。0 = 不带（绝大多数物品）。表在 engine.scm 的 item-capacities。
        private static int ItemCapacity(SSNoirGameManager gameManager, string name)
            => gameManager.DisplayedSnapshot.ItemCapacities.TryGetValue(name, out var cap) ? cap : 0;

        // 容量刻度：一格一件，装满的实心，空的暗着。和冷静/伤势条是同一种读法——
        // 这里的"满"没有好坏，只是到头了，所以不换颜色，靠格子自己说话。
        private static void DrawCapacityGauge(Rect rect, int segments, int filled, Color content, bool disabled)
        {
            const float gap = 2f;
            float cellW = (rect.width - gap * (segments - 1)) / segments;
            Color on = disabled ? DisabledResourceText : content;
            for (int i = 0; i < segments; i++)
            {
                GUI.color = i < filled ? on : Paper25;
                GUI.DrawTexture(new Rect(rect.x + i * (cellW + gap), rect.y, cellW, rect.height),
                    Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        // 物品：与骰子共用手牌方块，大字=类别符号，小标签=数量/金额（金钱用 $，其它取首字）。
        private static void DrawItemBlock(
            Rect itemRect, string name, bool isSelected, bool hover, int remaining, bool disabled, float gainPulse,
            int capacity = 0)
        {
            if (gainPulse > 0f)
            {
                // 获得物品是一次「落定」，不是持续状态：所以不做呼吸（呼吸说的是"这东西还在进行中"）。
                // 但也不该是一记冲击——东西是稳稳落进物品栏的，不是砸进来的。
                // 整条动画只做一遍：轻轻沉到位，金边慢慢退掉，看完就没了。
                float t = 1f - gainPulse;                       // 0 → 1 的推进
                // 前 45% 收位，二次缓出：开头就慢，越靠近原位越慢，落地没有"啪"。
                float settle = Mathf.Clamp01(t / 0.45f);
                float over = 3f * (1f - settle) * (1f - settle); // 起手只外扩 3px
                itemRect = new Rect(
                    itemRect.x - over,
                    itemRect.y - over,
                    itemRect.width + over * 2f,
                    itemRect.height + over * 2f);
            }

            // 带上限的东西（现在只有烟）不写数字，画一排刻度：几格满了、还剩几格，一眼就是
            // 「这东西囤不了」。上限只有少数几样有（engine.scm 的 item-capacities），
            // 所以这不是给每个物品格都加一层容量——没上限的照旧只写数量。
            if (capacity > 0)
            {
                DrawHandBlock(itemRect, ItemSymbol(name), null, isSelected, hover, disabled,
                    capacitySegments: capacity, capacityFilled: remaining, iconItem: name);
            }
            else
            {
                string smallLabel = name == "金钱" ? $"${remaining}" : $"x{remaining}";
                DrawHandBlock(itemRect, ItemSymbol(name), smallLabel, isSelected, hover, disabled, iconItem: name);
            }

            DrawItemCaption(itemRect, name, isSelected, disabled);

            // 金边必须画在方块之后，否则被填充盖掉。单圈 1px，一路退光到没有：
            // 全程没有 sin，就没有"还在喘气"的余味；也没有白闪——那是冲击的语气，这里不要。
            if (gainPulse > 0f)
            {
                float rim = Mathf.Sqrt(Mathf.Clamp01(gainPulse)); // 先亮着不动，最后才慢慢化掉
                IMGUIStyles.DrawOutline(
                    itemRect, 1f,
                    new Color(IMGUIStyles.Gold.r, IMGUIStyles.Gold.g, IMGUIStyles.Gold.b, 0.85f * rim));
            }
        }

        // 图标只占大字那一格的 8 成：剪影的视觉重量本来就比字重，占满会把方块撑得发闷。
        private const float IconScale = 0.80f;

        // 方块外上方那行名字。长名字在这儿会被收短（见 ItemDisplayName）——收的只是显示，
        // 内容脚本里的名字一个字都没动。
        private static void DrawItemCaption(Rect itemRect, string name, bool selected, bool disabled)
        {
            var style = new GUIStyle(IMGUIStyles.SlotLabel)
            {
                fontSize = IMGUIStyles.FontSize(11),
                alignment = TextAnchor.LowerCenter,
                normal =
                {
                    textColor = disabled
                        ? DisabledResourceText
                        : selected ? IMGUIStyles.Gold : IMGUIStyles.TextSecondary
                }
            };
            IMGUIStyles.DrawLabel(
                new Rect(itemRect.x - 4f, itemRect.y - ItemNameRowH - 2f, itemRect.width + 8f, ItemNameRowH),
                ItemDisplayName.Short(name), style);
        }

        private static string ItemSymbol(string name)
        {
            return name switch
            {
                "金钱" => "$",
                "酒" => UiText.Get("酒"),
                "香烟" => UiText.Get("烟"),
                "药品" => UiText.Get("药"),
                _ => name.Length > 0 ? name.Substring(0, 1) : "?"
            };
        }
    }
}
