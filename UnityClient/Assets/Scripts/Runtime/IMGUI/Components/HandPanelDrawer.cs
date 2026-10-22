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
        public const float HandDieSize = TokenSize - 8f;
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
                    rect = DieInsideSlot(new Rect(x + slotId * TokenSpacing, baseline - TokenSize, TokenSize, TokenSize));
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
            DrawHandDie(rect, value, false, false, false);
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
            return Mathf.Max(Mathf.Max(isLead ? 248f : 150f, diceW), 200f);
        }

        // 功能键两边都只有一块：交锋里是「休息」，世界里是「回家」。
        // 功能键保持紧凑；随身消费动作已经改成上方的标准卡，不再决定这一列的宽度。
        private const float FunctionBlockW = 108f;
        private static float FunctionWidth(SSNoirGameManager gameManager) => FunctionBlockW;

        private const float NameRowH   = 20f;   // 只在没有半身像时才占位
        private const float VitalRowH  = 36f;   // 冷静 / 伤势条那一行；字比以前大一号
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
            VitalHintDrawer.Begin(ui);
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
            // 主角常驻伤势条，和冷静条共用生命状态说明。
            bool showInjury = isLead;

            float diceY = baseline - TokenSize;
            float nameY = diceY - NameRowH - 4f;
            float composureY = (drawName ? nameY : diceY - 4f) - VitalRowH - 2f;
            float injuryY = showInjury ? composureY - VitalRowH - 2f : composureY;
            string statusText = DescribeSlotStatuses(actor);
            bool hasStatus = statusText.Length > 0;
            float statusY = hasStatus ? injuryY - 18f : injuryY;
            float topY = statusY;
            var speakingAnchor = new Rect(x, topY, clusterW, baseline - topY);

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
                // 对话指向人像头部，而非包含骰子与状态条的整簇区域。
                speakingAnchor = new Rect(bust.center.x - 12f, bust.y, 24f, 24f);
                if (drawPortraits)
                    DrawNeonBust(bust, neon, uv, PortraitConditionOf(actor, isLead, snapshot),
                        gameManager.VitalLossAge(actor.Id));
                topY = bust.y;
            }

            if (!drawForeground)
                return clusterW;

            // 状态条到行动位共用一块暗底，盖住人像下缘与场景细线；人像上部仍留在城市里。
            const float backdropPadding = 6f;
            var backdrop = new Rect(x - backdropPadding, statusY - backdropPadding,
                clusterW + backdropPadding * 2f, baseline - statusY + backdropPadding * 2f);
            IMGUIStyles.SetColor(IMGUIStyles.FunctionSlotBg);
            GUI.DrawTexture(backdrop, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();

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

            DrawComposureBar(x, composureY, 200f, actor, gameManager);
            var composureRect = new Rect(x, composureY, 200f, VitalRowH);
            VitalHintDrawer.Offer(composureRect, actor, ui);
            if (isLead)
            {
                // 刻度右侧留 8px，立体骰子底边与冷静行齐平；距行动位保留完整行间距。
                const float rangeSize = 40f;
                var rangeRect = new Rect(composureRect.xMax + 8f,
                    composureRect.yMax - rangeSize, rangeSize, rangeSize);
                DiceCubeDrawer.DrawRange(rangeRect, 6);
                VitalHintDrawer.Offer(rangeRect, actor, ui);
            }
            if (showInjury)
            {
                // 伤势是队伍级的，只挂在主角这一簇上。
                DrawInjuryBar(x, injuryY, 200f, snapshot, gameManager);
                VitalHintDrawer.Offer(new Rect(x, injuryY, 200f, VitalRowH), actor, ui);
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

            // 行动位是固定容器，骰子是其中每轮发放的物件。先画槽，再画骰子。
            int blockedFromSlot = actor.ActionSlotCount - (isLead ? snapshot.InjuryActionDieLoss : 0);
            for (int slotId = 0; slotId < actor.ActionSlotCount; slotId++)
            {
                var slotRect = new Rect(x + slotId * TokenSpacing, diceY, TokenSize, TokenSize);
                DrawActionSlot(slotRect, slotId >= blockedFromSlot);
                if (!ContainsActionSlot(actor.ActionDiceSlotIds, slotId))
                {
                    int reserved = FindPendingReservedValue(pendingAutoActionDice, actor.Id, slotId);
                    if (reserved > 0) DrawMovingDie(DieInsideSlot(slotRect), reserved);
                }
            }
            NoteDiceArrivals(actor);
            for (int d = 0; d < diceCount; d++)
            {
                var slot = new Rect(x + actor.ActionDiceSlotIds[d] * TokenSpacing, diceY, TokenSize, TokenSize);
                DrawDie(DieInsideSlot(slot), actor.ActionDice[d], flatDieOffset + d,
                    RollProgress(actor, d), SettleGlow(actor, d), gameManager, ui);
            }

            anchors?.RegisterActor(actor.Id, actor.Name, speakingAnchor);
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
            if (actor.IsDrunk) labels.Add("醉酒");
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
            float x,float y,float w,ActorSnapshot actor,SSNoirGameManager gameManager)
        {
            Color fill = IMGUIStyles.Paper;
            DrawVitalTitle(x,y,"冷静");
            if (actor.Id == "player" && ActionRiskFocus.IsComposureFocused())
                IMGUIStyles.DrawOutline(new Rect(x, y, w, 24f), 1f, IMGUIStyles.Gold);
            float barX=x+VitalLabelW+8f,barW=w-(VitalLabelW+8f);
            const float gap=2f;
            DrawVitalTrack(new Rect(barX-2f,y+2f,barW+4f,16f), actor.Composure == 0);
            float cellW=(barW-gap*(actor.MaxComposure-1))/actor.MaxComposure;
            for(int i=0;i<actor.MaxComposure;i++)
            {
                var cell=new Rect(barX+i*(cellW+gap),y+4f,cellW,12f);
                DrawVitalCell(cell,i<actor.Composure,fill,gameManager.GetComposureCellPulse(actor.Id,i));
            }
        }

        private static void DrawVitalTrack(Rect rect, bool depleted)
        {
            IMGUIStyles.SetColor(new Color(.006f,.008f,.012f,.94f));
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            IMGUIStyles.DrawOutline(rect,1f,IMGUIStyles.FunctionSlotLine);
            if(depleted)
            {
                var red=IMGUIStyles.OddsFail; red.a=.6f;
                IMGUIStyles.SetColor(red);
                GUI.DrawTexture(new Rect(rect.x+2f,rect.y+2f,rect.width-4f,rect.height-4f),Texture2D.whiteTexture);
            }
            IMGUIStyles.ResetColor();
        }

        private static void DrawVitalTitle(float x,float y,string title)
        {
            var style=new GUIStyle(IMGUIStyles.CardSubtitle) {fontSize=IMGUIStyles.FontSize(15),
                alignment=TextAnchor.MiddleLeft,normal={textColor=IMGUIStyles.TextSecondary}};
            IMGUIStyles.DrawLabel(new Rect(x,y,VitalLabelW,22f),title,style);
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
            GUI.color = filled ? fill : Color.clear;
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
        // 立体骰子短暂转动，结果面朝前后落入固定行动位；结果仍由引擎先行决定。
        private const float RollDuration = 0.7f;
        private const float RollStagger  = 0.08f;   // 一颗接一颗停，不是齐刷刷落地

        // 停住那一下的金色回闪。骰子落定是玩家该看见的一个事件，不是一个静止结果。
        private const float SettleFlash = 0.3f;

        private struct DieRollState
        {
            public int Value;
            public long Revision;
            public float StartedAt;
            public float SeenAt;
        }

        private static readonly Dictionary<string, DieRollState> _dieRolls = new Dictionary<string, DieRollState>();
        // 位置决定骰子的空间身份；Revision 决定这是否是重新发放的一手。
        private static string RollKey(ActorSnapshot actor, int index)
            => actor.Id + ":" + actor.ActionDiceSlotIds[index];

        public static void ResetDiceAnimations() => _dieRolls.Clear();

        private static void NoteDiceArrivals(ActorSnapshot actor)
        {
            float now = Time.unscaledTime;
            for (int d = 0; d < actor.ActionDice.Count; d++)
            {
                string key = RollKey(actor, d);
                int val = actor.ActionDice[d];
                if (_dieRolls.TryGetValue(key, out var state)
                    && state.Revision == actor.ActionDiceRevision && state.Value == val)
                {
                    state.SeenAt = now;
                    _dieRolls[key] = state;
                    continue;
                }
                _dieRolls[key] = new DieRollState
                {
                    Value = val, Revision = actor.ActionDiceRevision, StartedAt = now, SeenAt = now
                };
            }
            PruneStaleRolls(now);
        }

        /// <summary>0 = 还没开始滚，1 = 已经停住。第 d 颗晚 <see cref="RollStagger"/> 起步。</summary>
        private static float RollProgress(ActorSnapshot actor, int index)
        {
            if (!_dieRolls.TryGetValue(RollKey(actor, index), out var state))
                return 1f;
            float elapsed = Time.unscaledTime - state.StartedAt - actor.ActionDiceSlotIds[index] * RollStagger;
            return Mathf.Clamp01(elapsed / RollDuration);
        }

        /// <summary>刚停住那一下的金色回闪：1 = 正落定，0 = 已经凉了。</summary>
        private static float SettleGlow(ActorSnapshot actor, int index)
        {
            if (!_dieRolls.TryGetValue(RollKey(actor, index), out var state))
                return 0f;
            float since = Time.unscaledTime - (state.StartedAt + actor.ActionDiceSlotIds[index] * RollStagger + RollDuration);
            if (since < 0f || since > SettleFlash) return 0f;
            return 1f - since / SettleFlash;
        }

        /// <summary>
        /// 新回合的骰子是否还在滚动或落定回闪。回合开始的自动行动必须等这一段结束，
        /// 否则玩家会看见骰子一边重掷、一边被动作卡抓走，先后关系就倒了。
        /// </summary>
        public static bool HasUnsettledDice(PresentationSnapshot snapshot)
        {
            float now = Time.unscaledTime;
            bool unsettled = false;
            foreach (var actor in snapshot.Actors)
            {
                if (!actor.OnStage) continue;
                // 等待方也登记新一手，不依赖 OnGUI 是否已画过，避免自动行动抢在滚骰前开始。
                NoteDiceArrivals(actor);
                for (int index = 0; index < actor.ActionDice.Count; index++)
                {
                    if (_dieRolls.TryGetValue(RollKey(actor, index), out var state)
                        && now < state.StartedAt + actor.ActionDiceSlotIds[index] * RollStagger + RollDuration + SettleFlash)
                        unsettled = true;
                }
            }
            return unsettled;
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

        private static void DrawRollingDie(Rect rect, int finalValue, float progress)
        {
            DrawSpentDieSlot(rect);
            float landing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, 1f, progress));
            float side = rect.width * Mathf.Lerp(1.05f, 1.02f, landing);
            var cube = new Rect(rect.center.x-side/2f, rect.center.y-side/2f-12f*(1f-landing), side, side);
            DiceCubeDrawer.DrawRolling(cube, finalValue, progress);
        }

        /// <summary>
        /// 落定那一下：描边被推向金色，然后凉回去。
        /// 是回闪不是高亮——它一亮就在灭，所以读起来是"刚刚发生了一件事"，
        /// 而不是"这颗骰子被选中了"（那是金描边常驻的意思，两者不能撞）。
        /// </summary>
        private static void DrawSettleFlash(Rect rect, int val, float settle)
        {
            float k = settle * settle;   // 头上那一下亮，尾巴收得快
            var gold = IMGUIStyles.Gold;

            IMGUIStyles.DrawOutline(rect, 2f, new Color(gold.r, gold.g, gold.b, k));


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
                DrawDieFace(dieRect, val, disabled: true, showBackground: false, opacity: 0.6f);
                return;
            }

            bool disabled = ui.IsLocked;

            // 还在滚：方块照画，只是中间那个数还没定下来。这半秒里它不接手——
            // 让玩家抓一颗点数还在跳的骰子，等于让他赌自己手快。
            if (roll < 1f)
            {
                DrawHandBlock(dieRect, string.Empty, null, false, false, disabled);
                DrawRollingDie(dieRect, val, roll);
                return;
            }

            // 骰子与物品同一族方块：骰值居中（无下方标签）。
            DrawHandDie(dieRect, val, isSelected, hover, disabled);
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

        public static Rect DieInsideSlot(Rect rect)
            => new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);

        public static void DrawActionSlot(Rect rect, bool blocked)
        {
            if (blocked) DrawDisabledDie(rect);
            else IMGUIStyles.DrawOutline(rect, 1f, Paper14);
        }

        // 伤势封位：红色虚线和裂纹；用掉的槽仍是淡色空框。
        public static void DrawDisabledDie(Rect rect, bool muted = false)
        {
            var red = muted ? Paper35 : IMGUIStyles.OddsFail;
            if (!muted) red.a = .85f;
            GUI.color = muted ? new Color(0f,0f,0f,0f) : new Color(IMGUIStyles.OddsFail.r,IMGUIStyles.OddsFail.g,IMGUIStyles.OddsFail.b,.15f);
            GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color = Color.white;
            const float dash=6f, gap=5f;
            for(float d=0;d<rect.width;d+=dash+gap)
            {
                float n=Mathf.Min(dash,rect.width-d);
                IMGUIStyles.DrawLine(new Vector2(rect.x+d,rect.y),new Vector2(rect.x+d+n,rect.y),red,1.5f);
                IMGUIStyles.DrawLine(new Vector2(rect.x+d,rect.yMax),new Vector2(rect.x+d+n,rect.yMax),red,1.5f);
            }
            for(float d=0;d<rect.height;d+=dash+gap)
            {
                float n=Mathf.Min(dash,rect.height-d);
                IMGUIStyles.DrawLine(new Vector2(rect.x,rect.y+d),new Vector2(rect.x,rect.y+d+n),red,1.5f);
                IMGUIStyles.DrawLine(new Vector2(rect.xMax,rect.y+d),new Vector2(rect.xMax,rect.y+d+n),red,1.5f);
            }
            Vector2 a=new(rect.x+rect.width*.60f,rect.y+2f), b=new(rect.x+rect.width*.43f,rect.y+rect.height*.37f);
            Vector2 c=new(rect.x+rect.width*.61f,rect.y+rect.height*.55f), d2=new(rect.x+rect.width*.40f,rect.yMax-2f);
            IMGUIStyles.DrawLine(a,b,red,1.5f); IMGUIStyles.DrawLine(b,c,red,1.5f); IMGUIStyles.DrawLine(c,d2,red,1.5f);
        }

        // 手牌方块（骰子 / 物品共用）：黑方块 + 描边 + 硬投影；大字（骰值或物品符号）在上/中，
        // 下方可选小标签（物品的数量/金额）。选中：金描边 + 上浮 + 金字；禁用：整体降到 35%。
        // capacitySegments > 0 时，方块底下那一行不写数量，改画一排容量刻度（见 DrawCapacityGauge）。
        // 用户选定的 A 配色（sRGB）：低 #07090C/#FFFFFF/#606974，
        // 中 #C0A782/#1D1B19/#C3A273，高 #FFDD94/#1D1B19/#F1D8A6。
        // 转换为 Linear 后送入 IMGUI，避免深色骰点和金底被抬灰。
        private static readonly Color[] DieBackgrounds = {
            new Color32(7, 9, 12, 255), new Color32(192, 167, 130, 255), new Color32(255, 221, 148, 255)
        };
        private static readonly Color[] DieBorders = {
            new Color32(96, 105, 116, 255), new Color32(195, 162, 115, 255), new Color32(241, 216, 166, 255)
        };
        /// <summary>骰子配色偏好，由现有存档 Settings 保存。</summary>
        public static bool ColorDicePipsOnly { get; set; } = true;
        private static readonly Color PipOnlyBackground = new Color32(30, 33, 37, 255);
        private static readonly Color PipOnlyBorder = new Color32(80, 87, 97, 255);
        private static readonly Color[] PipOnlyColors = {
            new Color32(163, 173, 182, 255), new Color32(195, 160, 109, 255), new Color32(245, 211, 141, 255)
        };
        private static Texture2D? _diePipTexture;

        private static void DrawHandDie(Rect rect, int value, bool selected, bool hover, bool disabled)
        {
            if (selected && !disabled) rect.y -= 4f;
            if (!disabled) IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), 0.45f);
            Color? border = !disabled && selected ? IMGUIStyles.Gold
                : !disabled && hover ? IMGUIStyles.Paper : (Color?)null;
            DrawDieFace(rect, value, disabled, border);
        }

        public static void GetDieColors(int value, out Color background, out Color border, out Color pip)
        {
            UnityEngine.Assertions.Assert.IsTrue(value >= 1 && value <= 6);
            int tier = (value - 1) / 2;
            background = (ColorDicePipsOnly ? PipOnlyBackground : DieBackgrounds[tier]).linear;
            border = (ColorDicePipsOnly ? PipOnlyBorder : DieBorders[tier]).linear;
            pip = (ColorDicePipsOnly ? PipOnlyColors[tier] : tier == 0 ? Color.white : (Color)new Color32(29,27,25,255)).linear;
        }

        public static void DrawDieFace(Rect rect, int value, bool disabled = false,
            Color? borderOverride = null, bool octagon = false, bool showBackground = true, float opacity = 1f, bool neutral = false, Color? pipOverride = null)
        {
            UnityEngine.Assertions.Assert.IsTrue(value >= 1 && value <= 6, "骰面必须在 1～6 之间");
            GetDieColors(value, out var background, out var border, out var pip);
            if (neutral) { background = CardBlockBg; border = Paper55; pip = IMGUIStyles.Paper; }
            if (disabled) { background = DisabledResourceBg; border = Paper35; pip = DisabledResourceText; }
            if (borderOverride.HasValue) border = borderOverride.Value;
            if (pipOverride.HasValue) pip = pipOverride.Value;
            pip.a *= opacity;
            Color previous = GUI.color;
            if (showBackground)
            {
                if (octagon)
                {
                    ShapeDrawer.DrawOctagon(rect, background);
                    ShapeDrawer.DrawOctagonOutline(rect, borderOverride.HasValue ? 2f : 1f, border);
                }
                else
                {
                    GUI.color = background;
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    IMGUIStyles.DrawOutline(rect, borderOverride.HasValue ? 2f : 1f, border);
                }
            }
            if (_diePipTexture == null)
            {
                const int size = 32;
                _diePipTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "DiePip", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float distance = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude;
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(size / 2f - 1f - distance));
                    }
                _diePipTexture.SetPixels(pixels);
                _diePipTexture.Apply(false, true);
            }
            float side = Mathf.Min(rect.width, rect.height);
            float spacing = side * 0.22f;
            float diameter = side * 0.14f;
            GUI.color = pip;
            for (int row = -1; row <= 1; row++)
                for (int column = -1; column <= 1; column++)
                {
                    bool center = row == 0 && column == 0;
                    bool diagonal = row != 0 && row == column;
                    bool corner = row != 0 && column != 0;
                    bool dot = (value % 2 == 1 && center) || (value >= 2 && diagonal)
                        || (value >= 4 && corner) || (value == 6 && row == 0 && column != 0);
                    if (dot)
                        GUI.DrawTexture(new Rect(rect.center.x + column * spacing - diameter / 2f,
                            rect.center.y + row * spacing - diameter / 2f, diameter, diameter), _diePipTexture);
                }
            GUI.color = previous;
        }

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

        // 伤势细条：格数即刻度，越满越糟——和冷静条方向相反。在封位阈值下标出受损行动位；
        // 完好时整条不画，由调用方决定。
        private static void DrawInjuryBar(
            float x,float y,float w,PresentationSnapshot snapshot,SSNoirGameManager gameManager)
        {
            DrawVitalTitle(x,y,"伤势");
            if (ActionRiskFocus.IsInjuryFocused())
                IMGUIStyles.DrawOutline(new Rect(x, y, w, 24f), 1f, IMGUIStyles.Gold);
            int max=snapshot.InjuryMaxSeverity,severity=snapshot.InjurySeverity;
            Color fill=IMGUIStyles.OddsFail;
            float barX=x+VitalLabelW+8f,barW=w-(VitalLabelW+8f);
            const float gap=2f;
            DrawVitalTrack(new Rect(barX-2f,y+2f,barW+4f,16f),false);
            float cellW=(barW-gap*(max-1))/max;
            for(int i=0;i<max;i++)
            {
                var cell=new Rect(barX+i*(cellW+gap),y+4f,cellW,12f);
                DrawVitalCell(cell,i<severity,fill,gameManager.GetInjuryCellPulse(i));
                if((i+1)==Injury.FirstActionSlotLossThreshold
                    || (i+1)==Injury.SecondActionSlotLossThreshold)
                    DrawDisabledDie(new Rect(cell.center.x-8f,y+18f,16f,16f),muted:severity < i+1);
            }
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
                bool alreadyAtHome = gameManager.NavigationStack.Any(node => node.Id == "家");
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
                var slots = gameManager.GetSlotsForNode(nodes[i].Id);
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
            DrawnActionRects[node.Id] = rect;
            if (!string.IsNullOrEmpty(node.CarryItemId)
                && (node.Requires == null || node.Requires.Count != 2))
                throw new InvalidOperationException(
                    $"随身消费动作「{node.Id}」必须正好有物品和行动骰两个需求槽（见 engine.scm 的 随身动作）");

            var slots = gameManager.GetSlotsForNode(node.Id) ?? new List<SlottedResource?>();
            var execution = gameManager.GetExecutionState(node.Id);
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
            IMGUIStyles.DrawLabel(new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, 22f), node.Name, titleStyle);

            var requirements = node.Requires ?? new List<SSNoir.Core.ActionCost>();
            if (!string.IsNullOrEmpty(node.SupportId) && requirements.Count != 0)
                throw new InvalidOperationException(
                    $"关系支援「{node.Id}」不应有需求槽（见 engine.scm 的 support-frank），实际有 {requirements.Count} 个。");
            if (string.IsNullOrEmpty(node.SupportId) && string.IsNullOrEmpty(node.CarryItemId))
                throw new InvalidOperationException(
                    $"非场景交锋动作「{node.Id}」必须声明 :carry-item 或 :support。");
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
                if (interaction.DroppedSlotIndex != -1)
                    gameManager.TryPlaceSelectedResource(node, i);
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
