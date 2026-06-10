#nullable enable
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SSNoir.Core;

namespace SSNoir
{
    public static class DiceAnimator
    {
        public static IEnumerator PlayRoll(int finalValue, RollOutcome outcome, Canvas canvas)
        {
            Debug.Log($"[SSNoir] Dice roll animation started. finalValue={finalValue}, outcome={outcome}");

            // 1. Blocker background
            var blocker = UIHelper.CreatePanel(canvas.transform, "DiceRollBlocker", new Color(0, 0, 0, 0.7f), default, true);
            blocker.transform.SetAsLastSibling();

            var blockerRt = blocker.GetComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.offsetMin = Vector2.zero;
            blockerRt.offsetMax = Vector2.zero;
            blockerRt.sizeDelta = Vector2.zero;

            // 2. Central Panel
            var panel = UIHelper.CreatePanel(blocker.transform, "DiceRollPanel", new Color(0.1f, 0.12f, 0.16f, 0.95f), new Vector2(220, 200));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.9f, 0.75f, 0.2f, 0.8f);
            outline.effectDistance = new Vector2(2f, 2f);

            var vlgGo = UIHelper.CreateVerticalLayout(panel.transform, "Content", 12f, new RectOffset(10, 10, 10, 10));
            var vlg = vlgGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;

            var vlgRt = vlgGo.GetComponent<RectTransform>();
            vlgRt.anchorMin = Vector2.zero;
            vlgRt.anchorMax = Vector2.one;
            vlgRt.sizeDelta = Vector2.zero;

            var title = UIHelper.CreateText(vlgGo.transform, "命运判定中...", 13, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Center);
            
            var dieText = UIHelper.CreateText(vlgGo.transform, "D1", 48, Color.white, TextAlignmentOptions.Center);
            dieText.fontStyle = FontStyles.Bold;

            Canvas.ForceUpdateCanvases();
            yield return null;

            // 3. Roll rolling effect
            float duration = 1.2f;
            float elapsed = 0f;
            int lastVal = 1;
            var rand = new System.Random();

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float interval = Mathf.Lerp(0.05f, 0.22f, t);

                int nextVal;
                do
                {
                    nextVal = rand.Next(1, 7);
                } while (nextVal == lastVal);

                lastVal = nextVal;
                dieText.text = "D" + lastVal;

                // Micro scale jitter
                dieText.transform.localScale = Vector3.one * Random.Range(0.9f, 1.15f);

                yield return new WaitForSecondsRealtime(interval);
                elapsed += interval;
            }

            // 4. Reveal final roll value
            dieText.text = "D" + finalValue;
            dieText.color = new Color(1f, 0.85f, 0.1f);

            // Scale pulse animation
            float pulseElapsed = 0f;
            while (pulseElapsed < 0.25f)
            {
                pulseElapsed += Time.unscaledDeltaTime;
                float scale = 1f + Mathf.Sin((pulseElapsed / 0.25f) * Mathf.PI) * 0.35f;
                dieText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            dieText.transform.localScale = Vector3.one;

            // 5. Reveal outcome
            string outcomeStr = outcome == RollOutcome.Success ? "判定成功" : outcome == RollOutcome.Neutral ? "判定中性" : "判定失败";
            Color outcomeColor = outcome == RollOutcome.Success ? new Color(0.2f, 0.8f, 0.4f) 
                               : outcome == RollOutcome.Neutral ? new Color(0.9f, 0.8f, 0.2f) 
                               : new Color(0.9f, 0.2f, 0.2f);
            
            var outcomeText = UIHelper.CreateText(vlgGo.transform, outcomeStr, 16, outcomeColor, TextAlignmentOptions.Center);
            outcomeText.fontStyle = FontStyles.Bold;

            // Delay for readability
            yield return new WaitForSecondsRealtime(1.0f);

            // Cleanup
            Object.Destroy(blocker);
            Debug.Log("[SSNoir] Dice roll animation finished.");
        }
    }
}
