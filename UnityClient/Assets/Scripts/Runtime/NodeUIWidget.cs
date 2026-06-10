#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using SSNoir.Core;

namespace SSNoir
{
    public class NodeUIWidget : MonoBehaviour
    {
        public GameNode Node { get; private set; } = null!;
        public NodeAnchor Anchor { get; private set; } = null!;
        private SSNoirGameManager _gameManager = null!;
        
        private RectTransform _rectTransform = null!;
        private GameObject? _mainLayoutGo;

        public void Setup(GameNode node, NodeAnchor anchor, SSNoirGameManager gameManager)
        {
            Node = node;
            Anchor = anchor;
            _gameManager = gameManager;
            _rectTransform = GetComponent<RectTransform>();

            RebuildUI();
        }

        public void RebuildUI()
        {
            // Clear existing UI elements
            if (_mainLayoutGo != null)
            {
                Destroy(_mainLayoutGo);
            }

            bool isFocused = _gameManager.FocusedNodeName == Node.Name;

            // 1. Create a container panel with dark translucent background
            Color bgColor = isFocused ? new Color(0.12f, 0.12f, 0.18f, 0.95f) : new Color(0.08f, 0.08f, 0.08f, 0.8f);
            
            // Outer panel
            _mainLayoutGo = UIHelper.CreatePanel(transform, "WidgetLayout", bgColor);
            var layoutRt = _mainLayoutGo.GetComponent<RectTransform>();
            
            // Set anchoring to fill parent
            layoutRt.anchorMin = Vector2.zero;
            layoutRt.anchorMax = Vector2.one;
            layoutRt.sizeDelta = Vector2.zero;

            // Add Vertical Layout Group for contents
            var layoutGroup = _mainLayoutGo.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 8f;
            layoutGroup.padding = new RectOffset(12, 12, 12, 12);
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            var fitter = _mainLayoutGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Node Name / Title
            var titleText = UIHelper.CreateText(_mainLayoutGo.transform, Node.Name, 14, Color.white, TextAlignmentOptions.Center);
            titleText.fontStyle = FontStyles.Bold;

            // Render Clocks
            if (Node.Clocks != null && Node.Clocks.Count > 0)
            {
                foreach (var clock in Node.Clocks)
                {
                    string clockText = $"{clock.Label}: {clock.Current}/{clock.Max}";
                    UIHelper.CreateText(_mainLayoutGo.transform, clockText, 11, new Color(1f, 0.75f, 0.2f), TextAlignmentOptions.Center);
                }
            }

            if (isFocused)
            {
                // Detail Mode: Render Slots (Requires) and Action Button (Resolve)
                
                // Slots
                if (Node.Requires != null && Node.Requires.Count > 0)
                {
                    UIHelper.CreateText(_mainLayoutGo.transform, "需要投入:", 11, new Color(0.75f, 0.75f, 0.75f));
                    
                    var slotsContainer = UIHelper.CreateHorizontalLayout(_mainLayoutGo.transform, "SlotsContainer", 8f);
                    
                    var slottedList = _gameManager.GetSlotsForNode(Node.Name);
                    
                    for (int i = 0; i < Node.Requires.Count; i++)
                    {
                        var req = Node.Requires[i];
                        var slottedRes = (slottedList != null && i < slottedList.Count) ? slottedList[i] : null;
                        int slotIndex = i;

                        string slotLabel;
                        Color slotColor;

                        if (slottedRes != null)
                        {
                            // Filled slot
                            if (slottedRes.Type == "die")
                            {
                                slotLabel = $"[🎲 {slottedRes.Value}]";
                            }
                            else
                            {
                                slotLabel = $"[{slottedRes.ItemName}]";
                            }
                            slotColor = new Color(0.15f, 0.5f, 0.25f, 1f); // Greenish
                        }
                        else
                        {
                            // Empty slot
                            if (req.Type == "die")
                            {
                                slotLabel = "[ 放入骰子 ]";
                            }
                            else
                            {
                                slotLabel = $"[ 放入: {req.ItemName} x{req.Qty} ]";
                            }
                            slotColor = new Color(0.2f, 0.2f, 0.25f, 1f); // Greyish
                        }

                        UIHelper.CreateButton(slotsContainer.transform, slotLabel, slotColor, () =>
                        {
                            _gameManager.OnSlotClicked(Node, slotIndex);
                        }, new Vector2(120, 28));
                    }
                }

                // Action Resolve Button
                if (Node.HasResolve)
                {
                    string btnLabel = "执行行动";
                    if (Node.Resolve != null)
                    {
                        if (Node.Resolve.Type == ResolveType.Instant) btnLabel = "执行行动";
                        else if (Node.Resolve.Type == ResolveType.Roll) btnLabel = $"进行判定 ({Node.Resolve.SkillName})";
                        else if (Node.Resolve.Type == ResolveType.Observe) btnLabel = "查看线索";
                    }

                    // Active if all slots filled
                    bool canExecute = true;
                    var slottedList = _gameManager.GetSlotsForNode(Node.Name);
                    if (Node.Requires != null && Node.Requires.Count > 0)
                    {
                        if (slottedList == null)
                        {
                            canExecute = false;
                        }
                        else
                        {
                            foreach (var s in slottedList)
                            {
                                if (s == null)
                                {
                                    canExecute = false;
                                    break;
                                }
                            }
                        }
                    }

                    Color executeBtnColor = canExecute ? new Color(0.85f, 0.35f, 0.1f) : new Color(0.35f, 0.35f, 0.35f);
                    
                    UIHelper.CreateButton(_mainLayoutGo.transform, btnLabel, executeBtnColor, () =>
                    {
                        if (canExecute)
                        {
                            _gameManager.ExecuteNodeAction(Node);
                        }
                        else
                        {
                            _gameManager.ShowNotification("尚未填满所有需求槽位");
                        }
                    }, new Vector2(150, 32));
                }

                // Back Button
                UIHelper.CreateButton(_mainLayoutGo.transform, "返回地图", new Color(0.25f, 0.25f, 0.3f), () =>
                {
                    _gameManager.SetFocusedNode(null);
                }, new Vector2(100, 26));
            }
            else
            {
                // Compact Mode
                if (Node.HasChildren)
                {
                    UIHelper.CreateButton(_mainLayoutGo.transform, "进入", new Color(0.35f, 0.55f, 0.25f), () =>
                    {
                        _gameManager.NavigateIntoNode(Node);
                    }, new Vector2(100, 26));
                }
                else
                {
                    UIHelper.CreateButton(_mainLayoutGo.transform, "交互", new Color(0.15f, 0.35f, 0.55f), () =>
                    {
                        _gameManager.SetFocusedNode(Node.Name);
                    }, new Vector2(80, 26));
                }
            }

            // Adjust the size of this UI Widget to fit its content
            Canvas.ForceUpdateCanvases();
            var preferredHeight = LayoutUtility.GetPreferredHeight(layoutRt);
            _rectTransform.sizeDelta = new Vector2(isFocused ? 280f : 180f, preferredHeight > 0 ? preferredHeight + 24f : (isFocused ? 200f : 90f));
        }

        private void Update()
        {
            if (Anchor == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(Anchor.transform.position);
            
            // check if anchor is behind the camera
            if (screenPos.z < 0)
            {
                if (_mainLayoutGo != null && _mainLayoutGo.activeSelf)
                {
                    _mainLayoutGo.SetActive(false);
                }
            }
            else
            {
                if (_mainLayoutGo != null && !_mainLayoutGo.activeSelf)
                {
                    _mainLayoutGo.SetActive(true);
                }
                
                // Position widget at screen coordinate
                _rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
            }
        }
    }
}
