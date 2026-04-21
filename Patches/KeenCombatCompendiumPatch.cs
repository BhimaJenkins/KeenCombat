using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace KeenCombat.Patches
{
    // -----------------------------------------------------------------------
    // KeenCombatCompendiumPatch
    //
    // Adds a "Keen Combat" tab to the Valheim compendium (Texts dialog)
    // listing all weapon skills and their descriptions.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.AddActiveEffects))]
    public static class KeenCombatCompendium_Patch
    {
        private const string Header = "<color=orange>=== KEEN COMBAT ===</color>\n\n" +
            "<color=yellow>Each weapon type has a unique skill activated by pressing\n" +
            "the middle mouse button or right trigger (controller).\n" +
            "Cooldowns and damage values are configurable in the mod settings.\n</color>\n";

        private static readonly string SkillText =
            "<color=white><b>⚔ SWORD — Blink Strike</b></color>\n" +
            "Dash forward in the direction the camera is facing, striking all enemies in your path.\n\n" +

            "<color=white><b>🔨 MACE — Bulwark</b></color>\n" +
            "Flex and fortify yourself, reducing incoming damage and regenerating health for a short time.\n\n" +

            "<color=white><b>🪓 AXE / DUAL AXE — Frenzy</b></color>\n" +
            "Let out a battle roar, temporarily boosting your attack speed and movement speed.\n\n" +

            "<color=white><b>🔱 ATGEIR / SPEAR — Falcon Blitz</b></color>\n" +
            "Unleash a rapid six-hit flurry, locking yourself in place while striking everything in range.\n\n" +

            "<color=white><b>🏹 CROSSBOW — Rapid Fire</b></color>\n" +
            "Fire six bolts in quick succession in the direction you are aiming, consuming no ammunition.\n\n" +

            "<color=white><b>🗡 KNIFE — Assassination</b></color>\n" +
            "Vanish into the shadows. Enemies lose track of you. Press the skill button again to unleash a devastating backstrike.\n\n" +

            "<color=white><b>⚔⚔ 2H SWORD / 2H AXE — Whirlwind</b></color>\n" +
            "Spin twice in a devastating double sweep, carving through all enemies around you while moving forward.\n\n" +

            "<color=white><b>🏹 BOW — Primal Rally</b></color>\n" +
            "Call a wild beast to fight by your side. The creature summoned depends on your bow. " +
            "The beast follows you, attacks your enemies, and despawns after one minute.\n\n" +

            "<color=white><b>🗡🗡 DUAL KNIFE — Poison Mayhem</b></color>\n" +
            "Leap backward and hurl a cluster of poison bombs at your original position, " +
            "dealing direct and poison damage to all nearby enemies.\n\n" +

            "<color=white><b>👊 FIST — Onslaught</b></color>\n" +
            "Launch into a ferocious forward combo, finishing with a powerful lunge that damages all enemies in a long line ahead.\n\n" +

            "<color=white><b>🔨🔨 2H MACE — Earthquake</b></color>\n" +
            "Slam the ground and send four shockwaves rippling forward in sequence, " +
            "dealing heavy damage to everything in their path.\n\n" +

            "<color=orange>Primal Rally — Bow Creature Guide:</color>\n" +
            "The creature summoned by Primal Rally is determined by your bow.\n" +
            "Defaults: Crude Bow→Boar, Finewood→2★ Boar, Huntsman→Wolf,\n" +
            "Draugr Fang→BlobElite, Spinesnap→Seeker, Ashlands bows→Asksvin.\n" +
            "Custom mappings can be added in the mod configuration manager.";

        static void Postfix(TextsDialog __instance)
        {
            __instance.m_texts.Add(new TextsDialog.TextInfo(
                "Keen Combat Skills",
                Header + SkillText));
        }
    }
}