#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
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

        public void SetData(GameNode node)
        {
            Node = node;
            RebuildUI();
        }

        public void RebuildUI()
        {
            // Clear existing UI elements
            if (_mainLayoutGo != null)
            {
                Destroy(_mainLayoutGo);
            }

            bool isFlipped = _gameManager.IsNodeFlipped(Node.Name);
            if (isFlipped)
            {
                // Render Flipped Card (Back face containing ObserveText)
                _mainLayoutGo = new GameObject("WidgetLayout", typeof(RectTransform), typeof(Image));
                _mainLayoutGo.transform.SetParent(transform, false);
                
                var flippedImg = _mainLayoutGo.GetComponent<Image>();
                flippedImg.color = new Color(0.06f, 0.16f, 0.12f, 0.95f); // Beautiful Teal Backing

                var flippedRt = _mainLayoutGo.GetComponent<RectTransform>();
                flippedRt.anchorMin = Vector2.zero;
                flippedRt.anchorMax = Vector2.one;
                flippedRt.sizeDelta = Vector2.zero;

                var cardBtn = _mainLayoutGo.AddComponent<Button>();
                cardBtn.targetGraphic = flippedImg;
                cardBtn.onClick.AddListener(() =>
                {
                    _gameManager.OnNodeCardClicked(Node);
                });

                var flippedLg = _mainLayoutGo.AddComponent<VerticalLayoutGroup>();
                flippedLg.spacing = 6f;
                flippedLg.padding = new RectOffset(12, 12, 12, 12);
                flippedLg.childAlignment = TextAnchor.UpperCenter;
                flippedLg.childControlWidth = true;
                flippedLg.childControlHeight = true;
                flippedLg.childForceExpandWidth = true;
                flippedLg.childForceExpandHeight = false;

                var flippedFitter = _mainLayoutGo.AddComponent<ContentSizeFitter>();
                flippedFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                flippedFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Title
                var flippedTitle = UIHelper.CreateText(_mainLayoutGo.transform, Node.Name, 13, Color.white, TextAlignmentOptions.Center);
                flippedTitle.fontStyle = FontStyles.Bold;

                // Subtitle
                var flippedSub = UIHelper.CreateText(_mainLayoutGo.transform, "— 已解读线索 —", 10, new Color(1f, 0.8f, 0.2f), TextAlignmentOptions.Center);
                flippedSub.fontStyle = FontStyles.Italic;

                // Clue Content Text
                string clueText = Node.Resolve != null ? Node.Resolve.ObserveText : "";
                var contentText = UIHelper.CreateText(_mainLayoutGo.transform, clueText, 11, new Color(0.9f, 0.9f, 0.9f), TextAlignmentOptions.Center);
                contentText.enableWordWrapping = true;

                Canvas.ForceUpdateCanvases();
                var flippedHeight = LayoutUtility.GetPreferredHeight(flippedRt);
                _rectTransform.sizeDelta = new Vector2(180f, flippedHeight > 0 ? flippedHeight : 100f);
                return;
            }

            bool isFocused = _gameManager.FocusedNodeName == Node.Name;

            // 1. Determine Node Type and styling
            string typeLabel = "地点";
            Color normalColor = new Color(0.12f, 0.16f, 0.22f, 0.85f); // Slate Blue for Locations
            Color focusedColor = new Color(0.15f, 0.22f, 0.32f, 0.95f);

            if (Node.HasChildren)
            {
                typeLabel = "地点";
                normalColor = new Color(0.12f, 0.16f, 0.22f, 0.85f);
                focusedColor = new Color(0.15f, 0.22f, 0.32f, 0.95f);
            }
            else if (Node.Resolve != null)
            {
                if (Node.Resolve.Type == ResolveType.Instant)
                {
                    typeLabel = "行动";
                    normalColor = new Color(0.24f, 0.14f, 0.08f, 0.85f); // Warm Amber for Actions
                    focusedColor = new Color(0.36f, 0.22f, 0.12f, 0.95f);
                }
                else if (Node.Resolve.Type == ResolveType.Roll)
                {
                    typeLabel = "判定";
                    normalColor = new Color(0.18f, 0.12f, 0.24f, 0.85f); // Purple for Checks
                    focusedColor = new Color(0.26f, 0.18f, 0.36f, 0.95f);
                }
                else if (Node.Resolve.Type == ResolveType.Observe)
                {
                    typeLabel = "观察";
                    normalColor = new Color(0.08f, 0.18f, 0.14f, 0.85f); // Forest Teal for Observations
                    focusedColor = new Color(0.12f, 0.26f, 0.20f, 0.95f);
                }
            }

            Color bgColor = isFocused ? focusedColor : normalColor;

            // 2. Create the card panel
            _mainLayoutGo = new GameObject("WidgetLayout", typeof(RectTransform), typeof(Image));
            _mainLayoutGo.transform.SetParent(transform, false);
            
            var img = _mainLayoutGo.GetComponent<Image>();
            img.color = bgColor;

            var layoutRt = _mainLayoutGo.GetComponent<RectTransform>();
            layoutRt.anchorMin = Vector2.zero;
            layoutRt.anchorMax = Vector2.one;
            layoutRt.sizeDelta = Vector2.zero;

            // Make the entire card a button if not focused, OR if focused but has no requirements.
            // This satisfies the "whole card is button" and "trigger execution via detail card itself when no requirements" behaviors.
            bool cardIsButton = !isFocused || (Node.Requires == null || Node.Requires.Count == 0);
            if (cardIsButton)
            {
                var cardBtn = _mainLayoutGo.AddComponent<Button>();
                cardBtn.targetGraphic = img;
                var cb = cardBtn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
                cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                cardBtn.colors = cb;

                cardBtn.onClick.AddListener(() =>
                {
                    if (!isFocused)
                    {
                        _gameManager.OnNodeCardClicked(Node);
                    }
                    else
                    {
                        // Detail mode execution trigger for zero-requirement actions
                        _gameManager.ExecuteNodeAction(Node);
                    }
                });
            }

            // Vertical Layout Group
            var layoutGroup = _mainLayoutGo.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 8f;
            layoutGroup.padding = new RectOffset(14, 14, 14, 14);
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            var fitter = _mainLayoutGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            var titleText = UIHelper.CreateText(_mainLayoutGo.transform, Node.Name, 14, Color.white, TextAlignmentOptions.Center);
            titleText.fontStyle = FontStyles.Bold;

            // Type Label Subtitle
            Color typeTagColor = isFocused ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.7f, 0.7f, 0.7f);
            var subtitleText = UIHelper.CreateText(_mainLayoutGo.transform, $"— {typeLabel} —", 11, typeTagColor, TextAlignmentOptions.Center);
            subtitleText.fontStyle = FontStyles.Italic;

            // Clocks
            if (Node.Clocks != null && Node.Clocks.Count > 0)
            {
                foreach (var clock in Node.Clocks)
                {
                    var clockContainer = UIHelper.CreateVerticalLayout(_mainLayoutGo.transform, "Clock_" + clock.Label, 4f);
                    
                    var headerRow = UIHelper.CreateHorizontalLayout(clockContainer.transform, "HeaderRow", 10f);
                    var headerHlg = headerRow.GetComponent<HorizontalLayoutGroup>();
                    headerHlg.childAlignment = TextAnchor.UpperLeft;
                    headerHlg.childControlWidth = true;
                    headerHlg.childControlHeight = true;
                    headerHlg.childForceExpandWidth = true;
                    
                    var labelTxt = UIHelper.CreateText(headerRow.transform, clock.Label, 10, new Color(0.9f, 0.9f, 0.9f), TextAlignmentOptions.Left);
                    labelTxt.fontStyle = FontStyles.Bold;
                    
                    var valTxt = UIHelper.CreateText(headerRow.transform, $"{clock.Current}/{clock.Max}", 10, new Color(1f, 0.8f, 0.2f), TextAlignmentOptions.Right);
                    valTxt.fontStyle = FontStyles.Bold;

                    var progressTrack = UIHelper.CreatePanel(clockContainer.transform, "ProgressTrack", new Color(0.12f, 0.12f, 0.16f, 1f), new Vector2(0f, 6f));
                    var trackRt = progressTrack.GetComponent<RectTransform>();
                    var trackLe = progressTrack.AddComponent<LayoutElement>();
                    trackLe.preferredHeight = 6f;
                    trackLe.flexibleWidth = 1f;

                    float fillPct = Mathf.Clamp01(clock.Max > 0 ? (float)clock.Current / clock.Max : 0f);
                    var progressFill = UIHelper.CreatePanel(progressTrack.transform, "ProgressFill", new Color(1f, 0.75f, 0.2f, 1f));
                    var fillRt = progressFill.GetComponent<RectTransform>();
                    fillRt.anchorMin = Vector2.zero;
                    fillRt.anchorMax = new Vector2(fillPct, 1f);
                    fillRt.pivot = new Vector2(0f, 0.5f);
                    fillRt.anchoredPosition = Vector2.zero;
                    fillRt.sizeDelta = Vector2.zero;
                }
            }

            if (!isFocused)
            {
                // Compact Mode: status hint
                if (Node.Requires != null && Node.Requires.Count > 0)
                {
                    string reqsText = "需要: " + string.Join(", ", Node.Requires.Select(r => r.Type == "die" ? "骰子" : r.ItemName));
                    UIHelper.CreateText(_mainLayoutGo.transform, reqsText, 10, new Color(0.8f, 0.8f, 0.8f));
                }
            }
            else
            {
                // Detail Mode: Render slots, execute button, and back button inside the card as secondary actions
                
                // Slots
                if (Node.Requires != null && Node.Requires.Count > 0)
                {
                    UIHelper.CreateText(_mainLayoutGo.transform, "投入需求:", 11, new Color(0.8f, 0.8f, 0.8f));
                    
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
                            if (slottedRes.Type == "die")
                            {
                                slotLabel = $"[ D{slottedRes.Value} ]";
                            }
                            else
                            {
                                slotLabel = $"[ {slottedRes.ItemName} ]";
                            }
                            slotColor = new Color(0.15f, 0.5f, 0.25f, 1f); // Green
                        }
                        else
                        {
                            if (req.Type == "die")
                            {
                                slotLabel = "[ 放入骰子 ]";
                            }
                            else
                            {
                                slotLabel = $"[ {req.ItemName} x{req.Qty} ]";
                            }
                            slotColor = new Color(0.25f, 0.25f, 0.3f, 1f); // Grey
                        }

                        UIHelper.CreateButton(slotsContainer.transform, slotLabel, slotColor, () =>
                        {
                            _gameManager.OnSlotClicked(Node, slotIndex);
                        }, new Vector2(110, 28));
                    }
                }

                // Execute Button (Resolve)
                if (Node.HasResolve)
                {
                    string btnLabel = "执行行动";
                    if (Node.Resolve != null)
                    {
                        if (Node.Resolve.Type == ResolveType.Instant) btnLabel = "确认执行";
                        else if (Node.Resolve.Type == ResolveType.Roll) btnLabel = $"进行判定 ({Node.Resolve.SkillName})";
                        else if (Node.Resolve.Type == ResolveType.Observe) btnLabel = "查看线索";
                    }

                    // Check if requirements are satisfied
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
            }

            // Adjust the size of this UI Widget to fit its content
            Canvas.ForceUpdateCanvases();
            var preferredHeight = LayoutUtility.GetPreferredHeight(layoutRt);
            _rectTransform.sizeDelta = new Vector2(isFocused ? 280f : 180f, preferredHeight > 0 ? preferredHeight : (isFocused ? 200f : 90f));
        }

        private void Update()
        {
            if (Anchor == null || _gameManager.NavigationStack.Count > 0) return;

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
