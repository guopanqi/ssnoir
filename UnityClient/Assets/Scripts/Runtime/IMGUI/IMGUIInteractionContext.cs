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

        public IMGUIInteractionContext(Vector2 mouse, bool isLocked)
        {
            Mouse = mouse;
            IsLocked = isLocked;
        }

        public bool CanHover(Rect rect)
        {
            // Mark UI coverage on containment regardless of lock state, so the
            // camera is blocked even while a widget is temporarily non-interactive.
            bool over = rect.Contains(Mouse);
            if (over) _pointerOverUi = true;
            return !IsLocked && over;
        }

        // Containment test that does NOT mark pointer-over-UI. Use for large
        // passive regions (e.g. card-grid scroll areas) that should still let a
        // camera drag begin through their empty space.
        public bool ContainsMouse(Rect rect)
        {
            return !IsLocked && rect.Contains(Mouse);
        }

        public bool WasClicked(Rect rect)
        {
            return CanHover(rect)
                && Event.current.type == EventType.MouseDown
                && Event.current.button == 0;
        }
    }
}
