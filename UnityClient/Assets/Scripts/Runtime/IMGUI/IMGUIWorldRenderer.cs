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
            var projectedCards = new List<(GameNode node, Vector3 screenPos, float distance)>();
            var gridNodes = new List<GameNode>();

            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(focusedName) && node.Name != focusedName)
                    continue;

                var anchor = _gameManager.SceneDirectory?.GetAnchor(node.Name);
                if (anchor != null)
                {
                    var screenPos = cam.WorldToScreenPoint(anchor.transform.position);
                    if (screenPos.z >= 0)
                    {
                        projectedCards.Add((node, screenPos, screenPos.z));
                    }
                    else
                    {
                        gridNodes.Add(node); // Behind camera, fallback to grid
                    }
                }
                else
                {
                    gridNodes.Add(node); // No anchor, draw in grid
                }
            }

            // Draw projected cards first (sorted by distance, far to near)
            projectedCards.Sort((a, b) => b.distance.CompareTo(a.distance));
            foreach (var card in projectedCards)
            {
                DrawNodeCard(card.node, card.screenPos, mousePos);
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

        private void DrawNodeCard(GameNode node, Vector3 screenPos, Vector2 mousePos)
        {
            float cardWidth = isFocused(node.Name) ? 420f : 280f;
            float cardHeight = isFocused(node.Name) ? 320f : 130f;
            float cardX = screenPos.x - cardWidth / 2f;
            float cardY = Screen.height - screenPos.y - cardHeight / 2f;
            var cardRect = new Rect(cardX, cardY, cardWidth, cardHeight);

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
    }
}
