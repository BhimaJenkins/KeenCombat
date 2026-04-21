using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class SwordSkill : IWeaponSkill
    {
        public string SkillName => "Blink Strike";
        public string Description => "Dash forward and strike your target with blinding speed.";
        public float Cooldown => Plugin.SwordSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Sword";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("SwordBlink.png");
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
            player.StartCoroutine(BlinkRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator BlinkRoutine(Player player, ItemDrop.ItemData weapon)
        {
            var animator = player.GetComponentInChildren<Animator>();

            // Get camera-facing blink direction
            Vector3 blinkDir = GameCamera.instance != null
                ? new Vector3(GameCamera.instance.transform.forward.x, 0f,
                              GameCamera.instance.transform.forward.z).normalized
                : player.transform.forward;

            Vector3 startPos = player.transform.position;
            float maxDist = Plugin.SwordBlinkDistance.Value;

            // Raycast to find blink end point — stop at walls
            int wallMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");
            float blinkDist = maxDist;
            if (Physics.Raycast(startPos + Vector3.up, blinkDir, out RaycastHit wallHit,
                                maxDist, wallMask))
                blinkDist = wallHit.distance - 0.5f;

            Vector3 endPos = startPos + blinkDir * blinkDist + Vector3.up * 0.5f;

            // Play blink animation
            if (animator != null)
            {
                animator.speed = Plugin.SwordAnimSpeed.Value;
                animator.Play("atgeir_secondary", 0, 0.6f);
            }

            // Spawn Eikthyr shockwave VFX at start
            var vfxPrefab = ZNetScene.instance?.GetPrefab("fx_eikthyr_forwardshockwave");
            if (vfxPrefab != null)
                Object.Instantiate(vfxPrefab, startPos + Vector3.up,
                                   Quaternion.LookRotation(blinkDir));

            // Teleport player to end position
            player.transform.position = endPos;
            player.transform.forward = blinkDir;

            yield return new WaitForSeconds(0.1f);

            // Apply capsule hit along blink path
            ApplyBlinkHit(player, weapon, startPos, endPos, blinkDir);

            yield return new WaitForSeconds(0.25f);

            if (animator != null)
                animator.speed = 1f;
        }

        private static void ApplyBlinkHit(Player player, ItemDrop.ItemData weapon,
                                           Vector3 startPos, Vector3 endPos,
                                           Vector3 direction)
        {
            float radius = Plugin.SwordHitRadius.Value;
            float extendFront = Plugin.SwordHitExtendFront.Value;
            float extendBack = Plugin.SwordHitExtendBack.Value;
            float heightTop = Plugin.SwordHitHeightTop.Value;
            float heightBot = Plugin.SwordHitHeightBottom.Value;

            Vector3 capsuleStart = startPos - direction * extendBack + Vector3.up * heightBot;
            Vector3 capsuleEnd = endPos + direction * extendFront + Vector3.up * heightTop;

            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(Plugin.SwordSkillDamage.Value);
            hit.m_pushForce = 3.0f;
            hit.m_staggerMultiplier = 2.0f;
            hit.m_dir = direction;
            hit.m_attacker = player.GetZDOID();

            int mask = LayerMask.GetMask("character");
            var colliders = Physics.OverlapCapsule(capsuleStart, capsuleEnd, radius, mask);

            foreach (var col in colliders)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                // Don't hit player-faction allies (Primal Rally summons)
                if (character.m_faction == Character.Faction.Players) continue;

                hit.m_point = character.transform.position;
                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }
    }
}