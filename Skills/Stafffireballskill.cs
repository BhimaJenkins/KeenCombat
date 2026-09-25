using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // StaffFireballSkill — Meteor Strike
    //
    // Calls down a field of meteors at the cast position for 12 seconds.
    // Each meteor spawns at 20f height, falls to the ground, and detonates
    // with fx_goblinking_meteor_hit dealing damage in a 12f radius.
    // Meteors spawn randomly within the configured radius of the cast point.
    // Interval between meteors: 0.5s - 1.25s (randomized).
    // Player is slowed for 2 seconds during the cast animation commitment.
    // Costs Eitr to cast — no cooldown.
    // -----------------------------------------------------------------------
    public class StaffFireballSkill : IWeaponSkill
    {
        public string SkillName => "Meteor Strike";
        public string Description => "Call down a storm of meteors at your location, " +
                                        "scorching all enemies in a wide area.";
        public float Cooldown => 0f;
        public string CooldownSEName => "SE_Cooldown_StaffFireball";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("MeteorStrikeIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player) => false;

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            float eitrCost = MagicCostScaler.Scale(player, weapon, Plugin.StaffFireballEitrCost.Value);

            if (!player.HaveEitr(eitrCost))
            {
                SkillFeedback.NotEnoughEitr();
                return;
            }

            player.UseEitr(eitrCost);
            player.StartCoroutine(MeteorStrikeRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator MeteorStrikeRoutine(Player player, ItemDrop.ItemData weapon)
        {
            Patches.StaffCastGuard.Begin(player, 2f);
            // Lock cast position — meteors fall here regardless of player movement
            Vector3 castPos = player.transform.position;

            float duration = Plugin.StaffFireballDuration.Value;
            float radius = Plugin.StaffFireballRadius.Value;
            float meteorHeight = 20f;

            // 1. Play cast animation
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("staff_shield");

            // 2. Slow player during 2 second cast commitment window
            float savedSpeed = player.m_speed;
            float savedRunSpeed = player.m_runSpeed;
            player.m_speed *= 0.3f;
            player.m_runSpeed *= 0.3f;

            yield return new WaitForSeconds(2f);

            // 3. Restore speed — meteors now begin falling
            if (player == null || player.IsDead()) yield break;
            player.m_speed = savedSpeed;
            player.m_runSpeed = savedRunSpeed;

            // 4. Meteor loop
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (player == null || player.IsDead()) yield break;

                // Random position within the radius
                Vector2 randomCircle = Random.insideUnitCircle * radius;
                Vector3 groundTarget = castPos + new Vector3(randomCircle.x, 0f, randomCircle.y);

                // Snap target to terrain height
                if (ZoneSystem.instance != null &&
                    ZoneSystem.instance.GetGroundHeight(groundTarget, out float gh))
                    groundTarget.y = gh;

                Vector3 spawnPos = groundTarget + Vector3.up * meteorHeight;

                // Broadcast meteor to all clients
                NetworkedEffects.BroadcastMeteor(
                    spawnPos,
                    groundTarget,
                    weapon.GetDamage().m_fire,
                    Plugin.StaffFireballDamageRadius.Value,
                    player.GetZDOID());

                // Random interval before next meteor
                float interval = Random.Range(
                    Plugin.StaffFireballIntervalMin.Value,
                    Plugin.StaffFireballIntervalMax.Value);

                elapsed += interval;
                yield return new WaitForSeconds(interval);
            }
        }
    }
}