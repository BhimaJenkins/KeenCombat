using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class AtgeirSkill : IWeaponSkill
    {
        public string SkillName => "Falcon Blitz";
        public string Description => "Unleash a rapid flurry of strikes, hitting your target six times in quick succession.";
        public float Cooldown => Plugin.AtgeirSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Atgeir";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("FalconBlitzIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player)
            => HUD.SE_SkillCooldown.IsOnCooldown(player, CooldownSEName);

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            HUD.SE_SkillCooldown.Apply(player, Cooldown, CooldownSEName);

            if (player.m_currentAttack != null)
            {
                player.m_currentAttack.Stop();
                player.m_previousAttack = player.m_currentAttack;
                player.m_currentAttack = null;
            }
            player.ClearActionQueue();

            player.StartCoroutine(FlurryRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator FlurryRoutine(Player player, ItemDrop.ItemData weapon)
        {
            const int totalHits = 6;

            bool isSpear = weapon.m_shared.m_skillType == global::Skills.SkillType.Spears;
            float hitInterval = isSpear ? 0.25f : 0.33f;
            float rangeMultiplier = isSpear ? 2.0f : 1.0f;

            float savedSpeed = player.m_speed;
            float savedRunSpeed = player.m_runSpeed;
            float savedTurnSpeed = player.m_turnSpeed;
            Vector3 lockedLookDir = player.transform.forward;

            player.m_speed = 0f;
            player.m_runSpeed = 0f;
            player.m_turnSpeed = 0f;

            // ZSyncAnimation syncs triggers to all players
            var zsync = player.GetComponent<ZSyncAnimation>();

            for (int i = 0; i < totalHits; i++)
            {
                if (player == null || player.IsDead()) break;

                player.transform.forward = lockedLookDir;
                player.m_lookDir = lockedLookDir;

                // Strike animation — synced to all players
                if (zsync != null)
                {
                    if (isSpear)
                        zsync.SetTrigger("spear_poke");
                    else
                        zsync.SetTrigger($"atgeir_attack{i % 3}");
                }

                // Lightning strike VFX — broadcast to all players
                Vector3 vfxPos = player.transform.position
                               + lockedLookDir * (1.5f * rangeMultiplier)
                               + Vector3.up;
                NetworkedEffects.BroadcastVfx("vfx_crossbow_lightning_fire",
                    vfxPos, player.transform.rotation);

                // Swing sound — broadcast to all players
                NetworkedEffects.BroadcastSfx("sfx_sword_swing",
                    player.transform.position, player.transform.rotation);

                ApplyHit(player, weapon, lockedLookDir, rangeMultiplier);

                yield return new WaitForSeconds(hitInterval);
            }

            if (player != null)
            {
                player.m_speed = savedSpeed;
                player.m_runSpeed = savedRunSpeed;
                player.m_turnSpeed = savedTurnSpeed;
            }
        }

        private static void ApplyHit(Player player, ItemDrop.ItemData weapon,
                                      Vector3 direction, float rangeMultiplier = 1.0f)
        {
            float range = weapon.m_shared.m_attack.m_attackRange > 0f
                ? weapon.m_shared.m_attack.m_attackRange * rangeMultiplier
                : 3.0f * rangeMultiplier;

            var colliders = Physics.OverlapSphere(
                player.transform.position + direction * (range * 0.5f) + Vector3.up,
                range * 0.5f, SkillMasks.Characters);

            // One hit per enemy per strike, even if it has several colliders
            var alreadyHit = new HashSet<Character>();

            foreach (var col in colliders)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                // Don't hit player-faction allies (Primal Rally summons)
                if (character.m_faction == Character.Faction.Players) continue;
                if (!alreadyHit.Add(character)) continue;

                Vector3 toTarget = (character.transform.position
                                  - player.transform.position).normalized;
                if (Vector3.Dot(direction, toTarget) < 0.3f) continue;

                // Fresh HitData per enemy — Valheim modifies it when applied
                HitData hit = new HitData();
                hit.m_damage = weapon.GetDamage();
                hit.m_damage.Modify(Plugin.AtgeirSkillDamage.Value);
                hit.m_pushForce = 1.0f;
                hit.m_staggerMultiplier = 1.0f;
                hit.m_dir = direction;
                hit.m_attacker = player.GetZDOID();
                hit.m_point = character.transform.position;

                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }
    }
}