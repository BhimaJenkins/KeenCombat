using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // MagicCostScaler
    //
    // Scales staff skill costs (Eitr and Blood Magic HP) based on the caster's
    // level in the staff's own magic skill (Elemental Magic or Blood Magic).
    //
    // Default config values are balanced for skill level 100. Below that,
    // costs increase by PenaltyPerStep for every LevelsPerStep levels missing,
    // rounded up. With defaults (1% per 5 levels):
    //   Skill 0-1  → +20%
    //   Skill 50   → +10%
    //   Skill 96-99 → +1%
    //   Skill 100  → no increase
    // -----------------------------------------------------------------------
    public static class MagicCostScaler
    {
        /// <summary>
        /// Returns the cost multiplier for this player and weapon (1.0 = no change).
        /// </summary>
        public static float GetMultiplier(Player player, ItemDrop.ItemData weapon)
        {
            if (!Plugin.MagicCostScalingEnabled.Value) return 1f;
            if (player == null || weapon == null) return 1f;

            float level = player.GetSkills().GetSkillLevel(weapon.m_shared.m_skillType);

            float levelsPerStep = Mathf.Max(1f, Plugin.MagicCostLevelsPerStep.Value);
            float missing = Mathf.Clamp(100f - level, 0f, 100f);
            int steps = Mathf.CeilToInt(missing / levelsPerStep);

            return 1f + steps * Plugin.MagicCostPenaltyPerStep.Value;
        }

        /// <summary>
        /// Returns baseCost scaled by the player's skill level.
        /// </summary>
        public static float Scale(Player player, ItemDrop.ItemData weapon, float baseCost)
        {
            return baseCost * GetMultiplier(player, weapon);
        }
    }
}