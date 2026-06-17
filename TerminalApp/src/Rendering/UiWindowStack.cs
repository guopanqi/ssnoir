using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace SSNoir.TerminalApp.Rendering
{
    public enum UiLayer
    {
        World = 0,
        Panel = 10,
        Modal = 20,
    }

    public enum UiBlockMode
    {
        None,
        Bounds,
        Fullscreen,
    }

    public enum UiWindowId
    {
        DebugMenu,
        GrowthPanel,
        TurnPanel,
        RollResultModal,
    }

    public readonly struct UiWindowBlocker
    {
        public UiWindowId Id { get; init; }
        public Rectangle Bounds { get; init; }
        public UiLayer Layer { get; init; }
        public UiBlockMode BlockMode { get; init; }
        public bool CloseOnClickedOutside { get; init; }
    }

    public class UiWindowStack
    {
        private readonly List<UiWindowBlocker> _blockers = new();
        private Vector2 _mouse;
        private bool _clicked;

        public void BeginFrame(Vector2 mouse, bool clicked)
        {
            _blockers.Clear();
            _mouse = mouse;
            _clicked = clicked;
        }

        public void Register(UiWindowBlocker blocker)
        {
            _blockers.Add(blocker);
        }

        // Returns ids of blockers whose CloseOnClickedOutside fired this frame.
        public HashSet<UiWindowId> Update()
        {
            var requests = new HashSet<UiWindowId>();
            if (!_clicked) return requests;

            foreach (var blocker in _blockers)
            {
                if (!blocker.CloseOnClickedOutside) continue;
                if (!Raylib.CheckCollisionPointRec(_mouse, blocker.Bounds))
                    requests.Add(blocker.Id);
            }
            return requests;
        }

        // Returns a context for a widget at callerLayer.
        // IsLocked = true if any registered blocker at a higher layer would block this mouse position.
        public UiInteractionContext MakeContext(UiLayer callerLayer)
        {
            foreach (var blocker in _blockers)
            {
                if ((int)blocker.Layer <= (int)callerLayer) continue;

                bool blocks = blocker.BlockMode switch
                {
                    UiBlockMode.Fullscreen => true,
                    UiBlockMode.Bounds => Raylib.CheckCollisionPointRec(_mouse, blocker.Bounds),
                    _ => false,
                };

                if (blocks)
                    return new UiInteractionContext { Mouse = _mouse, IsSuppressed = true };
            }

            return new UiInteractionContext { Mouse = _mouse };
        }
    }
}
