using System.Numerics;
using Raylib_cs;

namespace SSNoir.TerminalApp.Rendering
{
    public readonly struct UiInteractionContext
    {
        public Vector2 Mouse { get; init; }
        public bool IsLocked { get; init; }

        public bool CanHover(Rectangle rect)
        {
            return !IsLocked && Raylib.CheckCollisionPointRec(Mouse, rect);
        }

        public bool WasClicked(Rectangle rect)
        {
            return CanHover(rect) && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }
    }
}
