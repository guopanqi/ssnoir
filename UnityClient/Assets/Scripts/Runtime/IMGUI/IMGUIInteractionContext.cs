using UnityEngine;

namespace SSNoir.IMGUI
{
    public readonly struct IMGUIInteractionContext
    {
        // Per-OnGUI-pass accumulator: set true whenever the pointer is over any
        // interactive UI rect. Read by the camera manager so a drag that begins
        // over the UI does not also pan/orbit the camera. Reset at the start of
        // each OnGUI pass via ResetPointerOverUi().
        private static bool _pointerOverUi;
        public static bool PointerOverUi => _pointerOverUi;
        public static void ResetPointerOverUi() => _pointerOverUi = false;

        // 触控设备上没有「悬停」这回事：手指离开后 Input.mousePosition 停在最后一次触点上，
        // 于是最后碰过的那个控件会一直亮着，看起来像选中了却什么也没发生。
        //
        // 这里不去伪造坐标（把鼠标挪到屏幕外那类做法会连带毁掉 PointerOverUi 与相机拖拽的
        // 判断），而是把「此刻有没有悬停这个状态」做成显式的上下文：没有指针悬停时
        // CanHover 一律返回 false，控件老老实实画默认态。命中判定（WasClicked）不受影响，
        // 按下的那一刻手指当然在。
        private static bool _hoverAvailable = true;
        public static bool HoverAvailable => _hoverAvailable;

        /// <summary>每帧 OnGUI 开头设置一次。桌面恒为 true；触控设备只在手指按住时为 true。</summary>
        public static void SetHoverAvailable(bool available) => _hoverAvailable = available;

        // ── 按下 / 抬起：触控上「点一下」和「按下去」不是一回事 ─────────────
        //
        // 鼠标按下几乎总是意味着「我要按这个」；手指落下时则还不知道玩家是要点它，
        // 还是要从它身上开始滑一段。这两件事必须分开，否则任何一次滑动列表都会先把
        // 手指底下那张卡点掉。
        //
        // 所以：WasClicked = 按下（用于「按住就开始拖」的骰子/物品），
        //       WasTapped  = 一次完整的轻点（用于按钮、卡片、列表行）。
        private static Vector2 _pressOrigin;
        private static bool _pressTravelled;
        private static bool _pressActive;
        private static bool _pressConsumed;

        /// <summary>手指判定为「滑动而非点击」的位移阈值（虚拟像素）。</summary>
        public const float TapSlop = 10f;

        /// <summary>本次按压是否已经走成了滑动。滑起来之后不再产生轻点。</summary>
        public static bool PressTravelled => _pressTravelled;

        /// <summary>
        /// 每次 OnGUI 开头喂一次当前事件，维护按压起点与位移。
        /// 需要 MouseDrag 事件也进得来（OnGUI 顶部的事件过滤要放行它）。
        /// </summary>
        public static void NotePointerEvent(Event e)
        {
            if (e == null) return;
            if (e.rawType == EventType.MouseDown && e.button == 0)
            {
                _pressOrigin = e.mousePosition;
                _pressTravelled = false;
                _pressActive = true;
                _pressConsumed = false;
            }
            else if (e.rawType == EventType.MouseDrag)
            {
                if ((e.mousePosition - _pressOrigin).sqrMagnitude > TapSlop * TapSlop)
                    _pressTravelled = true;
            }
        }

        /// <summary>
        /// 声明当前这次按压已经归上层 UI / 全局锁所有。之后的 MouseUp 仍会送达 IMGUI，
        /// 但不能再被另一个 WasTapped 当成一记新点击。
        /// </summary>
        public static void ConsumeCurrentPress()
        {
            if (_pressActive)
                _pressConsumed = true;
        }

        /// <summary>
        /// 每个 OnGUI pass 的控件处理完后调用。被 Use 的 Down / Drag 自动认领整次按压；
        /// MouseUp 最后再清状态。必须看 rawType，因为 Use 会把 type 改成 Used。
        /// </summary>
        public static void FinishPointerEvent(Event e)
        {
            if (e == null || !_pressActive)
                return;

            if ((e.rawType == EventType.MouseDown || e.rawType == EventType.MouseDrag)
                && e.type == EventType.Used)
            {
                _pressConsumed = true;
            }

            if (e.rawType == EventType.MouseUp && e.button == 0)
                ResetPress();
        }

        private static void ResetPress()
        {
            _pressActive = false;
            _pressTravelled = false;
            _pressConsumed = false;
        }

        public Vector2 Mouse { get; }

        // 本次按压的起点，和 Mouse 处在同一个坐标空间。
        // 它必须跟着上下文走而不是直接读那个全局值：GUI.BeginGroup 里的控件用的是组内局部
        // 坐标，拿全局起点去和局部矩形比，永远不相交——网格里的卡就再也点不动了。
        public Vector2 PressOrigin { get; }

        public bool IsLocked { get; }

        // 被上层卡遮挡：几何上鼠标确实在本控件里，但它不是最上面那张。
        // 与 IsLocked 的区别是这里只吞掉命中，不改变控件的「可用/禁用」外观。
        public bool IsOccluded { get; }

        public IMGUIInteractionContext(Vector2 mouse, bool isLocked, bool isOccluded = false)
            : this(mouse, _pressOrigin, isLocked, isOccluded)
        {
        }

        private IMGUIInteractionContext(Vector2 mouse, Vector2 pressOrigin, bool isLocked, bool isOccluded)
        {
            Mouse = mouse;
            PressOrigin = pressOrigin;
            IsLocked = isLocked;
            IsOccluded = isOccluded;
        }

        public IMGUIInteractionContext Occluded()
        {
            return new IMGUIInteractionContext(Mouse, PressOrigin, IsLocked, isOccluded: true);
        }

        /// <summary>
        /// 换到 GUI.BeginGroup 的局部坐标系。指针位置和按压起点一起平移，两者必须同步。
        /// </summary>
        public IMGUIInteractionContext Translated(Vector2 offset)
        {
            return new IMGUIInteractionContext(Mouse - offset, PressOrigin - offset, IsLocked, IsOccluded);
        }

        // 命中：指针几何上落在控件里，且控件此刻可交互。
        // Mark UI coverage on containment regardless of lock state, so the
        // camera is blocked even while a widget is temporarily non-interactive.
        // 被遮挡时同样要标记：上层卡挡住的地方依然是 UI，不该拖动镜头。
        private bool Hit(Rect rect)
        {
            bool over = rect.Contains(Mouse);
            if (over) _pointerOverUi = true;
            return !IsLocked && !IsOccluded && over;
        }

        // 悬停：命中 + 当前设备此刻真的有「悬停」这个状态（触控设备＝手指按着）。
        public bool CanHover(Rect rect)
        {
            return Hit(rect) && _hoverAvailable;
        }

        // 几何命中：用于拖拽落点等在 MouseUp 当帧仍需成立的判断。
        // 触控抬手时已经没有“悬停”状态，但最后触点仍然是合法落点；把二者混用会导致
        // 槽位在拖动时亮起、松手时却立即判空并回滚。
        public bool IsPointerInside(Rect rect)
        {
            return Hit(rect);
        }

        // Containment test that does NOT mark pointer-over-UI. Use for large
        // passive regions (e.g. card-grid scroll areas) that should still let a
        // camera drag begin through their empty space.
        public bool ContainsMouse(Rect rect)
        {
            return !IsLocked && !IsOccluded && rect.Contains(Mouse);
        }

        // 按下：用于「按住即开始」的交互（拿起骰子/物品去拖）。
        // 不看悬停可用性——按下的那一刻手指当然在控件上。
        public bool WasClicked(Rect rect)
        {
            return Hit(rect)
                && Event.current.type == EventType.MouseDown
                && Event.current.button == 0;
        }

        // 轻点：按钮、卡片、列表行用这个。要求「落点在控件里 + 没滑走 + 在控件里抬起」，
        // 滑动列表才不会顺手点掉一张卡。鼠标和手指走同一条规则——按钮在抬起时响应本来
        // 就是两边共同的习惯，而两边同一套判定，Editor 里试出来的手感才等于真机的手感。
        public bool WasTapped(Rect rect)
        {
            return Hit(rect)
                && _pressActive
                && Event.current.type == EventType.MouseUp
                && Event.current.button == 0
                && !_pressTravelled
                && !_pressConsumed
                && rect.Contains(PressOrigin);
        }
    }
}
