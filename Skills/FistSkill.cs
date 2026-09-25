using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class FistSkill : IWeaponSkill
    {
        public string SkillName => "Onslaught";
        public string Description => "Unleash a ferocious combo, lunging forward with a devastating finishing blow.";
        public float Cooldown => Plugin.FistSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Fist";

        // VFX/SFX prefab names — easy to swap
        private const string ComboVfxName = "fx_redlightning_burst";
        private const string ComboSfxName = "sfx_bear_claw_attack slash";
        private const string FinisherVfxName = "fx_DvergerMage_Mistile_attack";
        private const string FinisherSfxName = "sfx_bonemaw_serpent_bite";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("OnslaughtIcon.png");
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

            player.StartCoroutine(OnslaughtRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static bool IsGone(Player? player) => player == null || player.IsDead();

        // Restores normal animation speed on all clients if the player still exists
        private static void RestoreSpeed(Player? player)
        {
            if (player != null)
                NetworkedEffects.BroadcastAnimatorSpeed(player, 1f);
        }

        private static IEnumerator OnslaughtRoutine(Player player, ItemDrop.ItemData weapon)
        {
            if (IsGone(player)) yield break;

            var zsync = player.GetComponent<ZSyncAnimation>();

            // Lock facing to camera at skill start
            Vector3 attackDir = GameCamera.instance != null
                ? new Vector3(GameCamera.instance.transform.forward.x, 0f,
                              GameCamera.instance.transform.forward.z).normalized
                : player.transform.forward;

            player.transform.rotation = Quaternion.LookRotation(attackDir);

            // Right hand bone for VFX spawn position
            Transform? rightHand = FindBone(player, "RightHand");

            float totalDistance = 2.0f;
            int comboHits = 4;
            float hitInterval = 0.4f;

            // ---------------------------------------------------------------
            // Phase 1 — dualaxes combo at 1.2x speed (all clients)
            // ---------------------------------------------------------------
            NetworkedEffects.BroadcastAnimatorSpeed(player, 1.2f);

            yield return new WaitForSeconds(0.05f);

            for (int i = 0; i < comboHits; i++)
            {
                if (IsGone(player)) { RestoreSpeed(player); yield break; }

                player.transform.rotation = Quaternion.LookRotation(attackDir);

                // Combo punch — synced to all players by ZSyncAnimation
                if (zsync != null)
                    zsync.SetTrigger($"dualaxes{i}");

                // Combo VFX + SFX at the right hand — broadcast to all players
                Vector3 fistPos = rightHand != null
                    ? rightHand.position
                    : player.transform.position + attackDir * 0.8f + Vector3.up * 1.2f;

                NetworkedEffects.BroadcastVfx(ComboVfxName, fistPos,
                    Quaternion.LookRotation(attackDir));
                NetworkedEffects.BroadcastSfx(ComboSfxName, fistPos,
                    player.transform.rotation);

                // Cone damage
                ApplyConeHit(player, weapon, attackDir,
                             Plugin.FistSkillDamage.Value * 0.5f,
                             60f, 3.0f);

                // Move forward — 70% of total distance spread over combo
                float movePerHit = (totalDistance * 0.7f) / comboHits;
                player.transform.position += attackDir * movePerHit;

                yield return new WaitForSeconds(hitInterval);
            }

            // Extra wait to let 4th hit finish before finisher
            yield return new WaitForSeconds(0.25f);

            // ---------------------------------------------------------------
            // Phase 2 — mace_secondary finisher at normalized 0.5 (all clients)
            // ---------------------------------------------------------------
            if (IsGone(player)) { RestoreSpeed(player); yield break; }

            player.transform.rotation = Quaternion.LookRotation(attackDir);

            NetworkedEffects.BroadcastAnimatorSpeed(player, 1f);
            NetworkedEffects.BroadcastAnimationPlay(player, "mace_secondary", 0, 0.5f);

            // Move remaining 30% during finisher
            player.transform.position += attackDir * (totalDistance * 0.3f);

            // Finisher VFX + SFX at the right hand — broadcast to all players
            Vector3 finisherFistPos = rightHand != null
                ? rightHand.position
                : player.transform.position + attackDir * 0.8f + Vector3.up * 1.2f;

            NetworkedEffects.BroadcastVfx(FinisherVfxName, finisherFistPos,
                Quaternion.LookRotation(attackDir));
            NetworkedEffects.BroadcastSfx(FinisherSfxName, finisherFistPos,
                player.transform.rotation);

            yield return new WaitForSeconds(0.25f);

            if (IsGone(player)) yield break;

            // Rectangle finisher damage
            ApplyRectangleHit(player, weapon, attackDir,
                              Plugin.FistSkillDamage.Value * 1.0f,
                              10.0f);
        }

        // -----------------------------------------------------------------------
        // Find a bone by name in the player hierarchy
        // -----------------------------------------------------------------------
        private static Transform? FindBone(Player player, string boneName)
        {
            foreach (var t in player.GetComponentsInChildren<Transform>())
                if (t.name == boneName)
                    return t;
            return null;
        }

        private static bool IsValidTarget(Player player, Character? character)
        {
            if (character == null) return false;
            if (character == player) return false;
            if (character.IsPlayer() && !player.IsPVPEnabled()) return false;
            if (character.m_faction == Character.Faction.Players) return false;
            return true;
        }

        private static void DamageTarget(Player player, ItemDrop.ItemData weapon,
                                         Character character, Vector3 direction,
                                         float damageMultiplier, float push, float stagger)
        {
            // Fresh HitData per enemy — Valheim modifies it when applied
            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(damageMultiplier);
            hit.m_pushForce = push;
            hit.m_staggerMultiplier = stagger;
            hit.m_dir = direction;
            hit.m_attacker = player.GetZDOID();
            hit.m_point = character.transform.position;

            character.Damage(hit);

            if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                character.SetHealth(1f);
        }

        private static void ApplyConeHit(Player player, ItemDrop.ItemData weapon,
                                          Vector3 direction, float damageMultiplier,
                                          float coneAngle, float range)
        {
            var cols = Physics.OverlapSphere(
                player.transform.position + Vector3.up + direction, range, SkillMasks.Characters);

            // One hit per enemy, even if it has several colliders
            var alreadyHit = new HashSet<Character>();

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (!IsValidTarget(player, character)) continue;

                Vector3 toTarget = (character!.transform.position
                                  - player.transform.position).normalized;
                if (Vector3.Angle(direction, toTarget) > coneAngle * 0.5f) continue;

                if (!alreadyHit.Add(character)) continue;

                DamageTarget(player, weapon, character, direction,
                             damageMultiplier, 2.0f, 1.0f);
            }
        }

        private static void ApplyRectangleHit(Player player, ItemDrop.ItemData weapon,
                                               Vector3 direction, float damageMultiplier,
                                               float depth)
        {
            float playerHeight = 1.8f;
            float rectHeight = playerHeight * 1.5f;
            float rectWidth = 2.0f;

            Vector3 boxCenter = player.transform.position
                              + direction * (depth * 0.5f)
                              + Vector3.up * (rectHeight * 0.5f);

            Vector3 halfExtents = new Vector3(rectWidth * 0.5f,
                                              rectHeight * 0.5f,
                                              depth * 0.5f);
            Quaternion boxRot = Quaternion.LookRotation(direction);

            var cols = Physics.OverlapBox(boxCenter, halfExtents, boxRot, SkillMasks.Characters);

            // One hit per enemy, even if it has several colliders
            var alreadyHit = new HashSet<Character>();

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (!IsValidTarget(player, character)) continue;
                if (!alreadyHit.Add(character!)) continue;

                DamageTarget(player, weapon, character!, direction,
                             damageMultiplier, 5.0f, 3.0f);
            }
        }
    }
}