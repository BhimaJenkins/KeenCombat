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

        // Current aim: camera direction flattened, so the player can steer
        // every swing toward their target
        private static Vector3 CurrentAimDir(Player player)
        {
            if (GameCamera.instance != null)
            {
                var f = GameCamera.instance.transform.forward;
                var flat = new Vector3(f.x, 0f, f.z);
                if (flat.sqrMagnitude > 0.001f) return flat.normalized;
            }
            return player.transform.forward;
        }

        private static IEnumerator OnslaughtRoutine(Player player, ItemDrop.ItemData weapon)
        {
            if (IsGone(player)) yield break;

            var zsync = player.GetComponent<ZSyncAnimation>();

            // Right hand bone for VFX spawn position
            Transform? rightHand = FindBone(player, "RightHand");

            // One-handed sword reach and arc
            var (range, angle) = GetSwordHitbox();

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

                // Re-aim every swing so turning toward an enemy mid-combo connects
                Vector3 attackDir = CurrentAimDir(player);
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

                // Sword-sized arc in front of the player
                ApplyArcHit(player, weapon, attackDir,
                            Plugin.FistSkillDamage.Value * 0.5f,
                            angle, range);

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

            Vector3 finisherDir = CurrentAimDir(player);
            player.transform.rotation = Quaternion.LookRotation(finisherDir);

            NetworkedEffects.BroadcastAnimatorSpeed(player, 1f);
            NetworkedEffects.BroadcastAnimationPlay(player, "mace_secondary", 0, 0.5f);

            // Move remaining 30% during finisher
            player.transform.position += finisherDir * (totalDistance * 0.3f);

            // Finisher VFX + SFX at the right hand — broadcast to all players
            Vector3 finisherFistPos = rightHand != null
                ? rightHand.position
                : player.transform.position + finisherDir * 0.8f + Vector3.up * 1.2f;

            NetworkedEffects.BroadcastVfx(FinisherVfxName, finisherFistPos,
                Quaternion.LookRotation(finisherDir));
            NetworkedEffects.BroadcastSfx(FinisherSfxName, finisherFistPos,
                player.transform.rotation);

            yield return new WaitForSeconds(0.25f);

            if (IsGone(player)) yield break;

            // Long-line finisher damage
            ApplyRectangleHit(player, weapon, finisherDir,
                              Plugin.FistSkillDamage.Value * 1.0f,
                              10.0f);
        }

        // -----------------------------------------------------------------------
        // One-handed sword hitbox, read from a vanilla sword so it matches
        // exactly. Falls back to typical sword values if none is found.
        // -----------------------------------------------------------------------
        private static float _swordRange = -1f;
        private static float _swordAngle = -1f;

        private static (float range, float angle) GetSwordHitbox()
        {
            if (_swordRange > 0f) return (_swordRange, _swordAngle);

            var db = ObjectDB.instance;
            foreach (var name in new[] { "SwordIron", "SwordBronze", "SwordSilver", "SwordBlackmetal" })
            {
                var drop = db?.GetItemPrefab(name)?.GetComponent<ItemDrop>();
                if (drop == null) continue;
                var atk = drop.m_itemData.m_shared.m_attack;
                if (atk.m_attackRange <= 0f) continue;

                _swordRange = atk.m_attackRange;
                _swordAngle = atk.m_attackAngle > 0f ? atk.m_attackAngle : 90f;
                return (_swordRange, _swordAngle);
            }

            return (2.0f, 90f); // fallback, not cached so a later lookup can succeed
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

        // Stagger value large enough to fill any non-boss stagger bar at once
        private const float FullStagger = 1000f;

        private static void DamageTarget(Player player, ItemDrop.ItemData weapon,
                                         Character character, Vector3 direction,
                                         float damageMultiplier, float push)
        {
            // Hits on an already-staggered enemy get Valheim's staggered damage
            // bonus. Divide it back out so every swing deals the same damage it
            // did before Onslaught staggered (the first, unstaggered hit is
            // unchanged). Bosses can't be force-staggered, so no adjustment.
            bool isBoss = character.IsBoss();
            if (!isBoss && character.IsStaggering())
                damageMultiplier /= Mathf.Max(1f, Plugin.FistStaggeredDamageBonus.Value);

            // Fresh HitData per enemy — Valheim modifies it when applied
            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(damageMultiplier);
            hit.m_pushForce = push;
            hit.m_staggerMultiplier = isBoss ? 1f : FullStagger;
            hit.m_dir = direction;
            hit.m_attacker = player.GetZDOID();
            hit.m_point = character.transform.position;

            character.Damage(hit);

            if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                character.SetHealth(1f);
        }

        // Sword-style arc: anything whose nearest point is within reach and
        // inside the arc in front of the player. Measuring to the nearest
        // point (not the creature's center) means large enemies standing
        // right in front of you are hit like with a real sword.
        private static void ApplyArcHit(Player player, ItemDrop.ItemData weapon,
                                        Vector3 direction, float damageMultiplier,
                                        float arcAngle, float range)
        {
            Vector3 origin = player.transform.position + Vector3.up;

            var cols = Physics.OverlapSphere(origin, range + 1f, SkillMasks.Characters);

            // One hit per enemy, even if it has several colliders
            var alreadyHit = new HashSet<Character>();

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (!IsValidTarget(player, character)) continue;
                if (alreadyHit.Contains(character!)) continue;

                // ClosestPoint doesn't support non-convex mesh colliders —
                // use the collider's bounding box for those
                Vector3 nearest = (col is MeshCollider mc && !mc.convex)
                    ? col.bounds.ClosestPoint(origin)
                    : col.ClosestPoint(origin);
                Vector3 flat = nearest - origin;
                flat.y = 0f;

                // Within reach (horizontal), and inside the arc unless
                // practically on top of the player
                if (flat.magnitude > range) continue;
                if (flat.sqrMagnitude > 0.25f &&
                    Vector3.Angle(direction, flat) > arcAngle * 0.5f) continue;

                alreadyHit.Add(character!);
                DamageTarget(player, weapon, character!, direction,
                             damageMultiplier, 2.0f);
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
                             damageMultiplier, 5.0f);
            }
        }
    }
}