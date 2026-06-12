#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public class IMGUIWorldRenderer : MonoBehaviour
    {
        private SSNoirGameManager _gameManager = null!;
        private IMGUIAnimationPlayer _animator = null!;

        private float _notificationTimer = 0f;
        private string _notification = "";

        private readonly Dictionary<string, Vector2> _cardCenters = new Dictionary<string, Vector2>();

        public void Initialize(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
            _animator = gameObject.AddComponent<IMGUIAnimationPlayer>();
        }

        public void ShowNotification(string message)
        {
            _notification = message;
            _notificationTimer = 3.0f;
        }

        public void StartRollAnimation(ActionReport report, string actionName)
        {
            _animator.StartRoll(report, actionName);
        }

        public bool IsAnimationPlaying => _animator.IsPlaying;
        public bool IsAnimationReadyToAcknowledge => _animator != null && _animator.IsReadyToAcknowledge();
        public void AcknowledgeAnimation() => _animator?.Acknowledge();
        public bool IsInputLocked => _inputLocked;

        private bool _inputLocked = false;
        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
        }

        private void Update()
        {
            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.deltaTime;
            }

            _animator.Update();

            // Right-click to cancel selection
            if (Input.GetMouseButtonDown(1) && !_animator.IsPlaying)
            {
                if (_gameManager.SelectedResource != null)
                {
                    _gameManager.ClearSelectedResource();
                }
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown
                && Event.current.type != EventType.MouseUp && Event.current.type != EventType.Layout)
                return;

            // Initialize styles if needed
            IMGUIStyles.Init(_gameManager.ChineseFont);

            // Global input blocker during locked state (but NOT during animation, so user can click the modal)
            if (_inputLocked && !_animator.IsPlaying)
            {
                // Draw invisible blocker using GUI.Box (does NOT consume events)
                GUI.color = Color.clear;
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
                GUI.color = Color.white;
            }

            var mousePos = Event.current.mousePosition;

            // ── Navigation Bar ──
            NavigationDrawer.Draw(_gameManager, mousePos);

            // ── Node Clocks ──
            var clocks = GetCurrentClocks();
            if (clocks.Count > 0)
            {
                ClockDrawer.DrawClocksBar(clocks, 100f);
            }

            // ── Node Cards (3D projected) ──
            DrawCards(mousePos);

            // ── Bottom Panel ──
            HandPanelDrawer.Draw(_gameManager, mousePos);

            // ── Scene Dropdown ──
            SceneDropdownDrawer.Draw(_gameManager, mousePos);

            // ── Overlays ──
            OverlayDrawer.DrawToast(_notification, _notificationTimer);
            OverlayDrawer.DrawCursorFollower(_gameManager);
            OverlayDrawer.DrawRollResult(_gameManager, mousePos);

            // ── Animation Modal ──
            _animator.DrawModal();
        }

        private void DrawCards(Vector2 mousePos)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var nodes = _gameManager.VisibleNodes;
            var focusedName = _gameManager.FocusedNodeName;

            // Split nodes into two groups: those with world anchors and those without
            var initialProjected = new List<(GameNode node, Vector3 screenPos, float distance)>();
            var gridNodes = new List<GameNode>();

            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(focusedName) && node.Name != focusedName)
                    continue;

                var anchor = _gameManager.SceneDirectory?.GetAnchor(node.Name);
                if (anchor != null)
                {
                    // Check if the anchor point is inside the camera's viewport frustum
                    // We add a small padding (e.g. 0.05) so the card doesn't pop out abruptly when its anchor crosses the screen edge.
                    var viewPos = cam.WorldToViewportPoint(anchor.transform.position);
                    float padding = 0.05f;
                    bool inCameraSight = viewPos.z >= 0 
                                      && viewPos.x >= -padding && viewPos.x <= (1f + padding)
                                      && viewPos.y >= -padding && viewPos.y <= (1f + padding);

                    if (inCameraSight)
                    {
                        var screenPos = cam.WorldToScreenPoint(anchor.transform.position);
                        initialProjected.Add((node, screenPos, screenPos.z));
                    }
                    // Nodes with anchors panned out of view are not drawn (neither projected nor in fallback grid)
                }
                else
                {
                    gridNodes.Add(node); // No anchor, draw in grid
                }
            }

            // Clean up old cached centers that are no longer visible to avoid memory leaks
            var visibleKeys = new HashSet<string>(initialProjected.Select(x => x.node.Name));
            var keysToRemove = _cardCenters.Keys.Where(k => !visibleKeys.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _cardCenters.Remove(key);
            }

            // Create layout records for projected cards
            var layouts = new List<ProjectedCardLayout>();
            foreach (var item in initialProjected)
            {
                float anchorX = item.screenPos.x;
                float anchorY = Screen.height - item.screenPos.y;

                bool isLocation = item.node.HasChildren;
                bool focused = isFocused(item.node.Name);

                float cardWidth = focused ? 420f : (isLocation ? 140f : 280f);
                float cardHeight = focused ? 320f : (isLocation ? 32f : 130f);

                // Default target center position (centered horizontally above 3D anchor point)
                Vector2 targetCenter = new Vector2(anchorX, anchorY - cardHeight / 2f - 40f);

                // Retrieve from cache or initialize
                if (!_cardCenters.TryGetValue(item.node.Name, out var currentCenter))
                {
                    currentCenter = targetCenter;
                    _cardCenters[item.node.Name] = currentCenter;
                }

                layouts.Add(new ProjectedCardLayout(item.node, new Vector2(anchorX, anchorY), item.distance, targetCenter, currentCenter, cardWidth, cardHeight));
            }

            // Calculate mutual repulsion forces for overlapping cards
            for (int i = 0; i < layouts.Count; i++)
            {
                for (int j = i + 1; j < layouts.Count; j++)
                {
                    var a = layouts[i];
                    var b = layouts[j];

                    if (a.Rect.Overlaps(b.Rect))
                    {
                        // Calculate overlap on Y axis
                        float overlapY = Mathf.Min(a.Rect.yMax, b.Rect.yMax) - Mathf.Max(a.Rect.yMin, b.Rect.yMin);
                        if (overlapY > 0)
                        {
                            // A continuous push force proportional to overlap to eliminate jitter/oscillations
                            float pushForce = overlapY * 0.4f;

                            if (a.CurrentCenter.y < b.CurrentCenter.y)
                            {
                                a.RepulsionForce += new Vector2(0f, -pushForce);
                                b.RepulsionForce += new Vector2(0f, pushForce);
                            }
                            else
                            {
                                a.RepulsionForce += new Vector2(0f, pushForce);
                                b.RepulsionForce += new Vector2(0f, -pushForce);
                            }
                        }
                    }
                }
            }

            // Integrate forces: update positions smoothly
            float attractionStrength = 0.08f; // Softer attraction strength to allow repulsion to dominate
            foreach (var layout in layouts)
            {
                Vector2 attraction = (layout.TargetCenter - layout.CurrentCenter) * attractionStrength;
                Vector2 nextCenter = layout.CurrentCenter + attraction + layout.RepulsionForce;

                // Create tentative rect and clamp to safe boundaries
                Rect nextRect = new Rect(nextCenter.x - layout.Width / 2f, nextCenter.y - layout.Height / 2f, layout.Width, layout.Height);
                nextRect = ClampRect(nextRect, layout.Width, layout.Height);

                // Update current layout state and persistent cache
                layout.CurrentCenter = nextRect.center;
                _cardCenters[layout.Node.Name] = layout.CurrentCenter;
            }

            // Draw projected cards (sorted by distance, far to near)
            layouts.Sort((a, b) => b.Distance.CompareTo(a.Distance));
            foreach (var layout in layouts)
            {
                DrawNodeCard(layout.Node, layout.Rect, layout.AnchorPos, mousePos);
            }

            // Draw grid cards below
            if (gridNodes.Count > 0)
            {
                DrawCardsGrid(gridNodes, mousePos);
            }
        }

        private void DrawCardsGrid(List<GameNode> nodes, Vector2 mousePos)
        {
            float cardWidth = 280f;
            float cardHeight = 130f;
            float spacing = 20f;
            float startX = 40f;
            float startY = 140f;
            int cardsPerRow = Mathf.Max(1, (int)((Screen.width - startX * 2) / (cardWidth + spacing)));

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                int row = i / cardsPerRow;
                int col = i % cardsPerRow;
                float x = startX + col * (cardWidth + spacing);
                float y = startY + row * (cardHeight + spacing);
                var cardRect = new Rect(x, y, cardWidth, cardHeight);

                bool isHovered = cardRect.Contains(mousePos);
                bool isFlipped = _gameManager.IsNodeFlipped(node.Name);
                bool focused = isFocused(node.Name);

                List<SlottedResource?>? slotted = null;
                if (node.Requires != null && node.Requires.Count > 0)
                {
                    slotted = _gameManager.GetSlotsForNode(node.Name);
                }

                string backText = (node.Resolve?.Type == ResolveType.Observe) ? (node.Resolve?.ObserveText ?? "") : "";

                var interaction = CardDrawer.DrawCard(cardRect, node, isHovered, isFlipped, focused,
                    slotted, node.Clocks, backText, mousePos, _gameManager);

                if (interaction.CardClicked)
                {
                    _gameManager.OnNodeCardClicked(node);
                }
                if (interaction.ClickedSlotIndex != -1 && slotted != null && node.Requires != null)
                {
                    _gameManager.OnSlotClicked(node, interaction.ClickedSlotIndex);
                }
                if (interaction.ExecuteClicked)
                {
                    _gameManager.ExecuteNodeAction(node);
                }
            }
        }

        private void DrawNodeCard(GameNode node, Rect cardRect, Vector2 anchorPos, Vector2 mousePos)
        {
            float anchorX = anchorPos.x;
            float anchorY = anchorPos.y;

            // Determine target Y on card edge (bottom center if card is above anchor, top center if card is below)
            float targetY = (anchorY > cardRect.yMax) ? cardRect.yMax : (anchorY < cardRect.yMin ? cardRect.yMin : anchorY);
            float targetX = cardRect.center.x;

            // Draw elbow polyline: (anchorX, anchorY) -> (targetX, anchorY) -> (targetX, targetY)
            Vector2 pStart = new Vector2(anchorX, anchorY);
            Vector2 pElbow = new Vector2(targetX, anchorY);
            Vector2 pEnd = new Vector2(targetX, targetY);

            Color lineColor = isFocused(node.Name) ? IMGUIStyles.PrimaryColor : new Color(0.671f, 0.780f, 1.0f, 0.35f);
            float lineThickness = isFocused(node.Name) ? 2f : 1f;

            // Draw the leader line segments behind the card
            IMGUIStyles.DrawLine(pStart, pElbow, lineColor, lineThickness);
            IMGUIStyles.DrawLine(pElbow, pEnd, lineColor, lineThickness);

            bool isHovered = cardRect.Contains(mousePos);
            bool isFlipped = _gameManager.IsNodeFlipped(node.Name);
            bool focused = isFocused(node.Name);

            List<SlottedResource?>? slotted = null;
            if (node.Requires != null && node.Requires.Count > 0)
            {
                slotted = _gameManager.GetSlotsForNode(node.Name);
            }

            string backText = (node.Resolve?.Type == ResolveType.Observe) ? (node.Resolve?.ObserveText ?? "") : "";

            var interaction = CardDrawer.DrawCard(cardRect, node, isHovered, isFlipped, focused,
                slotted, node.Clocks, backText, mousePos, _gameManager);

            if (interaction.CardClicked)
            {
                _gameManager.OnNodeCardClicked(node);
            }

            if (interaction.ClickedSlotIndex != -1 && slotted != null && node.Requires != null)
            {
                _gameManager.OnSlotClicked(node, interaction.ClickedSlotIndex);
            }

            if (interaction.ExecuteClicked)
            {
                _gameManager.ExecuteNodeAction(node);
            }
        }

        private bool isFocused(string nodeName)
        {
            return _gameManager.FocusedNodeName == nodeName;
        }

        private List<GameClock> GetCurrentClocks()
        {
            var clocks = new List<GameClock>();
            if (_gameManager.NavigationStack.Count == 0)
            {
                foreach (var node in _gameManager.SceneManager.CurrentWorldNodes)
                {
                    clocks.AddRange(node.Clocks);
                }
            }
            else
            {
                var currentNode = _gameManager.NavigationStack[_gameManager.NavigationStack.Count - 1];
                clocks.AddRange(currentNode.Clocks);
            }
            return clocks;
        }

        private Rect ClampRect(Rect r, float cardWidth, float cardHeight)
        {
            float minX = 20f;
            float maxX = Screen.width - cardWidth - 20f;
            float minY = 90f;
            float maxY = Screen.height - 180f - cardHeight;
            return new Rect(Mathf.Clamp(r.x, minX, maxX), Mathf.Clamp(r.y, minY, maxY), cardWidth, cardHeight);
        }

        private class ProjectedCardLayout
        {
            public GameNode Node { get; }
            public Vector2 AnchorPos { get; }
            public float Distance { get; }
            public Vector2 TargetCenter { get; }
            public Vector2 CurrentCenter { get; set; }
            public Vector2 RepulsionForce { get; set; }
            public float Width { get; }
            public float Height { get; }
            public Rect Rect => new Rect(CurrentCenter.x - Width / 2f, CurrentCenter.y - Height / 2f, Width, Height);

            public ProjectedCardLayout(GameNode node, Vector2 anchorPos, float distance, Vector2 targetCenter, Vector2 currentCenter, float width, float height)
            {
                Node = node;
                AnchorPos = anchorPos;
                Distance = distance;
                TargetCenter = targetCenter;
                CurrentCenter = currentCenter;
                Width = width;
                Height = height;
                RepulsionForce = Vector2.zero;
            }
        }
    }
}
