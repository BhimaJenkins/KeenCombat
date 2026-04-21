using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Patches
{
    // -----------------------------------------------------------------------
    // TwoHandedMaceAnimPatch
    //
    // 1. Overrides normal attack animations on vanilla 2H maces to use the
    //    greatsword string (3-hit chain). Priority.Low runs after Warfare.
    //
    // 2. Scales sledge hit VFX opacity to 0 during normal attacks only.
    //    Full VFX preserved during heavy attack (sledge_secondary).
    // -----------------------------------------------------------------------

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    [HarmonyPriority(Priority.Low)]
    public static class TwoHandedMaceAnimOverride_Patch
    {
        static void Postfix(ObjectDB __instance)
        {
            // Try multiple greatsword prefabs — handles vanilla and modded installs
            var templatePrefab = __instance.GetItemPrefab("THSwordKrom")
                              ?? __instance.GetItemPrefab("THSwordSlayer")
                              ?? __instance.GetItemPrefab("THSwordWood");

            if (templatePrefab == null)
            {
                Plugin.Log.LogWarning("TwoHandedMaceAnimPatch: no greatsword template found!");
                return;
            }

            var templateDrop = templatePrefab.GetComponent<ItemDrop>();
            if (templateDrop == null) return;

            OverrideAnim(__instance, "SledgeStagbreaker", templateDrop);
            OverrideAnim(__instance, "SledgeIron", templateDrop);
            OverrideAnim(__instance, "SledgeDemolisher", templateDrop);
        }

        private static void OverrideAnim(ObjectDB db, string prefabName,
                                          ItemDrop template)
        {
            var prefab = db.GetItemPrefab(prefabName);
            if (prefab == null) return;

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null) return;

            var savedDamages = drop.m_itemData.m_shared.m_damages;
            var savedDamagesPerLevel = drop.m_itemData.m_shared.m_damagesPerLevel;

            var templateAttack = template.m_itemData.m_shared.m_attack;

            drop.m_itemData.m_shared.m_attack.m_attackType = templateAttack.m_attackType;
            drop.m_itemData.m_shared.m_attack.m_attackAnimation = "greatsword";
            drop.m_itemData.m_shared.m_attack.m_attackChainLevels = 3;
            drop.m_itemData.m_shared.m_hitEffect = new EffectList();
            drop.m_itemData.m_shared.m_attack.m_hitTerrain = templateAttack.m_hitTerrain;
            drop.m_itemData.m_shared.m_attack.m_attackRange = templateAttack.m_attackRange;
            drop.m_itemData.m_shared.m_attack.m_attackHeight = templateAttack.m_attackHeight;
            drop.m_itemData.m_shared.m_attack.m_attackAngle = templateAttack.m_attackAngle;
            drop.m_itemData.m_shared.m_attack.m_attackRayWidth = templateAttack.m_attackRayWidth;
            drop.m_itemData.m_shared.m_attack.m_maxYAngle = templateAttack.m_maxYAngle;
            drop.m_itemData.m_shared.m_attack.m_lowerDamagePerHit = templateAttack.m_lowerDamagePerHit;
            drop.m_itemData.m_shared.m_attack.m_hitTerrainEffect = new EffectList();

            drop.m_itemData.m_shared.m_secondaryAttack.m_attackAnimation = "sledge_secondary";

            drop.m_itemData.m_shared.m_damages = savedDamages;
            drop.m_itemData.m_shared.m_damagesPerLevel = savedDamagesPerLevel;
        }
    }

    // -----------------------------------------------------------------------
    // Set sledge hit VFX opacity to 0 during normal attacks only.
    // Heavy attack (sledge_secondary) plays at full opacity.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(EffectList), nameof(EffectList.Create))]
    public static class EffectList_ScaleSledgeVfx_Patch
    {
        private static readonly HashSet<string> _sledgeVfx = new HashSet<string>
        {
            "vfx_sledge_iron_hit",
            "vfx_sledge_hit",
            "fx_sledge_demolisher_hit"
        };

        static void Postfix(GameObject[] __result)
        {
            if (__result == null) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            var weapon = player.GetCurrentWeapon();
            if (weapon == null) return;
            if (weapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeapon) return;
            if (weapon.m_shared.m_skillType != global::Skills.SkillType.Clubs) return;

            // Don't scale during heavy attack — let it play at full size
            if (player.m_currentAttack != null &&
                player.m_currentAttack.m_attackAnimation == "sledge_secondary") return;

            foreach (var vfxGo in __result)
            {
                if (vfxGo == null) continue;
                if (_sledgeVfx.Contains(vfxGo.name.Replace("(Clone)", "").Trim()))
                {
                    foreach (var ps in vfxGo.GetComponentsInChildren<ParticleSystem>())
                    {
                        var main = ps.main;
                        var color = main.startColor.color;
                        color.a = 0f;
                        main.startColor = new ParticleSystem.MinMaxGradient(color);
                    }
                }
            }
        }
    }
}