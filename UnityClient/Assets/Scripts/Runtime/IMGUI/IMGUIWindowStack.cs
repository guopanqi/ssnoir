#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    public enum IMGUIWindowLayer
    {
        World = 0,
        Panel = 10,
        Modal = 20,
    }

    public enum IMGUIBlockMode
    {
        None,
        Bounds,
        Fullscreen,
    }

    public enum IMGUIWindowId
    {
        DebugPanel,
        GrowthPanel,
        HeavyOutcome,
        Spotlight,
        Conversation,
    }

    public struct IMGUIWindowBlocker
    {
        public IMGUIWindowId Id { get; set; }
        public Rect Bounds { get; set; }
        public IMGUIWindowLayer Layer { get; set; }
        public IMGUIBlockMode BlockMode { get; set; }
        public bool CloseOnClickedOutside { get; set; }
    }

    public sealed class IMGUIWindowStack
    {
        private readonly List<IMGUIWindowBlocker> _blockers = new();
        private Vector2 _mouse;
        private bool _clicked;

        public void BeginFrame(Vector2 mouse, bool clicked)
        {
            _blockers.Clear();
            _mouse = mouse;
            _clicked = clicked;
        }

        public void Register(IMGUIWindowBlocker blocker)
        {
            _blockers.Add(blocker);
        }

        public HashSet<IMGUIWindowId> Update()
        {
            var requests = new HashSet<IMGUIWindowId>();
            if (!_clicked)
            {
                return requests;
            }

            foreach (var blocker in _blockers)
            {
                if (!blocker.CloseOnClickedOutside)
                {
                    continue;
                }

                if (!blocker.Bounds.Contains(_mouse))
                {
                    requests.Add(blocker.Id);
                }
            }

            return requests;
        }

        // True when the pointer sits over any registered blocker region (open
        // panel or fullscreen modal). Used to keep camera drag from starting
        // under UI whose inner content does not route through CanHover.
        public bool IsPointerOverBlocker()
        {
            foreach (var blocker in _blockers)
            {
                bool over = blocker.BlockMode switch
                {
                    IMGUIBlockMode.Fullscreen => true,
                    IMGUIBlockMode.Bounds => blocker.Bounds.Contains(_mouse),
                    _ => false,
                };
                if (over)
                {
                    return true;
                }
            }
            return false;
        }

        public IMGUIInteractionContext MakeContext(IMGUIWindowLayer callerLayer, bool forceLocked = false)
        {
            if (forceLocked)
            {
                return new IMGUIInteractionContext(_mouse, true);
            }

            foreach (var blocker in _blockers)
            {
                if ((int)blocker.Layer <= (int)callerLayer)
                {
                    continue;
                }

                bool blocks = blocker.BlockMode switch
                {
                    IMGUIBlockMode.Fullscreen => true,
                    IMGUIBlockMode.Bounds => blocker.Bounds.Contains(_mouse),
                    _ => false,
                };

                if (blocks)
                {
                    return new IMGUIInteractionContext(_mouse, true);
                }
            }

            return new IMGUIInteractionContext(_mouse, false);
        }
    }
}
