using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class DualKnifeSkill : IWeaponSkill
    {
        public string SkillName => "Poison Mayhem";
        public string Description => "Leap backward and drop a cluster of poison bombs at your feet.";
        public float Cooldown => Plugin.DualKnifeSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_DualKnife";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("PoisonMayhemIcon.png");
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
            player.StartCoroutine(PoisonMayhemRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator PoisonMayhemRoutine(Player player, ItemDrop.ItemData weapon)
        {
            if (player == null || player.IsDead()) yield break;

            Vector3 bombOrigin = player.transform.position;
            Vector3 startPos = player.transform.position;

            Vector3 backDir = -player.transform.forward;
            backDir.y = 0f;
            backDir.Normalize();

            float backDist = Plugin.DualKnifeLeapBack.Value;
            float upHeight = Plugin.DualKnifeLeapUp.Value;
            float leapTime = 0.4f;

            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("jump");

            // Smooth arc leap
            float elapsed = 0f;
            while (elapsed < leapTime)
            {
                if (player == null || player.IsDead()) yield break;

                float t = elapsed / leapTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 horizontal = startPos + backDir * (backDist * smoothT);
                float arc = Mathf.Sin(t * Mathf.PI) * upHeight;

                player.transform.position = new Vector3(
                    horizontal.x,
                    startPos.y + arc,
                    horizontal.z);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (player != null)
                player.transform.position = startPos + backDir * backDist;

            yield return new WaitForSeconds(0.1f);

            if (player == null || player.IsDead()) yield break;

            int numBombs = Plugin.DualKnifeNumBombs.Value;
            float spreadRadius = Plugin.DualKnifeSpread.Value;

            var bombPrefab = ZNetScene.instance?.GetPrefab("oozebomb_projectile");
            var vfxPoison = ZNetScene.instance?.GetPrefab("vfx_swamp_poison_hit");
            var vfxCinder = ZNetScene.instance?.GetPrefab("fx_Cinder_storm_hit");

            for (int i = 0; i < numBombs; i++)
            {
                if (player == null) yield break;

                Vector3 offset = new Vector3(
                    Random.Range(-spreadRadius, spreadRadius),
                    0.1f,
                    Random.Range(-spreadRadius, spreadRadius));
                Vector3 spawnPos = bombOrigin + offset;

                if (vfxPoison != null)
                    Object.Instantiate(vfxPoison, spawnPos, Quaternion.identity);
                if (vfxCinder != null)
                    Object.Instantiate(vfxCinder, spawnPos, Quaternion.identity);

                if (bombPrefab != null)
                {
                    var spawnedBomb = Object.Instantiate(bombPrefab, spawnPos, Quaternion.identity);
                    var projectile = spawnedBomb.GetComponent<Projectile>();
                    if (projectile != null)
                    {
                        projectile.Setup(player, Vector3.zero, -1f, null, null, null);
                        projectile.OnHit(null, spawnPos, true, Vector3.up);
                    }
                }

                ApplyBombHit(player, weapon, spawnPos);

                yield return new WaitForSeconds(0.25f);
            }
        }

        private static void ApplyBombHit(Player player, ItemDrop.ItemData weapon,
                                          Vector3 origin)
        {
            var baseDamage = weapon.GetDamage();
            float mult = Plugin.DualKnifeSkillDamage.Value;
            float totalBase = baseDamage.m_slash + baseDamage.m_pierce +
                               baseDamage.m_blunt + baseDamage.m_poison;

            HitData hit = new HitData();
            // Direct damage scaled by config multiplier
            hit.m_damage.m_slash = baseDamage.m_slash * mult;
            hit.m_damage.m_pierce = baseDamage.m_pierce * mult;
            hit.m_damage.m_blunt = baseDamage.m_blunt * mult;
            // Poison damage triggers vanilla DoT
            hit.m_damage.m_poison = totalBase * mult;
            hit.m_pushForce = 1.0f;
            hit.m_staggerMultiplier = 0.5f;
            hit.m_dir = Vector3.up;
            hit.m_attacker = player.GetZDOID();
            hit.m_point = origin;

            float range = Plugin.DualKnifeSpread.Value + 1.5f;
            int mask = LayerMask.GetMask("character");
            var cols = Physics.OverlapSphere(origin + Vector3.up, range, mask);

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                if (character.m_faction == Character.Faction.Players) continue;

                hit.m_point = character.transform.position;
                character.Damage(hit);
            }
        }
    }
}