using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Patches
{
    // -----------------------------------------------------------------------
    // StaffCastGuard
    //
    // Valheim staff animations contain an "attack trigger" animation event
    // that tells the current Attack to spawn its projectile. If the player
    // used a normal staff attack earlier, that Attack object can linger on
    // the character. When a staff skill then plays a staff animation
    // (staff_shield, staff_thunder, etc.), the animation event fires the
    // lingering attack and spawns the staff's normal projectile.
    //
    // Call StaffCastGuard.Begin(player, duration) at the start of any staff
    // skill. It:
    //   1. Cancels and clears any lingering attack on the player
    //   2. Blocks Attack.OnAttackTrigger for the local player for 'duration'
    // -----------------------------------------------------------------------
    public static class StaffCastGuard
    {
        private static float _blockUntil = 0f;

        public static bool IsBlocking => Time.time < _blockUntil;

        public static void Begin(Player player, float duration)
        {
            if (player == null) return;

            // 1. Cancel any lingering normal attack
            if (player.m_currentAttack != null)
            {
                player.m_currentAttack.Stop();
                player.m_previousAttack = player.m_currentAttack;
                player.m_currentAttack = null;
            }
            player.ClearActionQueue();

            // 2. Block attack triggers for the cast window
            _blockUntil = Time.time + duration;
        }
    }

    // -----------------------------------------------------------------------
    // Blocks the attack trigger animation event on the local player while a
    // staff skill is casting, so no normal staff projectile can spawn.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
    public static class Attack_OnAttackTrigger_StaffGuard
    {
        static bool Prefix(Attack __instance)
        {
            if (!StaffCastGuard.IsBlocking) return true;
            if (__instance.m_character != Player.m_localPlayer) return true;

            // Skip the normal attack's projectile spawn during skill cast
            return false;
        }
    }
}