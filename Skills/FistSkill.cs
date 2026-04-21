using System.Collections;
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

        private static IEnumerator OnslaughtRoutine(Player player, ItemDrop.ItemData weapon)
        {
            if (player == null || player.IsDead()) yield break;

            var animator = player.GetComponentInChildren<Animator>();
            if (animator == null) yield break;

            // Lock facing to camera at skill start
            Vector3 attackDir = GameCamera.instance != null
                ? new Vector3(GameCamera.instance.transform.forward.x, 0f,
                              GameCamera.instance.transform.forward.z).normalized
                : player.transform.forward;

            player.transform.rotation = Quaternion.LookRotation(attackDir);

            // Find right hand bone for VFX spawn position
            Transform? rightHand = FindBone(player, "RightHand");

            float totalDistance = 2.0f;
            int comboHits = 4;
            float hitInterval = 0.4f;

            // Load VFX/SFX prefabs
            var comboVfx = ZNetScene.instance?.GetPrefab(ComboVfxName);
            var comboSfx = ZNetScene.instance?.GetPrefab(ComboSfxName);
            var finisherVfx = ZNetScene.instance?.GetPrefab(FinisherVfxName);
            var finisherSfx = ZNetScene.instance?.GetPrefab(FinisherSfxName);

            if (comboVfx == null)
                Plugin.Log.LogWarning($"FistSkill: '{ComboVfxName}' not found!");
            if (comboSfx == null)
                Plugin.Log.LogWarning($"FistSkill: '{ComboSfxName}' not found!");
            if (finisherVfx == null)
                Plugin.Log.LogWarning($"FistSkill: '{FinisherVfxName}' not found!");
            if (finisherSfx == null)
                Plugin.Log.LogWarning($"FistSkill: '{FinisherSfxName}' not found!");

            // ---------------------------------------------------------------
            // Phase 1 — dualaxes combo at 1.2x speed
            // ---------------------------------------------------------------
            animator.speed = 1.2f;

            yield return new WaitForSeconds(0.05f);

            for (int i = 0; i < comboHits; i++)
            {
                if (player == null || player.IsDead()) yield break;

                player.transform.rotation = Quaternion.LookRotation(attackDir);

                animator.SetTrigger($"dualaxes{i}");

                // Spawn combo VFX + SFX at right hand
                Vector3 fistPos = rightHand != null
                    ? rightHand.position
                    : player.transform.position + attackDir * 0.8f + Vector3.up * 1.2f;

                if (comboVfx != null)
                    Object.Instantiate(comboVfx, fistPos,
                                       Quaternion.LookRotation(attackDir));
                if (comboSfx != null)
                    Object.Instantiate(comboSfx, fistPos,
                                       player.transform.rotation);

                // Apply cone damage
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
            // Phase 2 — mace_secondary finisher at normalized 0.5f
            // ---------------------------------------------------------------
            if (player == null || player.IsDead()) yield break;

            player.transform.rotation = Quaternion.LookRotation(attackDir);

            animator.speed = 1.0f;
            animator.Play("mace_secondary", 0, 0.5f);

            // Move remaining 30% during finisher
            player.transform.position += attackDir * (totalDistance * 0.3f);

            // Spawn finisher VFX + SFX at right hand
            Vector3 finisherFistPos = rightHand != null
                ? rightHand.position
                : player.transform.position + attackDir * 0.8f + Vector3.up * 1.2f;

            if (finisherVfx != null)
                Object.Instantiate(finisherVfx, finisherFistPos,
                                   Quaternion.LookRotation(attackDir));
            if (finisherSfx != null)
                Object.Instantiate(finisherSfx, finisherFistPos,
                                   player.transform.rotation);

            yield return new WaitForSeconds(0.25f);

            // Apply rectangle finisher damage
            ApplyRectangleHit(player, weapon, attackDir,
                              Plugin.FistSkillDamage.Value * 1.0f,
                              10.0f);

            yield return new WaitForSeconds(0.4f);

            if (animator != null)
                animator.speed = 1f;
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

        private static void ApplyConeHit(Player player, ItemDrop.ItemData weapon,
                                          Vector3 direction, float damageMultiplier,
                                          float coneAngle, float range)
        {
            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(damageMultiplier);
            hit.m_pushForce = 2.0f;
            hit.m_staggerMultiplier = 1.0f;
            hit.m_dir = direction;
            hit.m_attacker = player.GetZDOID();

            int mask = LayerMask.GetMask("character");
            var cols = Physics.OverlapSphere(
                player.transform.position + Vector3.up + direction, range, mask);

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                if (character.m_faction == Character.Faction.Players) continue;

                Vector3 toTarget = (character.transform.position
                                  - player.transform.position).normalized;
                float angle = Vector3.Angle(direction, toTarget);
                if (angle > coneAngle * 0.5f) continue;

                hit.m_point = character.transform.position;
                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }

        private static void ApplyRectangleHit(Player player, ItemDrop.ItemData weapon,
                                               Vector3 direction, float damageMultiplier,
                                               float depth)
        {
            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(damageMultiplier);
            hit.m_pushForce = 5.0f;
            hit.m_staggerMultiplier = 3.0f;
            hit.m_dir = direction;
            hit.m_attacker = player.GetZDOID();

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

            int mask = LayerMask.GetMask("character");
            var cols = Physics.OverlapBox(boxCenter, halfExtents, boxRot, mask);

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                if (character.m_faction == Character.Faction.Players) continue;

                hit.m_point = character.transform.position;
                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }
    }
}