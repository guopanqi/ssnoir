#nullable enable
using System.Linq;
using System.Collections.Generic;
using SSNoir.Core;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>生命状态的图例浮层。悬停查看，点按固定；只读取当前显示快照。</summary>
    public static class VitalHintDrawer
    {
        private static readonly List<Rect> Sources = new();
        public static IMGUIInteractionContext Interaction { private get; set; }
        public static bool Suppressed { get; set; }
        public static void RegisterBlockers(IMGUIWindowStack stack)
        {
            if (Suppressed) { Close(); Sources.Clear(); return; }
            foreach (var source in Sources)
                stack.Register(new IMGUIWindowBlocker { Id=IMGUIWindowId.VitalHint, Bounds=source,
                    Layer=IMGUIWindowLayer.HudHint, BlockMode=IMGUIBlockMode.Bounds });
            if (IsVisible) stack.Register(new IMGUIWindowBlocker { Id=IMGUIWindowId.VitalHint, Bounds=Bounds,
                Layer=IMGUIWindowLayer.HudHint, BlockMode=IMGUIBlockMode.Bounds });
        }
        private static string? _actorId, _pinnedActor;
        private static Rect _source, _pinnedSource;
        private static float _hoverUntil;
        public static Rect Bounds { get; private set; }
        public static bool IsVisible => Bounds.width > 0f;

        public static void Close() { _actorId = _pinnedActor = null; Bounds = Rect.zero; }

        public static void Begin(IMGUIInteractionContext ui)
        {
            ui = Interaction;
            Sources.Clear();
            if (Suppressed) { Close(); return; }
            if (!IMGUIInteractionContext.HoverAvailable || (!Bounds.Contains(ui.Mouse) && Time.unscaledTime > _hoverUntil)) _actorId = null;
            if (ui.IsLocked || ui.IsOccluded) { Close(); return; }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                if (IsVisible) { Close(); Event.current.Use(); }
            }
            if (Event.current.type == EventType.MouseDown && !Bounds.Contains(ui.Mouse) && !_pinnedSource.Contains(ui.Mouse))
                _pinnedActor = null;
        }

        public static void Offer(Rect rect, ActorSnapshot actor, IMGUIInteractionContext ui)
        {
            if (Suppressed) return;
            ui = Interaction;
            Sources.Add(rect);
            if (ui.CanHover(rect)) { _actorId = actor.Id; _source = rect; _hoverUntil = Time.unscaledTime + .2f; }
            if (ui.WasTapped(rect))
            {
                bool same = _pinnedActor == actor.Id;
                _pinnedActor = same ? null : actor.Id;
                _pinnedSource = rect;
                // 点按再次关闭后，本帧也不由悬停立即重新打开。
                if (same) _actorId = null;
                Event.current.Use();
            }
        }

        public static void Draw(PresentationSnapshot snapshot, IMGUIInteractionContext ui)
        {
            if (Suppressed || ui.IsLocked || ui.IsOccluded) { Close(); return; }
            string? id = _pinnedActor ?? _actorId;
            var actor = snapshot.Actors.FirstOrDefault(a => a.Id == id && a.OnStage);
            if (actor == null) { Close(); return; }
            var player = snapshot.Actors.First(a => a.Role == "protagonist");
            Rect source = _pinnedActor != null ? _pinnedSource : _source;
            Rect safe = UIScale.SafeArea;
            float w = Mathf.Min(420f, safe.width-24f), inner = w-32f;
            int count = player.ActionSlotCount;
            int available = count-snapshot.InjuryActionDieLoss;
            string cadence = snapshot.IsInScenario ? "每回合" : "每天";
            string injuryText = $"伤势到 2、5 点时各封锁一个行动位，当前{cadence}可用 {available} 个。";
            string composureText = actor.Role == "protagonist"
                ? "冷静归零后，继续承受压力会转成身体伤势。"
                : "同伴的冷静耗尽后会离开。";
            float headingH = TextStyle(17,IMGUIStyles.Paper).lineHeight+4f;
            float injuryTextH = TextStyle(13,IMGUIStyles.Paper).CalcHeight(new GUIContent(injuryText),inner);
            float composureTextH = TextStyle(13,IMGUIStyles.Paper).CalcHeight(new GUIContent(composureText),inner);
            float slotSize = Mathf.Min(48f,(inner-(count-1)*10f)/Mathf.Max(1,count));
            float faceSize = Mathf.Min(46f,(inner-5*8f)/6f);
            const float meterH=10f;
            float h = 32f+2*(headingH+8f+meterH+14f+12f)+slotSize+faceSize+injuryTextH+composureTextH+32f;
            Bounds = new Rect(Mathf.Clamp(source.x,safe.x+8f,safe.xMax-w-8f),
                Mathf.Clamp(source.y-h-12f,safe.y+8f,Mathf.Max(safe.y+8f,safe.yMax-h-8f)),w,h);
            ui.IsPointerInside(Bounds);
            IMGUIStyles.DrawShadow(Bounds,new Vector2(2f,3f),.5f);
            IMGUIStyles.SetColor(new Color(.006f,.008f,.012f,.98f)); GUI.DrawTexture(Bounds,Texture2D.whiteTexture);
            IMGUIStyles.DrawOutline(Bounds,1f,IMGUIStyles.CityPlateLine); IMGUIStyles.ResetColor();
            float x=Bounds.x+16f,y=Bounds.y+16f;
            var close=new Rect(Bounds.xMax-38f,y-8f,26f,headingH);
            Label(close,"×",18,IMGUIStyles.TextSecondary);
            if(ui.WasTapped(close)) { Close(); Event.current.Use(); return; }
            Label(new Rect(x,y,inner-32f,headingH),"伤势",17,IMGUIStyles.Paper); y+=headingH+8f;
            DrawMeter(new Rect(x,y,inner,meterH),snapshot.InjurySeverity,snapshot.InjuryMaxSeverity,
                IMGUIStyles.OddsFail,IMGUIStyles.CityPlateLine);
            y+=meterH+14f;
            for(int i=0;i<count;i++)
            {
                var r=new Rect(x+i*(slotSize+10f),y,slotSize,slotSize);
                bool blocked=i>=available;
                HandPanelDrawer.DrawActionSlot(r,blocked);
                if(!blocked) HandPanelDrawer.DrawDieFace(HandPanelDrawer.DieInsideSlot(r),5,neutral:true);
            }
            y+=slotSize+12f;
            Label(new Rect(x,y,inner,injuryTextH),injuryText,13,IMGUIStyles.Paper); y+=injuryTextH+16f;
            IMGUIStyles.DrawLine(new Vector2(x,y),new Vector2(x+inner,y),IMGUIStyles.CityPlateLine,1f); y+=16f;
            Label(new Rect(x,y,inner,headingH),"冷静",17,IMGUIStyles.Paper); y+=headingH+8f;
            Color tone=IMGUIStyles.Paper;
            DrawMeter(new Rect(x,y,inner,meterH),actor.Composure,actor.MaxComposure,tone,
                new Color(tone.r,tone.g,tone.b,.25f),depleted:actor.Composure==0); y+=meterH+14f;
            for(int value=1;value<=6;value++)
            {
                var r=new Rect(x+(value-1)*(faceSize+8f),y,faceSize,faceSize);
                Color ink=IMGUIStyles.Paper;
                IMGUIStyles.DrawOutline(r,1f,ink);
                Label(r,value.ToString(),18,ink,TextAnchor.MiddleCenter);
            }
            y+=faceSize+12f;
            Label(new Rect(x,y,inner,composureTextH),composureText,13,IMGUIStyles.Paper);
        }

        private static void DrawMeter(Rect rect,int current,int maximum,Color color,Color emptyOutline,bool depleted=false)
        {
            const float numberW=48f,gap=6f;
            float width=rect.width-numberW-10f;
            float cell=(width-gap*(maximum-1))/maximum;
            if(depleted)
            {
                var red=IMGUIStyles.OddsFail; red.a=.6f;
                IMGUIStyles.SetColor(red);
                GUI.DrawTexture(new Rect(rect.x,rect.y,width,rect.height),Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
            }
            for(int i=0;i<maximum;i++)
            {
                var r=new Rect(rect.x+i*(cell+gap),rect.y,cell,rect.height);
                if(i<current) { IMGUIStyles.SetColor(color); GUI.DrawTexture(r,Texture2D.whiteTexture); IMGUIStyles.ResetColor(); }
                else IMGUIStyles.DrawOutline(r,1f,emptyOutline);
            }
            Label(new Rect(rect.xMax-numberW,rect.y-8f,numberW,rect.height+16f),$"{current}/{maximum}",13,IMGUIStyles.Paper,TextAnchor.MiddleRight);
        }

        private static void Label(Rect rect,string text,int size,Color color,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            IMGUIStyles.DrawLabel(rect,text,TextStyle(size,color,alignment));
        }

        private static GUIStyle TextStyle(int size, Color color, TextAnchor alignment=TextAnchor.UpperLeft)
            => new GUIStyle(IMGUIStyles.CardSubtitle) { fontSize=IMGUIStyles.FontSize(size),
                alignment=alignment,wordWrap=true,normal={textColor=color} };
    }
}
