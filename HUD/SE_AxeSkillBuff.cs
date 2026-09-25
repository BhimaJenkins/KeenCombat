using HarmonyLib;
using UnityEngine;

namespace KeenCombat.HUD
{
    // -----------------------------------------------------------------------
    // SE_AxeSkillBuff — Frenzy
    //
    // Attack speed: the caster's animator speed is raised at the start of
    // each of THEIR attacks and broadcast so other players see the faster
    // swings too. Restored (and broadcast) when the buff ends.
    // Move speed saved before buff and restored exactly on Stop to avoid
    // floating point drift from multiply/divide approach.
    // -----------------------------------------------------------------------
    public class SE_AxeSkillBuff : StatusEffect
    {
        public const string StatusEffectName = "SE_FrenzyBuff";

        private static SE_AxeSkillBuff? _instance;

        public float AttackSpeedBonus = 0.30f;
        public float MoveSpeedBonus = 0.30f;

        // Saved baseline speeds — restored exactly on Stop
        private static float _savedSpeed = 0f;
        private static float _savedRunSpeed = 0f;

        // True while other clients have been told to speed up our animator,
        // so the "restore to normal" broadcast is only sent when needed
        private static bool _animBoosted = false;

        public static void Register()
        {
            if (_instance != null) return;

            _instance = ScriptableObject.CreateInstance<SE_AxeSkillBuff>();
            _instance.name = StatusEffectName;
            _instance.m_name = "Frenzy";
            _instance.m_icon = Plugin.LoadEmbeddedSprite("FrenzyBuffIcon.png"); // shared loader, all install layouts
            _instance.m_startMessageType = MessageHud.MessageType.TopLeft;
            _instance.m_startMessage = "Frenzy!";
            _instance.m_stopMessageType = MessageHud.MessageType.TopLeft;
            _instance.m_stopMessage = "Frenzy faded.";

            if (ObjectDB.instance != null && !ObjectDB.instance.m_StatusEffects.Contains(_instance))
            {
                ObjectDB.instance.m_StatusEffects.Add(_instance);
                Plugin.Log.LogInfo("SE_AxeSkillBuff registered in ObjectDB.");
            }
        }

        public static void Apply(Player player, float duration,
                                  float attackSpeedBonus, float moveSpeedBonus)
        {
            if (_instance == null)
            {
                Plugin.Log.LogWarning("SE_AxeSkillBuff not registered yet.");
                return;
            }

            _instance.m_ttl = duration;
            _instance.AttackSpeedBonus = attackSpeedBonus;
            _instance.MoveSpeedBonus = moveSpeedBonus;

            // Save baseline speeds before modifying
            _savedSpeed = player.m_speed;
            _savedRunSpeed = player.m_runSpeed;

            // Apply move speed bonus
            player.m_speed *= (1f + moveSpeedBonus);
            player.m_runSpeed *= (1f + moveSpeedBonus);

            player.GetSEMan().AddStatusEffect(_instance, true, 0, 0f);
            Plugin.Log.LogInfo($"Frenzy applied: +{attackSpeedBonus * 100f}% atk speed, " +
                               $"+{moveSpeedBonus * 100f}% move speed for {duration}s");
        }

        public static SE_AxeSkillBuff? GetActiveBuff(Character character)
        {
            return character.GetSEMan()
                            .GetStatusEffect(StatusEffectName.GetStableHashCode())
                            as SE_AxeSkillBuff;
        }

        // -------------------------------------------------------------------
        // Animator speed helpers — broadcast so all players see the change
        // -------------------------------------------------------------------
        internal static void BoostAttackAnimation(Player player, float speed)
        {
            NetworkedEffects.BroadcastAnimatorSpeed(player, speed);
            _animBoosted = true;
        }

        internal static void RestoreAttackAnimation(Player player)
        {
            if (!_animBoosted) return;
            NetworkedEffects.BroadcastAnimatorSpeed(player, 1f);
            _animBoosted = false;
        }

        public override void Stop()
        {
            base.Stop();

            var player = Player.m_localPlayer;
            if (player == null) return;

            // Restore exact saved baseline speeds
            if (_savedSpeed > 0f) player.m_speed = _savedSpeed;
            if (_savedRunSpeed > 0f) player.m_runSpeed = _savedRunSpeed;
            _savedSpeed = 0f;
            _savedRunSpeed = 0f;

            // Restore animator speed on all clients
            RestoreAttackAnimation(player);

            Plugin.Log.LogInfo("Frenzy expired — speed restored.");
        }

        public override string GetTooltipString()
        {
            return $"<color=red>+{AttackSpeedBonus * 100f:F0}% Attack Speed</color>\n" +
                   $"<color=orange>+{MoveSpeedBonus * 100f:F0}% Move Speed</color>";
        }
    }

    // -----------------------------------------------------------------------
    // Speed up the caster's animator at the start of each of THEIR attacks
    // while Frenzy is active, broadcast to all players.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    public static class Attack_Start_FrenzySpeed_Patch
    {
        static void Prefix(Attack __instance)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            // Only the local player's own attacks — not enemies or others
            if (__instance.m_character != player) return;

            var buff = SE_AxeSkillBuff.GetActiveBuff(player);
            if (buff == null) return;

            SE_AxeSkillBuff.BoostAttackAnimation(player, 1f + buff.AttackSpeedBonus);
        }
    }

    // -----------------------------------------------------------------------
    // When the caster's attack ends and Frenzy is gone, restore normal
    // animator speed on all clients (only if it was boosted).
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Attack), nameof(Attack.Stop))]
    public static class Attack_Stop_FrenzySpeed_Patch
    {
        static void Postfix(Attack __instance)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            // Only the local player's own attacks
            if (__instance.m_character != player) return;

            // If buff is still active keep the speed boosted
            if (SE_AxeSkillBuff.GetActiveBuff(player) != null) return;

            SE_AxeSkillBuff.RestoreAttackAnimation(player);
        }
    }
}