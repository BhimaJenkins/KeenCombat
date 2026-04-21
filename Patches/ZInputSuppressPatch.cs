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

        // True while Right Trigger is held so JoyAttack can't fire vanilla
        // heavy attack on top of our skill.
        public static bool SuppressJoyAttack = false;

        // True for one frame after skill fires to suppress SecondaryAttack
        // so vanilla doesn't fire its own heavy attack on the same press.
        public static bool SkillJustFired = false;
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

            if (AttackInputState.SkillJustFired &&
               (name == "SecondaryAttack" || name == "JoySecondaryAttack"))
            {
                __result = false;
                return;
            }

            if (AttackInputState.SuppressJoyAttack && name == "JoyAttack")
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
                __result = false;
        }
    }
}