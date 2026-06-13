using UnityEngine;

namespace SSNoir.IMGUI
{
    public readonly struct IMGUIInteractionContext
    {
        public Vector2 Mouse { get; }
        public bool IsLocked { get; }

        public IMGUIInteractionContext(Vector2 mouse, bool isLocked)
        {
            Mouse = mouse;
            IsLocked = isLocked;
        }

        public bool CanHover(Rect rect)
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
