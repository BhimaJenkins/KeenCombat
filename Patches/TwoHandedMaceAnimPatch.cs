using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Patches
{
    // -----------------------------------------------------------------------
    // TwoHandedMaceAnimPatch
    //
    // Converts the normal attack of EVERY two-handed club (any TwoHandedWeapon
    // with the Clubs skill — the same rule WeaponSkillManager uses) into the
    // greatsword 3-hit combo. Covers vanilla, newer and modded sledges.
    // Priority.Low runs after other mods like Warfare.
    //
    // Every effect list on the NORMAL attack is replaced with the greatsword
    // template's, removing the sledge's slam sound and ground-impact VFX.
    //
    // Heavy attack: an exact copy of the sledge's ORIGINAL primary attack —
    // the vanilla overhead slam, with its full area, range, width, effects
    // and shockwave.
    //
    // Swing sound:
    //   - SwingOgg set and file found → custom OGG broadcast at each swing
    //   - SwingOgg blank or file missing → the greatsword's own swing sounds
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    [HarmonyPriority(Priority.Low)]
    public static class TwoHandedMaceAnimOverride_Patch
    {
        public const string ComboAnim = "greatsword";

        /// <summary>
        /// True when SwingOgg is set AND the file was found. Otherwise the
        /// combo uses the greatsword's own swing sounds.
        /// </summary>
        public static bool UseSwingOgg { get; private set; }

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

            // Only use the custom swing sound if the file actually exists
            string ogg = Plugin.TwoHandedMaceSwingOgg.Value;
            UseSwingOgg = !string.IsNullOrEmpty(ogg) && Plugin.FindAudioFile(ogg) != null;
            if (!string.IsNullOrEmpty(ogg) && !UseSwingOgg)
                Plugin.Log.LogWarning($"TwoHandedMaceAnimPatch: swing sound '{ogg}' not found — " +
                                      "using greatsword swing sounds instead.");

            int converted = 0;
            foreach (var prefab in __instance.m_items)
            {
                var drop = prefab?.GetComponent<ItemDrop>();
                if (drop == null) continue;

                var shared = drop.m_itemData.m_shared;
                if (shared.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeapon) continue;
                if (shared.m_skillType != global::Skills.SkillType.Clubs) continue;

                OverrideAnim(drop, templateDrop);
                converted++;
                KC_Log.Debug($"TwoHandedMaceAnimPatch: converted {prefab!.name}");
            }

            Plugin.Log.LogInfo($"TwoHandedMaceAnimPatch: {converted} two-handed maces use the combo.");
        }

        // Effect lists that exist both on the weapon (SharedData) and on each
        // Attack, and that Valheim plays from both places
        private static readonly string[] SharedEffectNames =
        {
            "m_startEffect", "m_triggerEffect", "m_trailStartEffect",
            "m_hitEffect", "m_hitTerrainEffect"
        };

        // Returns a NEW EffectList containing both lists' effects
        private static EffectList Merge(EffectList? a, EffectList? b)
        {
            var list = new System.Collections.Generic.List<EffectList.EffectData>();
            if (a?.m_effectPrefabs != null) list.AddRange(a.m_effectPrefabs);
            if (b?.m_effectPrefabs != null) list.AddRange(b.m_effectPrefabs);
            return new EffectList { m_effectPrefabs = list.ToArray() };
        }

        private static readonly System.Reflection.MethodInfo _memberwiseClone =
            AccessTools.Method(typeof(object), "MemberwiseClone");

        private static void OverrideAnim(ItemDrop drop, ItemDrop template)
        {
            var shared = drop.m_itemData.m_shared;
            var attack = shared.m_attack;
            var templateAttack = template.m_itemData.m_shared.m_attack;

            // ObjectDB loads more than once per session — skip weapons that are
            // already converted, or the slam copy below would copy the combo
            if (attack.m_attackAnimation == ComboAnim) return;

            // Preserve the ORIGINAL primary attack (the vanilla overhead slam
            // with its full area, range, width and shockwave) as the heavy
            // attack, before the primary is converted into the combo
            var slam = (Attack)_memberwiseClone.Invoke(attack, null);
            shared.m_secondaryAttack = slam;

            // Valheim plays BOTH the attack's effect lists and the weapon-wide
            // ones on every swing. The sledge keeps its ground shockwave in the
            // weapon-wide lists, so move them into the slam's own lists (the
            // heavy attack keeps everything), then clear the weapon-wide ones
            // so they stop firing on the combo.
            var templateShared = template.m_itemData.m_shared;
            foreach (var name in SharedEffectNames)
            {
                var sharedField = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), name);
                var attackField = AccessTools.Field(typeof(Attack), name);
                if (sharedField == null || attackField == null) continue;
                if (sharedField.FieldType != typeof(EffectList) ||
                    attackField.FieldType != typeof(EffectList)) continue;

                attackField.SetValue(slam, Merge(
                    (EffectList)attackField.GetValue(slam),
                    (EffectList)sharedField.GetValue(shared)));

                sharedField.SetValue(shared, new EffectList());
            }

            var savedDamages = shared.m_damages;
            var savedDamagesPerLevel = shared.m_damagesPerLevel;

            attack.m_attackType = templateAttack.m_attackType;
            attack.m_attackAnimation = ComboAnim;
            attack.m_attackChainLevels = 3;
            attack.m_hitTerrain = templateAttack.m_hitTerrain;
            attack.m_attackRange = templateAttack.m_attackRange;
            attack.m_attackHeight = templateAttack.m_attackHeight;
            attack.m_attackAngle = templateAttack.m_attackAngle;
            attack.m_attackRayWidth = templateAttack.m_attackRayWidth;
            attack.m_maxYAngle = templateAttack.m_maxYAngle;
            attack.m_lowerDamagePerHit = templateAttack.m_lowerDamagePerHit;

            // Replace EVERY effect list on the normal attack with the
            // greatsword's, so none of the sledge's slam/impact effects remain.
            // Found by reflection, so lists we don't know by name are covered.
            // The template's lists are only referenced, never modified, and the
            // heavy attack (a separate Attack) keeps all its sledge effects.
            foreach (var field in AccessTools.GetDeclaredFields(typeof(Attack)))
            {
                if (field.FieldType == typeof(EffectList))
                    field.SetValue(attack, field.GetValue(templateAttack));
            }

            // Add the greatsword's weapon-wide effects too, so the combo keeps
            // any swing sounds/effects the greatsword stores at weapon level
            foreach (var name in SharedEffectNames)
            {
                var sharedField = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), name);
                var attackField = AccessTools.Field(typeof(Attack), name);
                if (sharedField == null || attackField == null) continue;
                if (sharedField.FieldType != typeof(EffectList) ||
                    attackField.FieldType != typeof(EffectList)) continue;

                attackField.SetValue(attack, Merge(
                    (EffectList)attackField.GetValue(templateAttack),
                    (EffectList)sharedField.GetValue(templateShared)));
            }

            // Some weapons spawn an area-effect object on trigger — use the
            // greatsword's (normally none) so the sledge's can't carry over
            var spawnField = AccessTools.Field(typeof(Attack), "m_spawnOnTrigger");
            if (spawnField != null)
                spawnField.SetValue(attack, spawnField.GetValue(templateAttack));

            // Custom swing sound — silence the borrowed swing effects so only
            // the OGG plays (from Attack_OnAttackTrigger_MaceSwingSound)
            if (UseSwingOgg)
            {
                attack.m_startEffect = new EffectList();
                attack.m_triggerEffect = new EffectList();
            }

            shared.m_damages = savedDamages;
            shared.m_damagesPerLevel = savedDamagesPerLevel;
        }
    }

    // -----------------------------------------------------------------------
    // Plays the custom 2H mace swing OGG at each normal-attack swing, for the
    // local player's own attacks, broadcast so all players hear it.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
    public static class Attack_OnAttackTrigger_MaceSwingSound
    {
        static void Postfix(Attack __instance)
        {
            if (!TwoHandedMaceAnimOverride_Patch.UseSwingOgg) return;
            string ogg = Plugin.TwoHandedMaceSwingOgg.Value;

            var player = Player.m_localPlayer;
            if (player == null || __instance.m_character != player) return;

            // Normal (combo) attack only — not the heavy slam
            if (__instance.m_attackAnimation != TwoHandedMaceAnimOverride_Patch.ComboAnim) return;

            var weapon = player.GetCurrentWeapon();
            if (weapon == null) return;
            if (weapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeapon) return;
            if (weapon.m_shared.m_skillType != global::Skills.SkillType.Clubs) return;

            NetworkedEffects.BroadcastOgg(ogg, player.transform.position);
        }
    }
}