using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Patches
{
    [HarmonyPatch(typeof(Player), "Update")]
    public static class AttackInputPatch
    {
        // Separate state for mouse and controller to prevent cross-blocking
        private static float _mousePressTime = 0f;
        private static bool _mouseHeavyFired = false;
        private static float _ctrlPressTime = 0f;
        private static bool _ctrlHeavyFired = false;

        private static float _skillPressTime = 0f;
        private static bool _skillFired = false;
        private static bool _rtWasPressed = false;

        static void Prefix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;

            AttackInputState.SkillJustFired = false;

            if (__instance.IsDead()) return;
            if (__instance.InMinorAction()) return;
            if (__instance.IsEncumbered()) return;
            if (InventoryGui.IsVisible()) return;
            if (__instance.InPlaceMode()) return;
            if (Hud.IsPieceSelectionVisible()) return;

            var currentWeapon = __instance.GetCurrentWeapon();
            if (currentWeapon == null) return;
            if (currentWeapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool) return;
            if (currentWeapon.m_shared.m_attack.m_attackAnimation.StartsWith("swing_pickaxe")) return;
            if (currentWeapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) return;

            // ---------------------------------------------------------------
            // Mouse hold-to-heavy — independent state
            // ---------------------------------------------------------------
            bool mouseHeld = Input.GetMouseButton(0);
            bool mouseUp = Input.GetMouseButtonUp(0);
            bool mouseDown = Input.GetMouseButtonDown(0);

            if (mouseDown)
            {
                _mousePressTime = Time.time;
                _mouseHeavyFired = false;
                AttackInputState.HoldingForHeavy = false;
            }

            if (mouseHeld && !_mouseHeavyFired && _mousePressTime > 0f)
            {
                float held = Time.time - _mousePressTime;
                if (held >= Plugin.HoldThreshold.Value)
                {
                    _mouseHeavyFired = true;
                    AttackInputState.HoldingForHeavy = true;
                    FireHeavy(__instance);
                }
            }

            if (mouseUp)
            {
                _mousePressTime = 0f;
                _mouseHeavyFired = false;
                if (!_ctrlHeavyFired)
                    AttackInputState.HoldingForHeavy = false;
            }

            // ---------------------------------------------------------------
            // Controller hold-to-heavy — independent state
            // ---------------------------------------------------------------
            bool ctrlHeld = Input.GetKey(KeyCode.JoystickButton5);
            bool ctrlUp = Input.GetKeyUp(KeyCode.JoystickButton5);
            bool ctrlDown = Input.GetKeyDown(KeyCode.JoystickButton5);

            if (ctrlDown)
            {
                _ctrlPressTime = Time.time;
                _ctrlHeavyFired = false;
                AttackInputState.HoldingForHeavy = false;
            }

            if (ctrlHeld && !_ctrlHeavyFired && _ctrlPressTime > 0f)
            {
                float held = Time.time - _ctrlPressTime;
                if (held >= Plugin.HoldThreshold.Value)
                {
                    _ctrlHeavyFired = true;
                    AttackInputState.HoldingForHeavy = true;
                    FireHeavy(__instance);
                }
            }

            if (ctrlUp)
            {
                _ctrlPressTime = 0f;
                _ctrlHeavyFired = false;
                if (!_mouseHeavyFired)
                    AttackInputState.HoldingForHeavy = false;
            }
        }

        private static void FireHeavy(Player player)
        {
            if (player.m_currentAttack != null)
            {
                player.m_currentAttack.Stop();
                player.m_previousAttack = player.m_currentAttack;
                player.m_currentAttack = null;
            }
            player.ClearActionQueue();
            AttackInputState.ForceNotInAttack = true;
            player.StartAttack(null, true);
            AttackInputState.ForceNotInAttack = false;
        }

        static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;

            AttackInputState.ForceNotInAttack = false;

            var currentWeapon = __instance.GetCurrentWeapon();
            if (currentWeapon == null) return;
            if (currentWeapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool) return;
            if (currentWeapon.m_shared.m_attack.m_attackAnimation.StartsWith("swing_pickaxe")) return;
            if (InventoryGui.IsVisible()) return;
            if (__instance.InPlaceMode()) return;
            if (Hud.IsPieceSelectionVisible()) return;

            var skill = Skills.WeaponSkillManager.GetSkillForWeapon(currentWeapon);
            if (skill == null) return;

            bool mouseSkillDown = Input.GetKeyDown(Plugin.SkillKey.Value);
            bool mouseSkillHeld = Input.GetKey(Plugin.SkillKey.Value);
            bool mouseSkillUp = Input.GetKeyUp(Plugin.SkillKey.Value);

            float rtValue = Plugin.RightTriggerAction?.ReadValue<float>() ?? 0f;
            bool rtHeld = rtValue > 0.5f;
            bool rtDown = rtHeld && !_rtWasPressed;
            bool rtUp = !rtHeld && _rtWasPressed;
            _rtWasPressed = rtHeld;

            AttackInputState.SuppressJoyAttack = rtHeld;

            bool skillDown = mouseSkillDown || rtDown;
            bool skillHeld = mouseSkillHeld || rtHeld;
            bool skillUp = mouseSkillUp || rtUp;

            if (skillDown)
            {
                _skillPressTime = Time.time;
                _skillFired = false;

                if (!skill.IsOnCooldown(__instance))
                {
                    AttackInputState.SkillJustFired = true;
                    _skillFired = true;
                    skill.OnPress(__instance, currentWeapon);
                }
            }

            if (skillHeld && _skillFired)
                skill.OnHold(__instance, currentWeapon, Time.time - _skillPressTime);

            if (skillUp && _skillFired)
            {
                skill.OnRelease(__instance, currentWeapon, Time.time - _skillPressTime);
                _skillFired = false;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Weapon Swap Buff Cleanup
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    public static class Humanoid_EquipItem_BuffCleanup_Patch
    {
        static void Postfix(Humanoid __instance)
        {
            if (__instance is not Player player) return;

            var weapon = player.GetCurrentWeapon();

            RemoveIfWrongWeapon(player, weapon,
                HUD.SE_MaceSkillBuff.StatusEffectName,
                "SE_Cooldown_Mace",
                Plugin.MaceSkillCooldown.Value,
                global::Skills.SkillType.Clubs);

            RemoveIfWrongWeapon(player, weapon,
                HUD.SE_AxeSkillBuff.StatusEffectName,
                "SE_Cooldown_Axe",
                Plugin.AxeSkillCooldown.Value,
                global::Skills.SkillType.Axes);
        }

        private static void RemoveIfWrongWeapon(Player player,
                                                 ItemDrop.ItemData? weapon,
                                                 string buffSEName,
                                                 string cooldownSEName,
                                                 float cooldownDuration,
                                                 global::Skills.SkillType expectedType)
        {
            var seman = player.GetSEMan();
            var buff = seman.GetStatusEffect(buffSEName.GetStableHashCode());
            if (buff == null) return;

            if (weapon == null || weapon.m_shared.m_skillType != expectedType)
            {
                seman.RemoveStatusEffect(buff);
                HUD.SE_SkillCooldown.Apply(player, cooldownDuration, cooldownSEName);
                Plugin.Log.LogInfo($"Buff '{buffSEName}' removed — weapon swapped. Cooldown started.");
            }
        }
    }
}