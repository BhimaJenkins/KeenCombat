using HarmonyLib;
using UnityEngine;

namespace KeenCombat.HUD
{
    public class SE_MaceSkillBuff : StatusEffect
    {
        public const string StatusEffectName = "SE_BulwarkBuff";

        private static SE_MaceSkillBuff? _instance;

        public float DamageReduction = 0.20f;
        public float RegenPerSec = 5f;

        public static void Register()
        {
            if (_instance != null) return;

            _instance = ScriptableObject.CreateInstance<SE_MaceSkillBuff>();
            _instance.name = StatusEffectName;
            _instance.m_name = "Bulwark";
            _instance.m_startMessageType = MessageHud.MessageType.TopLeft;
            _instance.m_startMessage = "Bulwark activated!";
            _instance.m_stopMessageType = MessageHud.MessageType.TopLeft;
            _instance.m_stopMessage = "Bulwark faded.";

            // Shared loader — finds the icon in every supported install layout
            _instance.m_icon = Plugin.LoadEmbeddedSprite("Bulwark_BuffIcon.png");

            if (ObjectDB.instance != null && !ObjectDB.instance.m_StatusEffects.Contains(_instance))
            {
                ObjectDB.instance.m_StatusEffects.Add(_instance);
                Plugin.Log.LogInfo("SE_MaceSkillBuff registered in ObjectDB.");
            }
        }

        public static void Apply(Player player, float duration, float damageReduction, float regenPerSec)
        {
            if (_instance == null)
            {
                Plugin.Log.LogWarning("SE_MaceSkillBuff not registered yet.");
                return;
            }

            _instance.m_ttl = duration;
            _instance.DamageReduction = damageReduction;
            _instance.RegenPerSec = regenPerSec;

            player.GetSEMan().AddStatusEffect(_instance, true, 0, 0f);
            Plugin.Log.LogInfo($"Bulwark applied: {damageReduction * 100f}% dmg reduction, " +
                               $"+{regenPerSec} HP/s for {duration}s");
        }

        public static SE_MaceSkillBuff? GetActiveBuff(Character character)
        {
            return character.GetSEMan()
                            .GetStatusEffect(StatusEffectName.GetStableHashCode())
                            as SE_MaceSkillBuff;
        }

        public override string GetTooltipString()
        {
            return $"<color=cyan>{DamageReduction * 100f:F0}% damage reduction</color>\n" +
                   $"<color=green>+{RegenPerSec} HP/sec</color>";
        }
    }

    // -----------------------------------------------------------------------
    // Damage reduction while Bulwark is active
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class Character_ApplyDamage_Patch
    {
        static void Prefix(Character __instance, ref HitData hit)
        {
            var buff = SE_MaceSkillBuff.GetActiveBuff(__instance);
            if (buff == null) return;

            float reduction = 1f - Mathf.Clamp01(buff.DamageReduction);
            hit.m_damage.Modify(reduction);
        }
    }

    // -----------------------------------------------------------------------
    // Regen while Bulwark is active — LOCAL player only. Player.Update runs
    // for every player on this client, and the timer is shared, so other
    // players' updates would otherwise keep resetting it.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Player), nameof(Player.Update))]
    public static class Player_BulwarkRegen_Patch
    {
        private static float _regenTimer = 0f;

        static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;

            var buff = SE_MaceSkillBuff.GetActiveBuff(__instance);
            if (buff == null)
            {
                _regenTimer = 0f;
                return;
            }

            _regenTimer += Time.deltaTime;
            if (_regenTimer >= 1f)
            {
                _regenTimer = 0f;
                float newHp = Mathf.Min(__instance.GetHealth() + buff.RegenPerSec,
                                        __instance.GetMaxHealth());
                __instance.SetHealth(newHp);
            }
        }
    }
}