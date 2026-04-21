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

        // Cached audio clip
        private static AudioClip? _cachedRoar = null;
        private static bool _roarLoaded = false;

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

            var animator = player.GetComponentInChildren<Animator>();

            // Preload roar audio
            if (!_roarLoaded)
            {
                yield return player.StartCoroutine(
                    Plugin.LoadAudioClip("FrenzyRoar.ogg", clip =>
                    {
                        _cachedRoar = clip;
                        _roarLoaded = true;
                    }));
            }

            // Play emote at 1.5x speed
            if (animator != null)
                animator.speed = 1.5f;

            // Trigger roar emote
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("emote_roar");

            // Play roar audio
            if (_cachedRoar != null)
            {
                var src = player.GetComponent<AudioSource>()
                       ?? player.gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.PlayOneShot(_cachedRoar);
            }

            // Spawn roar VFX
            var vfxPrefab = ZNetScene.instance?.GetPrefab("fx_Fader_Roar");
            if (vfxPrefab != null)
            {
                var vfx = Object.Instantiate(vfxPrefab,
                    player.transform.position,
                    player.transform.rotation);

                // Recolor red
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(Color.red);
                }
            }

            // Wait for emote to complete (shorter due to 1.5x speed)
            yield return new WaitForSeconds(2.0f / 1.5f);

            // Restore animator speed before applying buff
            if (animator != null)
                animator.speed = 1f;

            if (player == null || player.IsDead()) yield break;

            // Apply Frenzy buff
            HUD.SE_AxeSkillBuff.Apply(player,
                Plugin.AxeBuffDuration.Value,
                Plugin.AxeAttackSpeedBonus.Value,
                Plugin.AxeMoveSpeedBonus.Value);
        }
    }
}