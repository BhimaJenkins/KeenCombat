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
            // 1. Calculate horizontal direction based on camera forward angle
            Vector3 cameraForward = GameCamera.instance != null
                ? GameCamera.instance.transform.forward
                : player.transform.forward;

            // Strip vertical pitch component to keep movement strictly horizontal
            Vector3 blinkDir = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
            if (blinkDir == Vector3.zero)
                blinkDir = player.transform.forward;

            Vector3 startPos = player.transform.position;
            float maxDist = Plugin.SwordBlinkDistance.Value;

            // 2. Obstacle Detection
            int wallMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");
            float blinkDist = maxDist;

            if (Physics.Raycast(startPos + Vector3.up, blinkDir, out RaycastHit wallHit,
                                maxDist, wallMask))
                blinkDist = Mathf.Max(0.5f, wallHit.distance - 0.5f);

            Vector3 rawEndPos = startPos + (blinkDir * blinkDist);

            // 3. Terrain Height Snapping
            Vector3 endPos = rawEndPos;
            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.GetGroundHeight(rawEndPos, out float groundHeight))
            {
                endPos.y = groundHeight;
            }
            else if (Physics.Raycast(rawEndPos + Vector3.up * 5f, Vector3.down,
                                     out RaycastHit groundHit, 10f, wallMask))
            {
                endPos.y = groundHit.point.y;
            }

            // 4. Play sword lunge animation on all clients — speed up, then
            //    jump past the wind-up straight into the forward thrust
            NetworkedEffects.BroadcastAnimatorSpeed(player, 2.5f);
            NetworkedEffects.BroadcastAnimationPlay(player, "sword_secondary", 0, 0.45f);

            // 5. Spawn Eikthyr shockwave VFX — broadcast to all clients
            NetworkedEffects.BroadcastVfx("fx_eikthyr_forwardshockwave",
                startPos + Vector3.up,
                Quaternion.LookRotation(blinkDir));

            // 6. Teleport player instantly (local only — position syncs via ZNet)
            if (player.m_body != null)
            {
                player.m_body.linearVelocity = Vector3.zero;
                player.m_body.angularVelocity = Vector3.zero;
                player.m_body.position = endPos;
            }

            player.transform.position = endPos;
            player.transform.rotation = Quaternion.LookRotation(blinkDir);
            player.m_maxAirAltitude = endPos.y;

            // 7. Apply damage along the travel path
            ApplyBlinkHit(player, weapon, startPos, endPos, blinkDir);

            yield return new WaitForSeconds(0.25f);

            // Restore animator speed on all clients
            NetworkedEffects.BroadcastAnimatorSpeed(player, 1f);
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
                if (character.m_faction == Character.Faction.Players) continue;

                hit.m_point = character.transform.position;
                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }
    }
}