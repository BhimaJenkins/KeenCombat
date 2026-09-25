using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using KeenCombat.Skills;

namespace KeenCombat.Patches
{
    // -----------------------------------------------------------------------
    // WeaponTooltipPatch
    //
    // Appends the equipped weapon's KeenCombat skill info (name, description,
    // damage, costs, duration, cooldown) to the bottom of the vanilla item
    // tooltip. Because it's appended to the same text, it uses Valheim's own
    // tooltip font and scrolls with the rest of the tooltip.
    //
    // Patches every static ItemDrop.ItemData.GetTooltip overload that returns
    // a string, so it survives Valheim changing the method's parameters.
    // -----------------------------------------------------------------------
    [HarmonyPatch]
    public static class WeaponTooltipPatch
    {
        // Invisible TMP tag used to avoid appending twice if one GetTooltip
        // overload calls another
        private const string Marker = "<link=\"kc_skill\"></link>";

        private const string ValueColor = "orange";
        private const string DescColor = "#C8C8C8";

        static IEnumerable<MethodBase> TargetMethods()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var m in typeof(ItemDrop.ItemData).GetMethods(flags))
            {
                if (m.Name != "GetTooltip" || m.ReturnType != typeof(string)) continue;
                var ps = m.GetParameters();
                if (ps.Length > 0 && ps[0].ParameterType == typeof(ItemDrop.ItemData))
                    yield return m;
            }
        }

        static void Postfix(ref string __result, object[] __args)
        {
            if (string.IsNullOrEmpty(__result) || __result.Contains(Marker)) return;

            // Pull the item and quality level out of the arguments
            ItemDrop.ItemData? item = null;
            int quality = -1;
            foreach (var arg in __args)
            {
                if (item == null && arg is ItemDrop.ItemData it) item = it;
                else if (quality < 0 && arg is int q) quality = q;
            }
            if (item == null) return;
            if (quality < 1) quality = item.m_quality;

            var skill = WeaponSkillManager.GetSkillForWeapon(item);
            if (skill == null) return;

            var sb = new StringBuilder();
            sb.Append("\n\n").Append(Marker);
            sb.Append($"<size=115%><color={ValueColor}><b>{skill.SkillName.ToUpperInvariant()}</b></color></size>\n");
            sb.Append($"<color={DescColor}><i>{skill.Description}</i></color>\n");

            AppendSkillStats(sb, skill, item, quality);

            if (skill.Cooldown > 0f)
                Line(sb, "Cooldown", $"{skill.Cooldown:0}s");

            __result += sb.ToString().TrimEnd('\n');
        }

        // -------------------------------------------------------------------
        // Per-skill stat lines
        // -------------------------------------------------------------------
        private static void AppendSkillStats(StringBuilder sb, IWeaponSkill skill,
                                             ItemDrop.ItemData item, int quality)
        {
            var player = Player.m_localPlayer;

            switch (skill)
            {
                // ---------------- Staves ----------------
                case StaffFireballSkill _:
                    {
                        var d = new HitData.DamageTypes();
                        d.m_fire = item.GetDamage().m_fire * Plugin.StaffFireballDamageMultiplier.Value;
                        sb.Append("<b>Per meteor</b>\n");
                        AppendDamage(sb, d);
                        Line(sb, "Duration", $"{Plugin.StaffFireballDuration.Value:0}s");
                        Line(sb, "Use eitr", Eitr(player, item, Plugin.StaffFireballEitrCost.Value));
                        break;
                    }

                case StaffIceShardsSkill _:
                    {
                        var d = new HitData.DamageTypes();
                        d.m_frost = item.GetDamage().m_frost * Plugin.StaffIceShardsDamageMultiplier.Value;
                        AppendDamage(sb, d);
                        Line(sb, "Freeze", $"{Plugin.StaffIceShardsDuration.Value:0}s");
                        Line(sb, "Radius", $"{Plugin.StaffIceShardsRadius.Value:0}m");
                        Line(sb, "Use eitr", Eitr(player, item, Plugin.StaffIceShardsEitrCost.Value));
                        break;
                    }

                case StaffClusterbombSkill _:
                    {
                        // Keep in sync with the constants in StaffClusterbombSkill.cs
                        const float dmgMult = 1.3f, minEitr = 35f, maxEitr = 60f;

                        var d = item.GetDamage();
                        d.Modify(dmgMult);
                        sb.Append("<b>Per shot</b>\n");
                        AppendDamage(sb, d);
                        Line(sb, "Shots", "1 - 3 (hold to charge)");
                        float mult = player != null ? MagicCostScaler.GetMultiplier(player, item) : 1f;
                        Line(sb, "Use eitr", $"{minEitr * mult:0} - {maxEitr * mult:0}");
                        break;
                    }

                case StaffGreenRootsSkill _:
                    {
                        Line(sb, "Heal", $"{Plugin.StaffGreenRootsHealPercent.Value * 100f:0}% max HP " +
                                         $"over {Plugin.StaffGreenRootsDuration.Value:0}s");
                        Line(sb, "Radius", $"{Plugin.StaffGreenRootsRadius.Value:0}m");
                        Line(sb, "Use eitr", Eitr(player, item, Plugin.StaffGreenRootsEitrCost.Value));
                        break;
                    }

                case StaffSkeletonSkill _:
                    {
                        var (eitr, hpPct, count) = GetSkeletonTier(quality);
                        float mult = player != null ? MagicCostScaler.GetMultiplier(player, item) : 1f;
                        Line(sb, "Summons", $"{count} Charred Archer{(count > 1 ? "s" : "")}");
                        Line(sb, "Duration", $"{Plugin.StaffSkeletonDuration.Value / 60f:0} min");
                        Line(sb, "Use eitr", $"{eitr * mult:0}");
                        Line(sb, "Use health", $"{hpPct * mult * 100f:0}% max HP");
                        break;
                    }

                case StaffThunderbloodSkill _:
                    {
                        Line(sb, "Lightning per strike", $"{StaffThunderbloodSkill.GetStrikeDamage(item):0}");
                        Line(sb, "Strikes every", $"{Plugin.StaffThunderbloodIntervalMin.Value:0} - " +
                                                  $"{Plugin.StaffThunderbloodIntervalMax.Value:0}s per enemy");
                        Line(sb, "Radius", $"{Plugin.StaffThunderbloodRadius.Value:0}m");
                        Line(sb, "Duration", $"{Plugin.StaffThunderbloodDuration.Value / 60f:0} min");
                        Line(sb, "Use eitr", Eitr(player, item, Plugin.StaffThunderbloodEitrCost.Value));
                        break;
                    }

                case StaffShieldSkill _:
                    {
                        float mult = player != null ? MagicCostScaler.GetMultiplier(player, item) : 1f;
                        Line(sb, "Slow", $"{Plugin.StaffShieldSlowPercent.Value * 100f:0}% enemies & projectiles");
                        Line(sb, "Radius", $"{Plugin.StaffShieldRadius.Value:0}m");
                        Line(sb, "Duration", $"{Plugin.StaffShieldDuration.Value:0}s");
                        Line(sb, "Use eitr", $"{Plugin.StaffShieldEitrCost.Value * mult:0}");
                        Line(sb, "Use health", $"{Plugin.StaffShieldHealthPercent.Value * mult * 100f:0}% max HP");
                        break;
                    }

                // ---------------- Melee & ranged ----------------
                case SwordSkill _:
                    AppendScaledDamage(sb, item, Plugin.SwordSkillDamage.Value);
                    break;

                case MaceSkill _:
                    Line(sb, "Damage reduction", $"{Plugin.MaceDamageReduction.Value * 100f:0}%");
                    Line(sb, "Regen", $"{Plugin.MaceRegenPerSec.Value:0} HP/s");
                    Line(sb, "Duration", $"{Plugin.MaceBuffDuration.Value:0}s");
                    break;

                case AxeSkill _:
                    Line(sb, "Attack speed", $"+{Plugin.AxeAttackSpeedBonus.Value * 100f:0}%");
                    Line(sb, "Move speed", $"+{Plugin.AxeMoveSpeedBonus.Value * 100f:0}%");
                    Line(sb, "Duration", $"{Plugin.AxeBuffDuration.Value:0}s");
                    break;

                case AtgeirSkill _:
                    sb.Append("<b>Per hit</b>\n");
                    AppendScaledDamage(sb, item, Plugin.AtgeirSkillDamage.Value);
                    break;

                case CrossbowSkill _:
                    sb.Append("<b>Per bolt</b>\n");
                    AppendScaledDamage(sb, item, Plugin.CrossbowSkillDamage.Value);
                    break;

                case KnifeSkill _:
                    AppendScaledDamage(sb, item, Plugin.KnifeSkillDamage.Value);
                    Line(sb, "Stealth", $"{Plugin.KnifeStealthDuration.Value:0}s");
                    break;

                case GreatswordSkill _:
                    sb.Append("<b>Per spin hit</b>\n");
                    AppendScaledDamage(sb, item, Plugin.GreatswordSkillDamage.Value);
                    break;

                case BowSkill _:
                    Line(sb, "Summons", GetBowCreature(item));
                    Line(sb, "Duration", $"{Plugin.BowSummonDuration.Value:0}s");
                    break;

                case DualKnifeSkill _:
                    sb.Append("<b>Per bomb</b>\n");
                    AppendScaledDamage(sb, item, Plugin.DualKnifeSkillDamage.Value);
                    break;

                case FistSkill _:
                    sb.Append("<b>Finisher</b>\n");
                    AppendScaledDamage(sb, item, Plugin.FistSkillDamage.Value);
                    break;

                case TwoHandedMaceSkill _:
                    sb.Append("<b>Per explosion</b>\n");
                    AppendScaledDamage(sb, item, Plugin.TwoHandedMaceSkillDamage.Value);
                    break;
            }
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        private static void Line(StringBuilder sb, string label, string value)
        {
            sb.Append($"{label}: <color={ValueColor}>{value}</color>\n");
        }

        private static string Eitr(Player? player, ItemDrop.ItemData item, float baseCost)
        {
            float cost = player != null ? MagicCostScaler.Scale(player, item, baseCost) : baseCost;
            return $"{cost:0}";
        }

        private static void AppendScaledDamage(StringBuilder sb, ItemDrop.ItemData item, float mult)
        {
            var d = item.GetDamage();
            d.Modify(mult);
            AppendDamage(sb, d);
        }

        private static void AppendDamage(StringBuilder sb, HitData.DamageTypes d)
        {
            Dmg(sb, "Blunt", d.m_blunt);
            Dmg(sb, "Slash", d.m_slash);
            Dmg(sb, "Pierce", d.m_pierce);
            Dmg(sb, "Fire", d.m_fire);
            Dmg(sb, "Frost", d.m_frost);
            Dmg(sb, "Lightning", d.m_lightning);
            Dmg(sb, "Poison", d.m_poison);
            Dmg(sb, "Spirit", d.m_spirit);
        }

        private static void Dmg(StringBuilder sb, string label, float value)
        {
            if (value < 0.5f) return;
            Line(sb, label, $"{Mathf.RoundToInt(value)}");
        }

        // Mirrors the LevelTiers parsing in StaffSkeletonSkill.cs
        private static (float eitr, float hpPct, int count) GetSkeletonTier(int quality)
        {
            var tiers = Plugin.StaffSkeletonLevelTiers.Value.Split(',');
            int index = Mathf.Clamp(quality - 1, 0, tiers.Length - 1);
            var parts = tiers[index].Trim().Split(':');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var style = System.Globalization.NumberStyles.Float;

            float e = parts.Length > 0 && float.TryParse(parts[0].Trim(), style, inv, out float ev) ? ev : 80f;
            float h = parts.Length > 1 && float.TryParse(parts[1].Trim(), style, inv, out float hv) ? hv : 0.30f;
            int c = parts.Length > 2 && int.TryParse(parts[2].Trim(), out int cv) ? cv : 1;
            return (e, h, Mathf.Max(1, c));
        }

        // Looks up the creature name from BowCreatureMap
        private static string GetBowCreature(ItemDrop.ItemData item)
        {
            string bowName = item.m_shared.m_name.ToLowerInvariant();
            foreach (var entry in Plugin.BowCreatureMap.Value.Split(','))
            {
                var parts = entry.Trim().Split(':');
                if (parts.Length < 2) continue;
                if (parts[0].Trim().ToLowerInvariant() == bowName)
                    return parts[1].Trim().Replace('_', ' ');
            }
            return "Neck";
        }
    }
}