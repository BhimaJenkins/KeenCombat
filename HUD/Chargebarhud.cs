using TMPro;
using UnityEngine;

namespace KeenCombat.HUD
{
    // -----------------------------------------------------------------------
    // ChargeBarHud
    //
    // Creates a simple charge bar UI overlay when the player is charging
    // Ragnarök. Uses Valheim's native TextMeshPro font grabbed from an
    // existing TMP component in the scene.
    // -----------------------------------------------------------------------
    public static class ChargeBarHud
    {
        private static GameObject? _barRoot = null;
        private static UnityEngine.UI.Image? _fillImage = null;
        private static TextMeshProUGUI? _label = null;

        public static void Show()
        {
            if (_barRoot != null) return;

            var hud = Hud.instance;
            if (hud == null) return;

            // Grab Valheim's TMP font from any existing TMP component in scene
            TMP_FontAsset? valheimFont = null;
            var existingTmp = Object.FindAnyObjectByType<TextMeshProUGUI>();
            if (existingTmp != null)
                valheimFont = existingTmp.font;

            // Root object
            _barRoot = new GameObject("KC_ChargeBar");
            _barRoot.transform.SetParent(hud.transform, false);

            var rect = _barRoot.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(200f, 20f);

            // Background
            var bg = new GameObject("BG");
            bg.transform.SetParent(_barRoot.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new UnityEngine.Color(0.05f, 0.05f, 0.05f, 0.85f);

            // Fill bar
            var fill = new GameObject("Fill");
            fill.transform.SetParent(_barRoot.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, 0f);
            _fillImage = fill.AddComponent<UnityEngine.UI.Image>();
            _fillImage.color = new UnityEngine.Color(1f, 0.4f, 0.05f, 1f);

            // Label using TMP
            var label = new GameObject("Label");
            label.transform.SetParent(_barRoot.transform, false);
            var labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            _label = label.AddComponent<TextMeshProUGUI>();
            _label.text = "RAGNARÖK";
            _label.fontSize = 11f;
            _label.color = UnityEngine.Color.white;
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontStyle = FontStyles.Bold;

            if (valheimFont != null)
                _label.font = valheimFont;
        }

        public static void UpdateFill(float ratio)
        {
            if (_fillImage == null) return;

            float clampedRatio = Mathf.Clamp01(ratio);

            var fillRect = _fillImage.rectTransform;
            if (fillRect != null)
                fillRect.sizeDelta = new Vector2(200f * clampedRatio, 0f);

            // Shifts from orange to red as charge fills
            _fillImage.color = UnityEngine.Color.Lerp(
                new UnityEngine.Color(1f, 0.5f, 0.05f, 1f),
                new UnityEngine.Color(1f, 0.05f, 0.05f, 1f),
                clampedRatio);

            // Label shows shot count progress
            if (_label != null)
            {
                if (ratio >= 1f) _label.text = "RAGNARÖK  ███";
                else if (ratio >= 0.67f) _label.text = "RAGNARÖK  ██";
                else if (ratio >= 0.33f) _label.text = "RAGNARÖK  █";
                else _label.text = "RAGNARÖK";
            }
        }

        public static void Hide()
        {
            if (_barRoot != null)
            {
                Object.Destroy(_barRoot);
                _barRoot = null;
                _fillImage = null;
                _label = null;
            }
        }
    }
}