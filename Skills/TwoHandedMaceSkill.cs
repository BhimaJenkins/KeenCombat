using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class TwoHandedMaceSkill : IWeaponSkill
    {
        public string SkillName => "Earthquake";
        public string Description => "Slam the ground and send a shockwave tearing through everything ahead.";
        public float Cooldown => Plugin.TwoHandedMaceSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_TwoHandedMace";

        private const string SlamAnimName = "swing_sledge";
        private const string StompVfxName = "vfx_gdking_stomp";
        private const string StompOgg = "Onslaught.ogg";
        private const float StompScale = 0.7f;
        private const float StompSpacing = 2.0f;
        private const float StompInterval = 0.3f;
        private const int StompCount = 4;
        private const float StompRadius = 3.0f;

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("EarthquakeIcon.png");
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

            player.StartCoroutine(EarthquakeRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator EarthquakeRoutine(Player player, ItemDrop.ItemData weapon)
        {
            if (player == null || player.IsDead()) yield break;

            // Lock facing to camera
            Vector3 attackDir = GameCamera.instance != null
                ? new Vector3(GameCamera.instance.transform.forward.x, 0f,
                              GameCamera.instance.transform.forward.z).normalized
                : player.transform.forward;

            player.transform.rotation = Quaternion.LookRotation(attackDir);

            // Fully root the player
            float savedSpeed = player.m_speed;
            float savedRunSpeed = player.m_runSpeed;
            float savedTurnSpeed = player.m_turnSpeed;

            player.m_speed = 0f;
            player.m_runSpeed = 0f;
            player.m_turnSpeed = 0f;

            // Slam animation — synced to all players by ZSyncAnimation
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger(SlamAnimName);

            // 1 second windup delay — lines up with hammer hitting the ground
            yield return new WaitForSeconds(1.0f);

            // Sequential stomp explosions
            for (int i = 0; i < StompCount; i++)
            {
                if (player == null || player.IsDead()) break;

                float dist = StompSpacing * (i + 1);
                Vector3 stompPos = player.transform.position + attackDir * dist;
                stompPos.y = player.transform.position.y;

                // Stomp VFX at 70% scale and sound at the impact — all players
                NetworkedEffects.BroadcastVfx(StompVfxName, stompPos,
                    Quaternion.identity, StompScale);
                NetworkedEffects.BroadcastOgg(StompOgg, stompPos);

                ApplyStompHit(player, weapon, stompPos);

                yield return new WaitForSeconds(StompInterval);
            }

            // Restore movement (also runs if the stomps were cut short)
            if (player != null)
            {
                player.m_speed = savedSpeed;
                player.m_runSpeed = savedRunSpeed;
                player.m_turnSpeed = savedTurnSpeed;
            }
        }

        private static void ApplyStompHit(Player player, ItemDrop.ItemData weapon,
                                           Vector3 position)
        {
            var cols = Physics.OverlapSphere(position + Vector3.up, StompRadius, SkillMasks.Characters);

            // One hit per enemy per stomp, even if it has several colliders
            var alreadyHit = new HashSet<Character>();

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                if (character.m_faction == Character.Faction.Players) continue;
                if (!alreadyHit.Add(character)) continue;

                // Fresh HitData per enemy — Valheim modifies it when applied
                HitData hit = new HitData();
                hit.m_damage = weapon.GetDamage();
                hit.m_damage.Modify(Plugin.TwoHandedMaceSkillDamage.Value);
                hit.m_pushForce = 3.0f;
                hit.m_staggerMultiplier = 2.0f;
                hit.m_dir = Vector3.up;
                hit.m_attacker = player.GetZDOID();
                hit.m_point = character.transform.position;

                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }
    }
}