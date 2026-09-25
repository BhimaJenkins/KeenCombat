using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class AxeSkill : IWeaponSkill
    {
        public string SkillName => "Frenzy";
        public string Description => "Let out a battle roar, boosting your attack and movement speed.";
        public float Cooldown => Plugin.AxeSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Axe";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("FrenzyIcon.png");
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
            player.StartCoroutine(FrenzyRoutine(player));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator FrenzyRoutine(Player player)
        {
            if (player == null || player.IsDead()) yield break;

            // Broadcast animator speed to all clients
            NetworkedEffects.BroadcastAnimatorSpeed(player, 1.5f);

            // Trigger roar emote via ZSyncAnimation (syncs automatically)
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("emote_roar");

            // Broadcast custom OGG to all clients
            NetworkedEffects.BroadcastOgg("FrenzyRoar.ogg", player.transform.position);

            // Roar VFX on all clients, tinted Frenzy orange (built-in audio kept)
            NetworkedEffects.BroadcastVfxOnCharacter(player, "fx_Fader_Roar",
                suppressAudio: false,
                tintHex: "#FF6600");

            // Wait for emote to complete (shorter due to 1.5x speed)
            yield return new WaitForSeconds(2.0f / 1.5f);

            // Restore animator speed on all clients
            NetworkedEffects.BroadcastAnimatorSpeed(player, 1f);

            if (player == null || player.IsDead()) yield break;

            // Apply Frenzy buff (local only — buff is per-player)
            HUD.SE_AxeSkillBuff.Apply(player,
                Plugin.AxeBuffDuration.Value,
                Plugin.AxeAttackSpeedBonus.Value,
                Plugin.AxeMoveSpeedBonus.Value);
        }
    }
}