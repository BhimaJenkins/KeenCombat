using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace KeenCombat.HUD
{
    public class SE_SkillCooldown : StatusEffect
    {
        private static readonly Dictionary<string, SE_SkillCooldown> _instances
            = new Dictionary<string, SE_SkillCooldown>();

        public float GetRemainingTime()
        {
            var field = typeof(StatusEffect).GetField("m_time",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field == null) return 0f;
            float elapsed = (float)field.GetValue(this);
            return Mathf.Max(0f, m_ttl - elapsed);
        }

        public static void Register(string seName)
        {
            if (_instances.ContainsKey(seName)) return;

            var instance = ScriptableObject.CreateInstance<SE_SkillCooldown>();
            instance.name = seName;
            instance.m_name = "Skill Cooldown";
            instance.m_icon = null;
            instance.m_startMessageType = MessageHud.MessageType.TopLeft;
            instance.m_startMessage = "";
            instance.m_stopMessageType = MessageHud.MessageType.TopLeft;
            instance.m_stopMessage = "Skill Ready!";

            if (ObjectDB.instance != null && !ObjectDB.instance.m_StatusEffects.Contains(instance))
            {
                ObjectDB.instance.m_StatusEffects.Add(instance);
                _instances[seName] = instance;
            }
        }

        public static void Apply(Player player, float duration, string seName)
        {
            if (!_instances.TryGetValue(seName, out var instance))
            {
                Plugin.Log.LogWarning($"SE_SkillCooldown not registered: {seName}");
                return;
            }
            instance.m_ttl = duration;
            player.GetSEMan().AddStatusEffect(instance, true, 0, 0f);
        }

        public static bool IsOnCooldown(Player player, string seName)
        {
            return player.GetSEMan().HaveStatusEffect(seName.GetStableHashCode());
        }

        public static SE_SkillCooldown? GetActive(Player player, string seName)
        {
            return player.GetSEMan()
                         .GetStatusEffect(seName.GetStableHashCode())
                         as SE_SkillCooldown;
        }

        public override string GetIconText()
        {
            float remaining = GetRemainingTime();
            if (remaining <= 0.5f) return "";
            return $"{Mathf.CeilToInt(remaining)}s";
        }

        public override string GetTooltipString()
        {
            float remaining = GetRemainingTime();
            if (remaining > 0)
                return $"{m_name}\n<color=orange>{remaining:F1}s remaining</color>";
            else
                return $"{m_name}\n<color=green>Ready!</color>";
        }
    }

    // -----------------------------------------------------------------------
    // Register NetworkedEffects RPCs for each world session. Safe to call
    // repeatedly — it only registers once per ZRoutedRpc instance.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Game), "Start")]
    public static class Game_Start_RegisterEffects
    {
        static void Postfix()
        {
            NetworkedEffects.Register();
        }
    }

    // -----------------------------------------------------------------------
    // Register all SE cooldowns, buffs and NetworkedEffects RPCs
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    public static class ObjectDB_Awake_Patch
    {
        static void Postfix(ObjectDB __instance)
        {
            SE_SkillCooldown.Register("SE_Cooldown_Sword");
            SE_SkillCooldown.Register("SE_Cooldown_Mace");
            SE_SkillCooldown.Register("SE_Cooldown_Axe");
            SE_SkillCooldown.Register("SE_Cooldown_Atgeir");
            SE_SkillCooldown.Register("SE_Cooldown_Crossbow");
            SE_SkillCooldown.Register("SE_Cooldown_Knife");
            SE_SkillCooldown.Register("SE_Cooldown_Greatsword");
            SE_SkillCooldown.Register("SE_Cooldown_Bow");
            SE_SkillCooldown.Register("SE_Cooldown_DualKnife");
            SE_SkillCooldown.Register("SE_Cooldown_Fist");
            SE_SkillCooldown.Register("SE_Cooldown_TwoHandedMace");

            SE_MaceSkillBuff.Register();
            SE_AxeSkillBuff.Register();
            SE_KnifeStealthBuff.Register();
            SE_RegenerationBuff.Register();
            SE_ThunderstruckBuff.Register();

            NetworkedEffects.Register();
        }
    }

    [HarmonyPatch(typeof(KeyHints), "Start")]
    public static class KeyHints_Start_Patch
    {
        static void Postfix(KeyHints __instance)
        {
            Plugin.Log.LogInfo("KeyHints.Start — KeenCombat loaded.");
        }
    }
}