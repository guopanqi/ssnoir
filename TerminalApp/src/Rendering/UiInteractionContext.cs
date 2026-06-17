using System.Numerics;
using Raylib_cs;

namespace SSNoir.TerminalApp.Rendering
{
    public readonly struct UiInteractionContext
    {
        public Vector2 Mouse { get; init; }

        // Input locked by an animation or global block — shows disabled/grayed visual.
        public bool IsLocked { get; init; }

        // Input consumed by a higher UI layer (window stack) — no visual change, clicks just don't register.
        public bool IsSuppressed { get; init; }

        public bool CanHover(Rectangle rect)
        {
            return !IsLocked && !IsSuppressed && Raylib.CheckCollisionPointRec(Mouse, rect);
        }

        public bool WasClicked(Rectangle rect)
        {
            return CanHover(rect) && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }
    }
}
