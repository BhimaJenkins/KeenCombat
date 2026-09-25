using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class GreatswordSkill : IWeaponSkill
    {
        public string SkillName => "Whirlwind";
        public string Description => "Unleash a devastating double spin attack, carving through everything in your path.";
        public float Cooldown => Plugin.GreatswordSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Greatsword";

        private const string VfxName = "fx_fallenfalkyrie_spin";
        private const string SfxName = "sfx_fader_spin";
        private const float VfxScale = 0.325f;

        private const string SpinAnim = "atgeir_secondary";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("WhirlwindIcon.png");
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

            player.StartCoroutine(WhirlwindRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator WhirlwindRoutine(Player player, ItemDrop.ItemData weapon)
        {
            float totalDistance = Plugin.GreatswordMoveDistance.Value;
            float hitRange = 3.5f;

            Vector3 moveDir = GameCamera.instance != null
                ? new Vector3(GameCamera.instance.transform.forward.x, 0f,
                              GameCamera.instance.transform.forward.z).normalized
                : player.transform.forward;

            // ---------------------------------------------------------------
            // Spin 1 — full atgeir spin from beginning
            // ---------------------------------------------------------------
            NetworkedEffects.BroadcastVfxSfx(
                VfxName, SfxName,
                player.transform.position,
                player.transform.rotation,
                VfxScale);

            // Spin animation on all clients
            NetworkedEffects.BroadcastAnimatorSpeed(player, 1.2f);
            NetworkedEffects.BroadcastAnimationPlay(player, SpinAnim, 0, 0f);
            ApplyHit(player, weapon, hitRange);

            float spin1Duration = Plugin.GreatswordHoldDuration.Value;
            float elapsed = 0f;

            while (elapsed < spin1Duration)
            {
                if (player == null || player.IsDead())
                {
                    // Don't leave the animator stuck fast on anyone's screen
                    if (player != null)
                        NetworkedEffects.BroadcastAnimatorSpeed(player, 1f);
                    yield break;
                }

                float moveDelta = (totalDistance * 0.5f / spin1Duration) * Time.deltaTime;
                player.transform.position += moveDir * moveDelta;

                elapsed += Time.deltaTime;
                yield return null;
            }

            // ---------------------------------------------------------------
            // Spin 2 — jump to 0.5 normalized to skip windup
            // ---------------------------------------------------------------
            NetworkedEffects.BroadcastVfxSfx(
                VfxName, SfxName,
                player.transform.position,
                player.transform.rotation,
                VfxScale);

            NetworkedEffects.BroadcastAnimationPlay(player, SpinAnim, 0, 0.5f);
            ApplyHit(player, weapon, hitRange);

            NetworkedEffects.BroadcastAnimatorSpeed(player, 1f);
        }

        private static void ApplyHit(Player player, ItemDrop.ItemData weapon, float range)
        {
            var cols = Physics.OverlapSphere(
                player.transform.position + Vector3.up, range, SkillMasks.Characters);

            // One hit per enemy per spin, even if it has several colliders
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
                hit.m_damage.Modify(Plugin.GreatswordSkillDamage.Value);
                hit.m_pushForce = 2.0f;
                hit.m_staggerMultiplier = 1.5f;
                hit.m_attacker = player.GetZDOID();
                hit.m_dir = player.transform.forward;
                hit.m_point = character.transform.position;

                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }
    }
}