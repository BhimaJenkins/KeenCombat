using System.Reflection;
using HarmonyLib;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // SkillFeedback
    //
    // Vanilla-style feedback when a skill can't be cast.
    //
    // NotEnoughEitr() triggers the same HUD Eitr bar red flash Valheim shows
    // when you try to cast a vanilla spell without enough Eitr. The HUD
    // method is looked up by name so a future Valheim rename won't break the
    // build — the flash would just stop happening.
    // -----------------------------------------------------------------------
    public static class SkillFeedback
    {
        private static readonly MethodInfo? _eitrFlash =
            AccessTools.Method(typeof(Hud), "EitrBarEmptyFlash");

        private static bool _warned = false;

        public static void NotEnoughEitr()
        {
            if (Hud.instance == null) return;

            if (_eitrFlash != null)
            {
                _eitrFlash.Invoke(Hud.instance, null);
            }
            else if (!_warned)
            {
                _warned = true;
                KC_Log.Warn("SkillFeedback: Hud.EitrBarEmptyFlash not found — no Eitr flash.");
            }
        }
    }
}