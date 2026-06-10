#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SSNoir
{
    public static class UIHelper
    {
        public static TMP_FontAsset? DefaultFont;

        public static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 size = default, bool raycastTarget = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            if (size != default)
            {
                rt.sizeDelta = size;
            }
            
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return go;
        }

        public static GameObject CreateVerticalLayout(Transform parent, string name, float spacing, RectOffset? padding = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // Transparent by default
            img.raycastTarget = false;

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            if (padding != null) vlg.padding = padding;

            var csf = go.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        public static GameObject CreateHorizontalLayout(Transform parent, string name, float spacing, RectOffset? padding = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = false;

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            if (padding != null) hlg.padding = padding;

            var csf = go.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        public static TMP_Text CreateText(Transform parent, string content, int fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (DefaultFont != null)
            {
                text.font = DefaultFont;
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return text;
        }

        public static Button CreateButton(Transform parent, string label, Color normalColor, System.Action onClick, Vector2 size = default)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            if (size != default)
            {
                rt.sizeDelta = size;
            }
            else
            {
                rt.sizeDelta = new Vector2(100, 30);
            }

            var img = go.GetComponent<Image>();
            img.color = normalColor;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = rt.sizeDelta.x;
            le.preferredHeight = rt.sizeDelta.y;
            
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = cb;

            btn.onClick.AddListener(() => onClick?.Invoke());
            
            CreateText(go.transform, label, 12, Color.white);
            return btn;
        }
    }
}
