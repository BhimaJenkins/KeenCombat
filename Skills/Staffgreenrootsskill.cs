using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // StaffGreenRootsSkill — Regeneration
    //
    // Heals all friendly players, summons and tamed creatures within radius.
    // Heal amount and duration are fully configurable.
    // Formula: healPerSecond = (maxHP * healPercent) / duration
    // Refreshes if cast again before expiry — does not stack.
    // Costs Eitr. No cooldown.
    // -----------------------------------------------------------------------
    public class StaffGreenRootsSkill : IWeaponSkill
    {
        public string SkillName => "Regeneration";
        public string Description => "Channel nature's power to regenerate the health of " +
                                        "all nearby allies over time.";
        public float Cooldown => 0f;
        public string CooldownSEName => "SE_Cooldown_StaffGreenRoots";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("RegenerationIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player) => false;

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            float eitrCost = MagicCostScaler.Scale(player, weapon, Plugin.StaffGreenRootsEitrCost.Value);
            if (!player.HaveEitr(eitrCost))
            {
                SkillFeedback.NotEnoughEitr();
                return;
            }
            player.UseEitr(eitrCost);
            player.StartCoroutine(RegenerationRoutine(player));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator RegenerationRoutine(Player player)
        {
            Patches.StaffCastGuard.Begin(player, 2f);
            // 1. Cast animation
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("staff_shield");

            // 2. Broadcast heal VFX + SFX to all clients
            NetworkedEffects.BroadcastVfxSfx(
                "shaman_heal_aoe",
                "sfx_dverger_heal_start",
                player.transform.position,
                player.transform.rotation);

            // Small delay for animation
            yield return new WaitForSeconds(0.5f);

            if (player == null || player.IsDead()) yield break;

            float radius = Plugin.StaffGreenRootsRadius.Value;
            float healPercent = Plugin.StaffGreenRootsHealPercent.Value;
            float duration = Plugin.StaffGreenRootsDuration.Value;

            // 3. Apply regen to all friendlies in radius including caster
            int mask = SkillMasks.Characters;
            var cols = Physics.OverlapSphere(player.transform.position, radius, mask);

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;

                // Only heal friendlies — players, tamed creatures, PrimalRally summons
                bool isPlayer = character.IsPlayer();
                bool isTamed = character.GetComponent<Tameable>() != null &&
                                 character.m_faction == Character.Faction.Players;
                bool isSummon = character.GetComponent<ZNetView>()?.GetZDO()?
                                 .GetBool("KeenCombat_PrimalRally") == true;

                if (!isPlayer && !isTamed && !isSummon) continue;

                // Apply or refresh regen buff
                NetworkedEffects.BroadcastRegeneration(character.GetZDOID(), healPercent, duration);

            }
        }
    }
}