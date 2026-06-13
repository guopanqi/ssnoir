using UnityEngine;

namespace SSNoir.UnityClient.IMGUI
{
    public readonly struct IMGUIInteractionContext
    {
        public Vector2 Mouse { get; init; }
        public bool IsLocked { get; init; }

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
