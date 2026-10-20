#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 节点卡入口与共享框架。
    //
    // DESIGN.md「节点规范（暗版，场景内）」通用规则：
    //   Ink 填充 + 1px 纸白描边 @ 70–75%（与建筑线同宽，偏纸白暖调）
    //   内侧 3px 加一根 20% 细线（双线，始终是「线」的语言，不是「框」）
    //   直角无圆角；硬投影偏移 (5,6) 纯黑 @ 45–50%，无模糊
    //
    // 交互状态（DESIGN.md）：默认白线 70% / 悬停白线 100%（变亮不变色）/ 选中金描边 /
    // 正在发生金光呼吸 / 禁用降到 35%。
    //
    // 本类只画共享框架与时钟徽章；动作 / 判定卡内容委托 ActionNodeDrawer，
    // 普通 / 地点 / 人物节点内容委托 ContainerNodeDrawer。
    public static class CardDrawer
    {
        // 场景内每张节点卡都需要稳定的点击目标；内容再少也不能退化成难以点中的细条。
        // 需求展签已移除，短动作卡不再需要旧的 150 高空白；仍保留足够的触控目标与
        // 标题/骰位/执行键间距。长副标题的卡高继续完全由内容测量决定。
        public const float MinCardHeight = 132f;

        public struct CardInteraction
        {
            public bool CardClicked;
            public int ClickedSlotIndex;
            public int DroppedSlotIndex;
            public bool ExecuteClicked;
        }

        // ── 入口 ───────────────────────────────────────────────────────

        // 卡片的视觉类别。卡分两族，两族都是「可操作」的东西：
        //   有框（Action / Character）——框起来的「器物」，你在上面操作。
        //   无边悬浮（Location / Common）——漂在图纸上的「一个地方 / 一件事」，你点进去。
        //
        // 第三族「标注」不在这里：它没有底、没有影子、没有框，也不可点，
        // 整个由 AnnotationDrawer 负责——Ink 填充加硬投影正是「可以拿起来」的标志，
        // 所以标注必须连这层框架一起绕开，而不是在这里多一个 CardKind。
        public enum CardKind { Action, Location, Character, Common }

        // 数据驱动分类。内容里并没有「地点 / 人物」标签，真正的「这是个地方」信号是它
        // 锚定在场景 3D 建筑上（projected）；网格里的容器则是地点内部的事件/分组（Common）。
        // 标签留作显式覆盖，未来内容想强制某类时可用。
        //
        // 标注（ResolveType.Note）不在这里分类——它根本不是卡，由 AnnotationDrawer 独立
        // 绘制，调用方必须先用 AnnotationDrawer.IsAnnotation 把它分流出去。
        public static CardKind Classify(GameNode node, bool anchored)
        {
            if (node.HasResolve) return CardKind.Action;
            if (node.Tags.Contains("人物")) return CardKind.Character;
            if (anchored || node.Tags.Contains("地点")) return CardKind.Location;
            return CardKind.Common;
        }

        // 这张卡按自身内容该有多高。所有摆卡的地方（网格瀑布流、世界投射）都必须问它，
        // 不要再各自写「网格一律 190 / 地点 168」这类与内容无关的常量——那正是标题被时钟徽章
        // 压住、副标题被截掉、按钮贴着骰位的根因：内容变了，盒子不跟着变。
        // 高度只由内容决定，不随装填 / 执行 / 结算状态变化：那些都是卡片下挂的附件。
        public static float MeasureCardHeight(
            GameNode node, CardKind kind, float cardWidth, IReadOnlyList<ActorSnapshot>? actors)
        {
            if (kind == CardKind.Action)
                return ActionNodeDrawer.RecommendedCardHeight(node, cardWidth, actors);

            float clocksHeight = MeasureClockBadgesHeight(cardWidth, node.Clocks);
            return kind switch
            {
                CardKind.Character => ContainerNodeDrawer.MeasureCharacterHeight(node, cardWidth, clocksHeight),
                CardKind.Location => ContainerNodeDrawer.MeasureLocationHeight(node, cardWidth, clocksHeight),
                _ => ContainerNodeDrawer.MeasureCommonHeight(node, cardWidth, clocksHeight),
            };
        }

        public static CardInteraction DrawCard(
            Rect rect, GameNode node, CardKind kind, bool isHovered, bool isFlipped, bool isFocused,
            List<SlottedResource?>? slotted, List<GameClock> clocks, string backText,
            IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            bool isExecuting = false, float executeProgress = 0f, string executingText = "执行中",
            bool isHappening = false,
            bool isRestBlockerTarget = false,
            bool containsRestBlockerTarget = false)
        {
            var interaction = new CardInteraction { CardClicked = false, ClickedSlotIndex = -1, DroppedSlotIndex = -1, ExecuteClicked = false };
            bool disabled = node.Disabled;

            // 翻转卡（观察线索背面）：正交状态，独立绘制。
            if (isFlipped)
            {
                DrawFlippedCard(rect, node, backText, isHovered && !disabled, ui, ref interaction);
                if (disabled) interaction.CardClicked = false;
                return interaction;
            }

            // 时钟徽章可能换行——先量出它实际占到哪，卡内其余内容（标题/头像/能力栏）
            // 才知道该从哪开始画，不会被压在徽章下面。
            float clocksBottomY = MeasureClockBadgesBottom(rect, clocks);

            // ── 无边悬浮族（地点 / 普通）：Ink + 硬投影，不走框架，整卡可点。
            if (kind == CardKind.Location || kind == CardKind.Common)
            {
                ContainerNodeDrawer.DrawFloating(rect, node, kind == CardKind.Location, isHovered && !disabled, disabled, clocksBottomY, isFocused);
                ClockPulse.BeginHost(node.Id);
                DrawClockBadges(rect, clocks);
                ClockPulse.EndHost();
                ActionNodeDrawer.DrawTopTabs(rect, node, disabled, containsRestBlockerTarget);
                if (isRestBlockerTarget)
                    IMGUIStyles.DrawGoldPulse(rect, baseAlpha: 0.72f, rings: 3, ringStep: 2.5f, speed: 2.2f);
                if (!disabled && ui.WasTapped(rect))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
                return interaction;
            }

            // ── 有框族（动作 / 人物相册页）：共享卡框架 + 内容。
            bool isCharacter = kind == CardKind.Character;
            Matrix4x4 oldMatrix = GUI.matrix;
            if (isCharacter && isFocused)
            {
                float rotationDeg = (node.Id.GetHashCode() % 2 == 0) ? -2f : 2f;
                GUIUtility.RotateAroundPivot(rotationDeg, rect.center);
            }

            // 「正在发生」的金光：执行中，或判定动画正挂在这张卡下面（附件由渲染器另画）。
            DrawCardFrame(rect, isHovered, isFocused, disabled, isExecuting || isHappening);
            ClockPulse.BeginHost(node.Id);
            DrawClockBadges(rect, clocks);
            ClockPulse.EndHost();

            if (isCharacter)
            {
                ContainerNodeDrawer.DrawCharacter(rect, node, disabled, clocksBottomY);
                if (!disabled && ui.WasTapped(rect))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
            }
            else
            {
                ActionNodeDrawer.DrawContent(
                    rect, node, slotted, ui, gameManager,
                    isExecuting, executeProgress, executingText,
                    clocksBottomY, containsRestBlockerTarget, ref interaction);
            }

            // 重要性是卡片最上层的状态标记，必须在动作内容与结果附件之后绘制。
            // 动作卡的「必须处理」已并进顶部书签（ActionNodeDrawer.DrawTopTabs），这里只剩金光。
            if (isCharacter)
                ActionNodeDrawer.DrawTopTabs(rect, node, disabled, containsRestBlockerTarget);
            if (isRestBlockerTarget)
                IMGUIStyles.DrawGoldPulse(rect, baseAlpha: 0.72f, rings: 3, ringStep: 2.5f, speed: 2.2f);

            if (isCharacter && isFocused)
                GUI.matrix = oldMatrix;

            return interaction;
        }

        // ── 共享卡框架 ─────────────────────────────────────────────────

        // 动作与人物卡沿用城市牌子的底与细边，选中和执行仍用金色强调。
        private static void DrawCardFrame(Rect rect, bool isHovered, bool isFocused, bool disabled, bool isHappening)
        {
            ContainerNodeDrawer.DrawPlateBase(rect, isHovered && !disabled);

            if (disabled)
            {
                // 禁用态不能只靠按钮文字区分。Ink 本身已接近纯黑，再叠黑几乎看不出变化——
                // 换成暖灰洗色（DisabledWash）整张覆盖：卡面会明显发灰发亮，读成"褪色的纸"
                // 而不是"更暗的黑"。侧栏与描边同用这支灰，三处统一成一套禁用态身份。
                IMGUIStyles.SetColor(new Color(IMGUIStyles.DisabledWash.r, IMGUIStyles.DisabledWash.g, IMGUIStyles.DisabledWash.b, 0.30f));
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
                IMGUIStyles.DrawOutline(rect, 1f,
                    new Color(IMGUIStyles.DisabledWash.r, IMGUIStyles.DisabledWash.g, IMGUIStyles.DisabledWash.b, 0.75f));
                IMGUIStyles.SetColor(IMGUIStyles.DisabledWash);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
            }

            if (isFocused && !disabled)
                IMGUIStyles.DrawOutline(rect, 2f, IMGUIStyles.Gold);

            if (isHappening && !disabled)
                IMGUIStyles.DrawGoldPulse(rect);
        }

        // Clock 图形优先：Gauge 顶部粗横条，Countdown 右侧大圆环。
        // 颜色是展示参数，默认金色；压力横条可由调用方传入 OddsFail，不改变进度规则。
        public const float ClockBadgeHeightCompact = 64f;
        private const float ClockGap = 8f;

        public static float MeasureClockBadgesBottom(Rect rect,List<GameClock>? clocks)
            => LayoutClockBadges(rect,clocks,false);
        public static float MeasureClockBadgesHeight(float cardWidth,List<GameClock>? clocks)
            => LayoutClockBadges(new Rect(0,0,cardWidth,0),clocks,false);
        private static void DrawClockBadges(Rect rect,List<GameClock>? clocks)
            => LayoutClockBadges(rect,clocks,true);

        private static float ClockHeight(GameClock clock,bool compact)
            => clock.Priority==ClockPriority.Secondary ? 28f
                : clock.Style==ClockStyle.Countdown ? (compact?64f:92f)
                : (clock.Style==ClockStyle.Gauge || clock.Style==ClockStyle.Pressure) ? (compact?64f:70f) : 32f;

        private static float LayoutClockBadges(Rect rect,List<GameClock>? clocks,bool draw)
        {
            if(clocks==null || clocks.Count==0) return rect.y;
            float width=Mathf.Max(1f,rect.width-24f),y=rect.y+8f;
            int i=0;
            while(i<clocks.Count)
            {
                int count=0;float total=0f,height=0f;
                while(i+count<clocks.Count)
                {
                    var clock=clocks[i+count];
                    float w=Mathf.Min(width,MeasureClockBadge(clock,false));
                    if(count>0 && total+ClockGap+w>width) break;
                    total+=(count>0?ClockGap:0f)+w;
                    height=Mathf.Max(height,ClockHeight(clock,false));count++;
                }
                float x=rect.x+12f;
                for(int j=0;j<count;j++)
                {
                    var clock=clocks[i+j];float w=Mathf.Min(width,MeasureClockBadge(clock,false));
                    if(draw) DrawClockBadge(new Rect(x,y,w,ClockHeight(clock,false)),clock,false);
                    x+=w+ClockGap;
                }
                y+=height+ClockGap;i+=count;
            }
            return y-ClockGap;
        }

        public static float MeasureClockBadge(GameClock clock,bool compact=true)
        {
            var style=new GUIStyle(IMGUIStyles.ClockLabel){fontSize=IMGUIStyles.FontSize(compact?12:14)};
            float text=style.CalcSize(new GUIContent(clock.ShownLabel)).x;
            if(clock.Priority==ClockPriority.Secondary)
                return style.CalcSize(new GUIContent($"{clock.ShownLabel} {clock.Current}/{clock.Max}")).x+20f;
            return clock.Style==ClockStyle.Countdown ? Mathf.Max(compact?174f:218f,text+(compact?76f:104f))
                : Mathf.Max(compact?166f:210f,text+74f);
        }

        public static void DrawClockBadge(Rect rect,GameClock clock,bool compact=true,Color? accent=null)
        {
            ClockPulse.Note(clock);
            Color color=accent??ClockColor(clock);
            float glow=ClockPulse.Glow(clock);
            IMGUIStyles.SetColor(IMGUIStyles.CityPlateBg);GUI.DrawTexture(rect,Texture2D.whiteTexture);IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(rect,1f,Color.Lerp(IMGUIStyles.CityPlateLine,color,glow));
            var label=new GUIStyle(IMGUIStyles.ClockLabel){fontSize=IMGUIStyles.FontSize(compact?12:14),
                alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip,normal={textColor=IMGUIStyles.Paper}};
            if(clock.Priority==ClockPriority.Secondary)
            {
                label.normal.textColor=clock.Style==ClockStyle.Pressure?color:IMGUIStyles.TextSecondary;
                IMGUIStyles.DrawLabel(new Rect(rect.x+8f,rect.y,rect.width-16f,rect.height),
                    $"{clock.ShownLabel} {clock.Current}/{clock.Max}",label);
            }
            else if(clock.Style==ClockStyle.Countdown)
            {
                float d=Mathf.Min(compact?56f:80f,Mathf.Min(rect.height-8f,rect.width*.45f));
                var graphic=new Rect(rect.xMax-d-6f,rect.center.y-d*.5f,d,d);
                IMGUIStyles.DrawLabel(new Rect(rect.x+10f,rect.y+8f,graphic.x-rect.x-18f,rect.height-16f),clock.ShownLabel,label);
                DrawClockGraphic(graphic,clock,color);
            }
            else if((clock.Style==ClockStyle.Gauge || clock.Style==ClockStyle.Pressure))
            {
                var graphic=new Rect(rect.x+9f,rect.y+8f,rect.width-18f,compact?19f:24f);
                DrawClockGraphic(graphic,clock,color);
                float labelY=graphic.yMax+7f;
                IMGUIStyles.DrawLabel(new Rect(rect.x+10f,labelY,rect.width-74f,rect.yMax-labelY-6f),clock.ShownLabel,label);
                label.alignment=TextAnchor.MiddleRight;label.normal.textColor=IMGUIStyles.TextSecondary;
                IMGUIStyles.DrawLabel(new Rect(rect.xMax-64f,labelY,54f,rect.yMax-labelY-6f),$"{clock.Current} / {clock.Max}",label);
            }
            else
            {
                IMGUIStyles.DrawLabel(new Rect(rect.x+10f,rect.y,rect.width-82f,rect.height),clock.ShownLabel,label);
                label.alignment=TextAnchor.MiddleRight;label.normal.textColor=color;
                IMGUIStyles.DrawLabel(new Rect(rect.xMax-72f,rect.y,62f,rect.height),$"{clock.Current}/{clock.Max}",label);
            }
        }

        // 压力统一采用警示红，目标进度采用金色。
        public static Color ClockColor(GameClock clock)
            => clock.Style==ClockStyle.Pressure?IMGUIStyles.OddsFail:IMGUIStyles.Gold;

        /// <summary>共享进度图形；空轨道保持低对比，实际进度按钟的语义着色。</summary>
        public static void DrawClockGraphic(Rect rect,GameClock clock,Color? accent=null)
        {
            ClockPulse.Note(clock);
            Color color=accent??ClockColor(clock);
            Color active=Color.Lerp(color,Color.white,ClockPulse.Glow(clock)*.2f);
            Color track=new Color(IMGUIStyles.Paper.r,IMGUIStyles.Paper.g,IMGUIStyles.Paper.b,.13f);
            if(clock.Style==ClockStyle.Countdown)
            {
                ShapeDrawer.DrawDisc(rect,IMGUIStyles.CityPlateBg);
                if(ClockPulse.TryGetFadingLoss(clock,out int previous,out float ghost))
                    ShapeDrawer.DrawDial(rect,previous,clock.Max,new Color(1,1,1,ghost),track);
                ShapeDrawer.DrawDial(rect,clock.Current,clock.Max,active,ghost>0f?Color.clear:track);
                var style=new GUIStyle(IMGUIStyles.ClockValue){fontSize=IMGUIStyles.FontSize(rect.width>=70f?18:14),
                    alignment=TextAnchor.MiddleCenter,normal={textColor=IMGUIStyles.Paper}};
                IMGUIStyles.DrawLabel(rect,$"{clock.Current}/{clock.Max}",style);
                return;
            }
            IMGUIStyles.SetColor(IMGUIStyles.CityPlateBg);GUI.DrawTexture(rect,Texture2D.whiteTexture);IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(rect,1f,IMGUIStyles.FunctionSlotLine);
            const float inset=3f;
            float gap=Mathf.Min(3f,(rect.width-inset*2f)/Mathf.Max(1,clock.Max)*.2f);
            float w=(rect.width-inset*2f-gap*Mathf.Max(0,clock.Max-1))/Mathf.Max(1,clock.Max);
            for(int i=0;i<clock.Max;i++)
                ClockPulse.DrawCell(new Rect(rect.x+inset+i*(w+gap),rect.y+inset,w,rect.height-inset*2f),
                    i<clock.Current,active,track,ClockPulse.CellPulse(clock,i),null);
        }

        private static void DrawFlippedCard(Rect rect, GameNode node, string backText, bool isHovered, IMGUIInteractionContext ui,
            ref CardInteraction interaction)
        {
            Color outline = isHovered
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);
            float thickness = isHovered ? 2f : 1f;

            IMGUIStyles.DrawShadow(rect, new Vector2(4f, 4f), 0.45f);
            IMGUIStyles.SetColor(IMGUIStyles.Ink);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(rect, thickness, outline);

            IMGUIStyles.DrawLabel(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20), node.Name, IMGUIStyles.FlippedTitle);
            IMGUIStyles.DrawLabel(new Rect(rect.x + 8, rect.y + 28, rect.width - 16, 16), "— 已解读线索 —", IMGUIStyles.FlippedTip);

            var contentRect = new Rect(rect.x + 8, rect.y + 48, rect.width - 16, rect.height - 68);
            string clueText = node.Resolve != null ? node.Resolve.ObserveText : "";
            IMGUIStyles.DrawLabel(contentRect, clueText, IMGUIStyles.FlippedContent);

            IMGUIStyles.DrawLabel(new Rect(rect.x + 8, rect.y + rect.height - 18, rect.width - 16, 14), UiText.Get("点击返回"), IMGUIStyles.FlippedTip);

            if (ui.WasTapped(rect))
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

        public static void DrawEffectRows(
            Rect area,
            IReadOnlyList<ActionEffectRecord> effects,
            float rowHeight = 14f,
            float rowGap = 16f,
            int fontSize = 10)
        {
            if (effects.Count == 0 || area.height <= 0f)
                return;

            float usedHeight = 0f;
            int visibleCount = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                float gap = visibleCount > 0 ? rowGap : 0f;
                float height = MeasureEffectRowHeight(effects[i], area.width, rowHeight, fontSize);
                if (usedHeight + gap + height > area.height + 0.01f)
                    break;

                usedHeight += gap + height;
                visibleCount++;
            }

            if (visibleCount == 0)
            {
                // 连一整行都放不下时也不能直接 return：影响行是结算的核心信息，什么都不画等于
                // 告诉玩家「这次行动什么都没发生」。挤出一条「+N」提示，把「有内容、但这里没
                // 地方显示」明说出来（AGENTS.md：信息不能被悄悄藏起来）。
                float moreHeight = Mathf.Min(rowHeight, area.height);
                if (moreHeight >= 8f)
                    DrawEffectMoreRow(new Rect(area.x, area.y, area.width, moreHeight), effects.Count, fontSize);
                return;
            }

            float y = area.y;
            for (int i = 0; i < visibleCount; i++)
            {
                if (i > 0)
                    y += rowGap;
                float height = MeasureEffectRowHeight(effects[i], area.width, rowHeight, fontSize);
                DrawSingleEffectRow(effects[i], new Rect(area.x, y, area.width, height), fontSize);
                y += height;
            }

            if (visibleCount < effects.Count)
            {
                float moreY = y + rowGap;
                float moreHeight = Mathf.Min(rowHeight, area.yMax - moreY);
                if (moreHeight < 8f)
                {
                    moreY = y;
                    moreHeight = Mathf.Min(rowHeight, area.yMax - moreY);
                }
                if (moreHeight >= 8f)
                    DrawEffectMoreRow(
                        new Rect(area.x, moreY, area.width, moreHeight),
                        effects.Count - visibleCount,
                        fontSize);
            }
        }

        public static float MeasureEffectRowsHeight(
            IReadOnlyList<ActionEffectRecord> effects,
            float areaWidth,
            float rowHeight = 14f,
            float rowGap = 16f,
            int fontSize = 10)
        {
            if (effects.Count == 0 || areaWidth <= 0f)
                return 0f;

            float total = 0f;
            for (int i = 0; i < effects.Count; i++)
            {
                if (i > 0)
                    total += rowGap;
                total += MeasureEffectRowHeight(effects[i], areaWidth, rowHeight, fontSize);
            }
            return total;
        }

        private static float MeasureEffectRowHeight(
            ActionEffectRecord effect, float areaWidth, float rowHeight, int fontSize)
        {
            float textWidth=areaWidth-16f;
            string text=EffectLine(effect);
            var style = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(fontSize),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
            };
            float wrappedHeight = style.CalcHeight(
                new GUIContent(text), Mathf.Max(1f, textWidth));
            return Mathf.Max(rowHeight, wrappedHeight + 2f);
        }

        // 「还有 N 项影响」折叠提示。行高不固定——高度紧张时它会被压到不足一行，
        // 但只要还有 ≥8px 就必须画出来，这是「信息不被悄悄藏起来」的最后一道兜底。
        private static void DrawEffectMoreRow(Rect row, int hiddenCount, int fontSize)
        {
            Color accent = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.50f);
            DrawEffectRowBg(row, accent);

            var style = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(fontSize),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            IMGUIStyles.DrawLabel(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), UiText.MoreEffects(hiddenCount), style);
        }

        private static void DrawSingleEffectRow(ActionEffectRecord effect, Rect row, int fontSize)
        {
            Color accent=effect.Tone switch
            {
                ActionEffectTone.Positive=>IMGUIStyles.Gold,
                ActionEffectTone.Negative=>IMGUIStyles.OddsFail,
                _=>IMGUIStyles.TextSecondary
            };
            DrawEffectRowBg(row,accent);
            var style=new GUIStyle(IMGUIStyles.ModalBody){fontSize=IMGUIStyles.FontSize(fontSize),alignment=TextAnchor.MiddleLeft,
                wordWrap=true,normal={textColor=accent}};
            string text=EffectLine(effect);
            IMGUIStyles.DrawLabel(new Rect(row.x+8f,row.y,row.width-16f,row.height),text,style);
        }

        private static string EffectLine(ActionEffectRecord effect)
        {
            if(effect.Kind==ActionEffectKind.Supplement) return effect.Text;
            string delta=effect.Delta.HasValue ? (effect.Delta.Value>0?"+":"")+effect.Delta.Value : "";
            return string.IsNullOrEmpty(delta)?effect.DisplayLabel:delta+"  "+effect.DisplayLabel;
        }

        private static void DrawEffectRowBg(Rect row, Color accent)
        {
            IMGUIStyles.SetColor(IMGUIStyles.CityPlateBg);
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(row,1f,new Color(accent.r,accent.g,accent.b,.8f));
        }

    }
}
