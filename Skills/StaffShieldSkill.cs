using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // StaffShieldSkill — Sanctuary
    //
    // Casts a stationary dome at the caster's feet. Enemies and enemy
    // projectiles inside are slowed (movement, attack animations, projectile
    // speed). The dome logic runs on every client in SanctuaryDome.
    //
    // Cost: Eitr + % max HP, both scaled by Blood Magic skill level.
    // Blocked if the health cost would kill the caster.
    // Recast replaces the previous dome.
    // -----------------------------------------------------------------------
    public class StaffShieldSkill : IWeaponSkill
    {
        public string SkillName => "Sanctuary";
        public string Description => "Raise a warding dome that slows enemies and their " +
                                        "projectiles to a crawl.";
        public float Cooldown => 0f;
        public string CooldownSEName => "SE_Cooldown_StaffShield";

        // Custom sounds
        private const string CastOgg = "SanctuaryCastSound.ogg";
        private const string BubbleOgg = "SanctuaryBubbleSound.ogg";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("SanctuaryIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player) => false;

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            float mult = MagicCostScaler.GetMultiplier(player, weapon);
            float eitrCost = Plugin.StaffShieldEitrCost.Value * mult;
            float hpCost = player.GetMaxHealth() * Plugin.StaffShieldHealthPercent.Value * mult;

            if (!player.HaveEitr(eitrCost))
            {
                SkillFeedback.NotEnoughEitr();
                return;
            }

            if (player.GetHealth() <= hpCost)
            {
                player.Message(MessageHud.MessageType.Center, "Not enough health");
                return;
            }

            player.UseEitr(eitrCost);
            player.SetHealth(player.GetHealth() - hpCost);

            player.StartCoroutine(CastRoutine(player));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator CastRoutine(Player player)
        {
            Patches.StaffCastGuard.Begin(player, 2f);

            // Lock the dome position at cast time
            Vector3 center = player.transform.position;

            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("staff_summon");

            // Cast VFX attached to the caster: tinted to the dome color,
            // lowered a bit, built-in audio stripped
            string vfx = Plugin.StaffShieldCastVfx.Value;
            if (!string.IsNullOrEmpty(vfx))
            {
                NetworkedEffects.BroadcastVfxOnCharacter(player, vfx,
                    suppressAudio: true,
                    tintHex: Plugin.StaffShieldDomeColor.Value,
                    heightOffset: Plugin.StaffShieldCastVfxHeight.Value);
            }

            // Optional extra vanilla SFX from config
            string sfx = Plugin.StaffShieldCastSfx.Value;
            if (!string.IsNullOrEmpty(sfx))
                NetworkedEffects.BroadcastSfx(sfx, center, player.transform.rotation);

            // Custom cast sound
            NetworkedEffects.BroadcastOgg(CastOgg, player.transform.position);

            // Let the cast animation play out before the bubble appears
            yield return new WaitForSeconds(Plugin.StaffShieldBubbleDelay.Value);
            if (player == null || player.IsDead()) yield break;

            // Bubble sound as the dome appears
            NetworkedEffects.BroadcastOgg(BubbleOgg, center);

            NetworkedEffects.BroadcastSanctuary(
                player.GetZDOID(),
                center,
                Plugin.StaffShieldRadius.Value,
                Plugin.StaffShieldDuration.Value,
                Plugin.StaffShieldSlowPercent.Value);
        }
    }
}