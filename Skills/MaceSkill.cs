using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class MaceSkill : IWeaponSkill
    {
        public string SkillName => "Bulwark";
        public string Description => "Brace yourself, reducing damage taken and regenerating health.";
        public float Cooldown => Plugin.MaceSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Mace";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("Bulwark.png");
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

            // Start emote via ZSyncAnimation
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("emote_flex");

            player.StartCoroutine(SlowAndRestore(player, 1.5f));

            // Spawn VFX at player position
            var vfxPrefab = ZNetScene.instance?.GetPrefab("fx_guardstone_activate");
            if (vfxPrefab != null)
                Object.Instantiate(vfxPrefab, player.transform.position, player.transform.rotation);
            else
                Plugin.Log.LogWarning("MaceSkill: fx_guardstone_activate not found.");

            // Apply Bulwark buff
            HUD.SE_MaceSkillBuff.Apply(player,
                Plugin.MaceBuffDuration.Value,
                Plugin.MaceDamageReduction.Value,
                Plugin.MaceRegenPerSec.Value);
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator SlowAndRestore(Player player, float duration)
        {
            float savedSpeed = player.m_speed;
            float savedRunSpeed = player.m_runSpeed;

            player.m_speed = 0.5f;
            player.m_runSpeed = 0.5f;

            yield return new WaitForSeconds(1f);

            if (player == null) yield break;

            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("emote_stop");

            yield return new WaitForSeconds(duration - 1f);

            if (player == null) yield break;

            player.m_speed = savedSpeed;
            player.m_runSpeed = savedRunSpeed;
        }
    }
}