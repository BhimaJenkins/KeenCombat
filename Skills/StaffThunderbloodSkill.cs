using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // StaffThunderbloodSkill — Thunderstruck
    //
    // Casts a long-lasting buff that periodically calls lightning down on
    // hostile enemies inside a radius around the player. The strike logic
    // lives in SE_ThunderstruckBuff.
    //
    // Damage per strike = total staff weapon damage × DamagePercent,
    // dealt as lightning. Snapshotted at cast time.
    // Recast refreshes the duration. Costs Eitr (scaled by skill level).
    // -----------------------------------------------------------------------
    public class StaffThunderbloodSkill : IWeaponSkill
    {
        public string SkillName => "Thunderstruck";
        public string Description => "Bathe yourself in electrical energy, calling lightning " +
                                        "down on enemies that linger near you.";
        public float Cooldown => 0f;
        public string CooldownSEName => "SE_Cooldown_StaffThunderblood";

        // Custom cast sound
        private const string CastOgg = "ThunderstruckCharacterAudio.ogg";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("ThunderstruckIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player) => false;

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            float eitrCost = MagicCostScaler.Scale(player, weapon,
                                 Plugin.StaffThunderbloodEitrCost.Value);
            if (!player.HaveEitr(eitrCost))
            {
                SkillFeedback.NotEnoughEitr();
                return;
            }

            player.UseEitr(eitrCost);
            player.StartCoroutine(CastRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        /// <summary>Damage of one lightning strike for this weapon.</summary>
        public static float GetStrikeDamage(ItemDrop.ItemData weapon)
        {
            var d = weapon.GetDamage();
            float total = d.m_blunt + d.m_slash + d.m_pierce + d.m_fire +
                          d.m_frost + d.m_lightning + d.m_poison + d.m_spirit;
            return total * Plugin.StaffThunderbloodDamagePercent.Value;
        }

        private static IEnumerator CastRoutine(Player player, ItemDrop.ItemData weapon)
        {
            Patches.StaffCastGuard.Begin(player, 2f);

            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("staff_shield");

            // Cast VFX attached to the caster on all clients, built-in audio stripped
            string vfx = Plugin.StaffThunderbloodCastVfx.Value;
            if (!string.IsNullOrEmpty(vfx))
                NetworkedEffects.BroadcastVfxOnCharacter(player, vfx, suppressAudio: true);

            // Optional vanilla SFX from config, plus the custom OGG
            string sfx = Plugin.StaffThunderbloodCastSfx.Value;
            if (!string.IsNullOrEmpty(sfx))
                NetworkedEffects.BroadcastSfx(sfx, player.transform.position, player.transform.rotation);

            NetworkedEffects.BroadcastOgg(CastOgg, player.transform.position);

            yield return new WaitForSeconds(0.5f);
            if (player == null || player.IsDead()) yield break;

            HUD.SE_ThunderstruckBuff.Apply(player,
                GetStrikeDamage(weapon),
                Plugin.StaffThunderbloodDuration.Value);
        }
    }
}