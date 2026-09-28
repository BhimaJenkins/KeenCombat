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

        // Throttled weapon info log
        private static float _lastStaffLog = 0f;

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
            if (Minimap.IsOpen()) return;
            if (Chat.instance != null && Chat.instance.HasFocus()) return;
            if (Console.IsVisible()) return;

            var currentWeapon = __instance.GetCurrentWeapon();
            if (currentWeapon == null) return;

            // Throttled weapon info log — equip a weapon to see its details
            if (Time.time - _lastStaffLog > 2f)
            {
                _lastStaffLog = Time.time;
                KC_Log.Debug($"Weapon: {currentWeapon.m_shared.m_name} " +
                             $"anim: {currentWeapon.m_shared.m_attack.m_attackAnimation} " +
                             $"type: {currentWeapon.m_shared.m_itemType} " +
                             $"skill: {currentWeapon.m_shared.m_skillType}");
            }

            if (currentWeapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool) return;
            if (currentWeapon.m_shared.m_attack.m_attackAnimation.StartsWith("swing_pickaxe")) return;
            if (currentWeapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) return;

            // Skip hold-to-heavy for staves — they use normal attack for their abilities
            if (currentWeapon.m_shared.m_attack.m_attackAnimation.StartsWith("staff_")) return;

            float holdThreshold = GetHoldThreshold(currentWeapon);

            // ---------------------------------------------------------------
            // Mouse hold-to-heavy — independent state.
            // Skipped when mouse & keyboard players chose Valheim's own
            // heavy attack button (KeyboardHeavyAttack = Vanilla).
            // ---------------------------------------------------------------
            if (!AttackInputState.KeyboardVanillaHeavy)
            {
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
                    if (held >= holdThreshold)
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
            }
            else if (_mouseHeavyFired || _mousePressTime > 0f)
            {
                // Setting switched to Vanilla mid-hold — clear mouse state
                _mousePressTime = 0f;
                _mouseHeavyFired = false;
                if (!_ctrlHeavyFired)
                    AttackInputState.HoldingForHeavy = false;
            }

            // ---------------------------------------------------------------
            // Controller hold-to-heavy — independent state (always active)
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
                if (held >= holdThreshold)
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

        // -------------------------------------------------------------------
        // Per-weapon-group hold time (Hold To Heavy Timing config section)
        // -------------------------------------------------------------------
        private static float GetHoldThreshold(ItemDrop.ItemData weapon)
        {
            var shared = weapon.m_shared;
            var skillType = shared.m_skillType;
            string anim = shared.m_attack.m_attackAnimation;

            // Two-handed maces/sledges first — their combo uses the greatsword animation
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon &&
                skillType == global::Skills.SkillType.Clubs)
                return Plugin.HoldTwoHandedMace.Value;

            // 2H swords and 2H axes
            if (anim.StartsWith("greatsword") || anim.StartsWith("battleaxe"))
                return Plugin.HoldGreatsword.Value;

            switch (skillType)
            {
                case global::Skills.SkillType.Axes: return Plugin.HoldAxe.Value;     // axes + dual axes
                case global::Skills.SkillType.Clubs: return Plugin.HoldMace.Value;
                case global::Skills.SkillType.Swords: return Plugin.HoldSword.Value;
                case global::Skills.SkillType.Spears: return Plugin.HoldSpear.Value;
                case global::Skills.SkillType.Polearms: return Plugin.HoldAtgeir.Value;
                case global::Skills.SkillType.Knives: return Plugin.HoldKnife.Value;   // knives + dual knives
                default: return Plugin.HoldThreshold.Value;
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

            if (InventoryGui.IsVisible()) return;
            if (__instance.InPlaceMode()) return;
            if (Hud.IsPieceSelectionVisible()) return;
            if (Minimap.IsOpen()) return;
            if (Chat.instance != null && Chat.instance.HasFocus()) return;
            if (Console.IsVisible()) return;

            var currentWeapon = __instance.GetCurrentWeapon();
            if (currentWeapon == null) return;
            if (currentWeapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool) return;
            if (currentWeapon.m_shared.m_attack.m_attackAnimation.StartsWith("swing_pickaxe")) return;

            var skill = Skills.WeaponSkillManager.GetSkillForWeapon(currentWeapon);
            if (skill == null) return;

            // Rebindable mouse & keyboard skill key
            var skillKey = Plugin.SkillKeybind.Value;
            bool mouseSkillDown = skillKey.IsDown();
            bool mouseSkillHeld = skillKey.IsPressed();
            bool mouseSkillUp = skillKey.IsUp();

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
            }
        }
    }
}