using HarmonyLib;
using KeenCombat;
using UnityEngine;

namespace KeenCombat.Patches
{
    public static class AttackInputState
    {
        // Forces Player.InAttack() to return false so we can cancel into heavy.
        public static bool ForceNotInAttack = false;

        // True only while player has committed to a heavy attack hold.
        // Suppresses GetButton("Attack") to stop vanilla auto-chaining
        // during the heavy attack, but leaves it alone during normal tapping.
        public static bool HoldingForHeavy = false;

        // Legacy flag — still set by AttackInputPatch but no longer read.
        // RT suppression now uses the live RightTriggerReclaimed check below.
        public static bool SuppressJoyAttack = false;

        // True for one frame after skill fires.
        public static bool SkillJustFired = false;

        // -------------------------------------------------------------------
        // Live check: is RT physically pressed right now AND does the current
        // weapon have a KeenCombat skill? Read at the exact moment vanilla
        // asks about the button, so there's no one-frame gap. Weapons without
        // a skill (tools, pickaxes, Spirit Caller, Red Troll) keep vanilla RT.
        // -------------------------------------------------------------------
        public static bool RightTriggerReclaimed
        {
            get
            {
                float rt = Plugin.RightTriggerAction?.ReadValue<float>() ?? 0f;
                if (rt <= 0.5f) return false;

                var player = Player.m_localPlayer;
                if (player == null) return false;

                var weapon = player.GetCurrentWeapon();
                if (weapon == null) return false;
                if (weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool) return false;
                if (weapon.m_shared.m_attack.m_attackAnimation.StartsWith("swing_pickaxe")) return false;

                return Skills.WeaponSkillManager.GetSkillForWeapon(weapon) != null;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "InAttack")]
    public static class InAttack_Override
    {
        static bool Prefix(Player __instance, ref bool __result)
        {
            if (AttackInputState.ForceNotInAttack)
            {
                __instance.m_cachedAttack = false;
                __instance.m_cachedFrame = -1;
                __result = false;
                return false;
            }
            return true;
        }
    }

    // -----------------------------------------------------------------------
    // Using Postfix + Priority.First because in Harmony, postfixes run in
    // REVERSE priority order — Priority.First on a Postfix means it runs
    // LAST among all postfixes, after every other mod including those using
    // Priority.Last. This guarantees we are the final word on __result
    // regardless of what any other mod (Config Manager, Extra Slots, etc.)
    // sets it to.
    //
    // RT reaches vanilla as "JoyAttack", so while RT is reclaimed for a
    // skill, JoyAttack is blocked in all three checks (held, down, up).
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButton))]
    [HarmonyPriority(Priority.First)]
    public static class ZInput_SuppressGetButton
    {
        static void Postfix(string name, ref bool __result)
        {
            if (name == "SecondaryAttack" || name == "JoySecondaryAttack")
            {
                __result = false;
                return;
            }

            if ((name == "Attack" || name == "JoyAttack") && AttackInputState.HoldingForHeavy)
            {
                __result = false;
                return;
            }

            if (name == "JoyAttack" && AttackInputState.RightTriggerReclaimed)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
    [HarmonyPriority(Priority.First)]
    public static class ZInput_SuppressGetButtonDown
    {
        static void Postfix(string name, ref bool __result)
        {
            if (name == "SecondaryAttack" || name == "JoySecondaryAttack")
            {
                __result = false;
                return;
            }

            if (name == "JoyAttack" && AttackInputState.RightTriggerReclaimed)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonUp))]
    [HarmonyPriority(Priority.First)]
    public static class ZInput_SuppressGetButtonUp
    {
        static void Postfix(string name, ref bool __result)
        {
            if (name == "SecondaryAttack" || name == "JoySecondaryAttack")
            {
                __result = false;
                return;
            }

            if (name == "JoyAttack" && AttackInputState.RightTriggerReclaimed)
                __result = false;
        }
    }
}