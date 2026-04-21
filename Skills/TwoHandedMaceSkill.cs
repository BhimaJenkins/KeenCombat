using System.Collections;
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
        private const float StompScale = 0.7f;
        private const float StompSpacing = 2.0f;
        private const float StompInterval = 0.3f;
        private const int StompCount = 4;
        private const float StompRadius = 3.0f;

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        // Cache the audio clip after first load
        private static AudioClip? _cachedClip = null;
        private static bool _clipLoaded = false;

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

            var animator = player.GetComponentInChildren<Animator>();
            if (animator == null) yield break;

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

            // Preload audio clip if not cached
            if (!_clipLoaded)
            {
                yield return player.StartCoroutine(
                    Plugin.LoadAudioClip("Onslaught.ogg", clip =>
                    {
                        _cachedClip = clip;
                        _clipLoaded = true;
                    }));
            }

            var stompVfxPrefab = ZNetScene.instance?.GetPrefab(StompVfxName);
            if (stompVfxPrefab == null)
                Plugin.Log.LogWarning($"EarthquakeSkill: '{StompVfxName}' not found!");

            // Play slam animation
            animator.SetTrigger(SlamAnimName);

            // 1 second windup delay — lines up with hammer hitting the ground
            yield return new WaitForSeconds(1.0f);

            if (player == null || player.IsDead()) yield break;

            // Sequential stomp explosions — audio plays with each one
            for (int i = 0; i < StompCount; i++)
            {
                if (player == null || player.IsDead()) yield break;

                float dist = StompSpacing * (i + 1);
                Vector3 stompPos = player.transform.position + attackDir * dist;
                stompPos.y = player.transform.position.y;

                // Spawn stomp VFX at 70% scale
                if (stompVfxPrefab != null)
                {
                    var vfx = Object.Instantiate(stompVfxPrefab, stompPos,
                                                 Quaternion.identity);
                    vfx.transform.localScale = Vector3.one * StompScale;
                }

                // Play audio on each stomp
                if (_cachedClip != null)
                {
                    var src = player.GetComponent<AudioSource>()
                           ?? player.gameObject.AddComponent<AudioSource>();
                    src.spatialBlend = 1f;
                    src.PlayOneShot(_cachedClip);
                }

                ApplyStompHit(player, weapon, stompPos);

                yield return new WaitForSeconds(StompInterval);
            }

            // Restore movement
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
            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(Plugin.TwoHandedMaceSkillDamage.Value);
            hit.m_pushForce = 3.0f;
            hit.m_staggerMultiplier = 2.0f;
            hit.m_dir = Vector3.up;
            hit.m_attacker = player.GetZDOID();
            hit.m_point = position;

            int mask = LayerMask.GetMask("character");
            var cols = Physics.OverlapSphere(position + Vector3.up, StompRadius, mask);

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