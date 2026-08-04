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

        public Vector2 Mouse { get; }
        public bool IsLocked { get; }

        // 被上层卡遮挡：几何上鼠标确实在本控件里，但它不是最上面那张。
        // 与 IsLocked 的区别是这里只吞掉命中，不改变控件的「可用/禁用」外观。
        public bool IsOccluded { get; }

        public IMGUIInteractionContext(Vector2 mouse, bool isLocked, bool isOccluded = false)
        {
            Mouse = mouse;
            IsLocked = isLocked;
            IsOccluded = isOccluded;
        }

        public IMGUIInteractionContext Occluded()
        {
            return new IMGUIInteractionContext(Mouse, IsLocked, isOccluded: true);
        }

        public bool CanHover(Rect rect)
        {
            // Mark UI coverage on containment regardless of lock state, so the
            // camera is blocked even while a widget is temporarily non-interactive.
            // 被遮挡时同样要标记：上层卡挡住的地方依然是 UI，不该拖动镜头。
            bool over = rect.Contains(Mouse);
            if (over) _pointerOverUi = true;
            return !IsLocked && !IsOccluded && over;
        }

        // Containment test that does NOT mark pointer-over-UI. Use for large
        // passive regions (e.g. card-grid scroll areas) that should still let a
        // camera drag begin through their empty space.
        public bool ContainsMouse(Rect rect)
        {
            return !IsLocked && !IsOccluded && rect.Contains(Mouse);
        }

        public bool WasClicked(Rect rect)
        {
            return CanHover(rect)
                && Event.current.type == EventType.MouseDown
                && Event.current.button == 0;
        }
    }
}
