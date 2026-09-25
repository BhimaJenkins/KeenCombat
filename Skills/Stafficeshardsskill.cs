using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // StaffIceShardsSkill — Ice Nova
    //
    // AOE frost burst at cast position.
    // - Instantly deals 2x weapon frost damage to all enemies in radius
    // - Freezes enemy movement for 8 seconds (attack still works)
    // - Spawns IceBlocker at 70% scale at each frozen enemy's feet
    // - After 8 seconds: IceBlocker despawns, fx_DvergerMage_Ice_hit plays
    // - Post-freeze: 3 second movement + attack speed slow
    // - Costs 30 Eitr, no cooldown
    // - Does not affect friendly summons, tamed creatures or allies
    // -----------------------------------------------------------------------
    public class StaffIceShardsSkill : IWeaponSkill
    {
        public string SkillName => "Ice Nova";
        public string Description => "Unleash a burst of frost, freezing all nearby enemies " +
                                        "in place and shattering them with cold damage.";
        public float Cooldown => 0f;
        public string CooldownSEName => "SE_Cooldown_StaffIceShards";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("IceNovaIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player) => false;

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            float eitrCost = MagicCostScaler.Scale(player, weapon, Plugin.StaffIceShardsEitrCost.Value);

            if (!player.HaveEitr(eitrCost))
            {
                SkillFeedback.NotEnoughEitr();
                return;
            }

            player.UseEitr(eitrCost);
            player.StartCoroutine(IceNovaRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator IceNovaRoutine(Player player, ItemDrop.ItemData weapon)
        {
            Patches.StaffCastGuard.Begin(player, 2.5f);
            // 1. Play cast animation — synced to all clients
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("staff_thunder");

            // 2. Broadcast nova ring VFX to all clients (has built-in SFX)
            NetworkedEffects.BroadcastVfx("fx_DvergerMage_Nova_ring",
                player.transform.position,
                player.transform.rotation,
                2f);

            yield return new WaitForSeconds(1.25f);

            if (player == null || player.IsDead()) yield break;

            float radius = Plugin.StaffIceShardsRadius.Value;
            float damage = weapon.GetDamage().m_frost
                           * Plugin.StaffIceShardsDamageMultiplier.Value;
            float freezeDuration = Plugin.StaffIceShardsDuration.Value;
            float slowDuration = Plugin.StaffIceShardsSlowDuration.Value;

            // 3. Find all enemies in radius
            int mask = SkillMasks.Characters;
            var cols = Physics.OverlapSphere(player.transform.position, radius, mask);

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer()) continue;
                if (character.m_faction == Character.Faction.Players) continue;
                if (character.GetComponent<Tameable>()?.IsHungry() == false &&
                    character.m_faction == Character.Faction.Players) continue;

                // 4. Apply frost damage — fresh HitData per enemy
                HitData hit = new HitData();
                hit.m_damage.m_frost = damage;
                hit.m_pushForce = 0f;   // no knockback — they're freezing in place
                hit.m_staggerMultiplier = 0f;
                hit.m_point = character.transform.position;
                hit.m_dir = Vector3.down;
                hit.m_attacker = player.GetZDOID();
                character.Damage(hit);

                // 5. Broadcast freeze + IceBlocker to all clients via RPC
                var nview = character.GetComponent<ZNetView>();
                if (nview != null && nview.GetZDO() != null)
                {
                    NetworkedEffects.BroadcastIceFreeze(
                        nview.GetZDO().m_uid,
                        character.transform.position,
                        freezeDuration,
                        slowDuration);
                }

             
            }
        }

        // -----------------------------------------------------------------------
        // Freezes enemy movement while allowing attacks.
        // Applies post-freeze slow on expiry.
        // -----------------------------------------------------------------------
        public static IEnumerator FreezeEnemy(Character character,
                                               float freezeDuration,
                                               float slowDuration)
        {
            if (character == null) yield break;

            var monsterAI = character.GetComponent<MonsterAI>();
            var animalAI = character.GetComponent<AnimalAI>();

            // Save original speeds
            float savedMoveSpeed = character.m_speed;
            float savedRunSpeed = character.m_runSpeed;
            float savedTurnSpeed = character.m_turnSpeed;

            // Freeze movement — zero speeds so AI can't move
            character.m_speed = 0f;
            character.m_runSpeed = 0f;
            character.m_turnSpeed = 0f;

            // Disable AI path following
            if (monsterAI != null) monsterAI.enabled = false;
            if (animalAI != null) animalAI.enabled = false;

            yield return new WaitForSeconds(freezeDuration);

            if (character == null) yield break;

            // Restore movement
            character.m_speed = savedMoveSpeed;
            character.m_runSpeed = savedRunSpeed;
            character.m_turnSpeed = savedTurnSpeed;

            // Re-enable AI
            if (monsterAI != null) monsterAI.enabled = true;
            if (animalAI != null) animalAI.enabled = true;

            // Post-freeze slow (3 seconds)
            if (slowDuration > 0f)
            {
                character.m_speed *= 0.4f;
                character.m_runSpeed *= 0.4f;

                yield return new WaitForSeconds(slowDuration);

                if (character == null) yield break;
                character.m_speed = savedMoveSpeed;
                character.m_runSpeed = savedRunSpeed;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Receives BroadcastIceFreeze RPC — spawns IceBlocker VFX on all clients.
    // Damage and movement lock applied by sender only.
    // -----------------------------------------------------------------------
    // Registration and handler added to NetworkedEffects.cs
}