using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace KeenCombat.HUD
{
    [HarmonyPatch(typeof(Hud), "Awake")]
    public static class SkillSlotWidget_Awake
    {
        internal static Image? SkillIcon = null;
        internal static Image? CooldownOverlay = null;
        internal static Text? CooldownText = null;
        internal static GameObject? WidgetRoot = null;

        static void Postfix(Hud __instance)
        {
            BuildWidget(__instance);
        }

        private static void BuildWidget(Hud hud)
        {
            WidgetRoot = new GameObject("WeaponSkillSlot");
            WidgetRoot.transform.SetParent(hud.m_rootObject.transform, false);

            var rootRect = WidgetRoot.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(64f, 64f);
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(Plugin.SkillWidgetX.Value, Plugin.SkillWidgetY.Value);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(WidgetRoot.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(64f, 64f);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0f);

            // Skill icon
            var iconGO = new GameObject("SkillIcon");
            iconGO.transform.SetParent(WidgetRoot.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(64f, 64f);
            SkillIcon = iconGO.AddComponent<Image>();
            SkillIcon.preserveAspect = true;
            SkillIcon.color = new Color(1f, 1f, 1f, 0.3f);

            // Cooldown overlay
            var overlayGO = new GameObject("CooldownOverlay");
            overlayGO.transform.SetParent(WidgetRoot.transform, false);
            var overlayRect = overlayGO.AddComponent<RectTransform>();
            overlayRect.sizeDelta = new Vector2(64f, 64f);
            CooldownOverlay = overlayGO.AddComponent<Image>();
            CooldownOverlay.color = new Color(0f, 0f, 0f, 0.7f);
            CooldownOverlay.type = Image.Type.Filled;
            CooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            CooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
            CooldownOverlay.fillClockwise = false;
            CooldownOverlay.fillAmount = 0f;

            // Countdown text
            var textGO = new GameObject("CooldownText");
            textGO.transform.SetParent(WidgetRoot.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(64f, 64f);
            CooldownText = textGO.AddComponent<Text>();
            CooldownText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            CooldownText.fontSize = 16;
            CooldownText.fontStyle = FontStyle.Bold;
            CooldownText.color = Color.white;
            CooldownText.alignment = TextAnchor.MiddleCenter;
            CooldownText.text = "";

            WidgetRoot.SetActive(false);
            Plugin.Log.LogInfo("SkillSlotWidget built.");
        }
    }

    [HarmonyPatch(typeof(Hud), "Update")]
    public static class SkillSlotWidget_Update
    {
        static void Postfix()
        {
            var player = Player.m_localPlayer;
            var root = SkillSlotWidget_Awake.WidgetRoot;

            if (player == null || root == null) return;

            if (root.TryGetComponent<RectTransform>(out var rt))
                rt.anchoredPosition = new Vector2(Plugin.SkillWidgetX.Value, Plugin.SkillWidgetY.Value);

            var weapon = player.GetCurrentWeapon();
            var skill = Skills.WeaponSkillManager.GetSkillForWeapon(weapon);

            root.SetActive(skill != null);
            if (skill == null) return;

            // Update icon — full color when ready, desaturated when on cooldown
            if (SkillSlotWidget_Awake.SkillIcon != null)
            {
                var icon = Skills.WeaponSkillManager.GetSkillIcon(weapon);
                bool onCooldown = skill.IsOnCooldown(player);

                SkillSlotWidget_Awake.SkillIcon.sprite = icon;
                SkillSlotWidget_Awake.SkillIcon.color = icon == null
                    ? new Color(1f, 1f, 1f, 0.3f)
                    : onCooldown
                        ? new Color(0.35f, 0.35f, 0.35f, 0.85f)
                        : Color.white;
            }

            // Look up this skill's specific cooldown SE by name
            var se = SE_SkillCooldown.GetActive(player, skill.CooldownSEName);

            if (se != null)
            {
                float remaining = se.GetRemainingTime();
                float fraction = se.m_ttl > 0f ? remaining / se.m_ttl : 0f;

                if (SkillSlotWidget_Awake.CooldownOverlay != null)
                {
                    SkillSlotWidget_Awake.CooldownOverlay.gameObject.SetActive(true);
                    SkillSlotWidget_Awake.CooldownOverlay.fillAmount = Mathf.Clamp01(fraction);
                }

                if (SkillSlotWidget_Awake.CooldownText != null)
                    SkillSlotWidget_Awake.CooldownText.text = remaining > 0.5f
                        ? $"{Mathf.CeilToInt(remaining)}s"
                        : "";
            }
            else
            {
                if (SkillSlotWidget_Awake.CooldownOverlay != null)
                {
                    SkillSlotWidget_Awake.CooldownOverlay.gameObject.SetActive(false);
                    SkillSlotWidget_Awake.CooldownOverlay.fillAmount = 0f;
                }

                if (SkillSlotWidget_Awake.CooldownText != null)
                    SkillSlotWidget_Awake.CooldownText.text = "";
            }
        }
    }
}