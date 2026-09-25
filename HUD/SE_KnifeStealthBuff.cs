using UnityEngine;

namespace KeenCombat.HUD
{
    // -----------------------------------------------------------------------
    // SE_KnifeStealthBuff — Assassination Stealth
    //
    // Shows a buff icon while the player is in stealth mode.
    // Duration is driven by KnifeSkill — the SE is added on stealth entry
    // and removed on stealth exit or strike.
    // -----------------------------------------------------------------------
    public class SE_KnifeStealthBuff : StatusEffect
    {
        public const string StatusEffectName = "SE_AssassinationStealth";

        private static SE_KnifeStealthBuff? _instance;

        public static void Register()
        {
            if (_instance != null) return;

            _instance = ScriptableObject.CreateInstance<SE_KnifeStealthBuff>();
            _instance.name = StatusEffectName;
            _instance.m_name = "Stealth";
            // Shared loader — finds the icon in every supported install layout
            _instance.m_icon = Plugin.LoadEmbeddedSprite("AssassinationBuffIcon.png");
            _instance.m_startMessageType = MessageHud.MessageType.TopLeft;
            _instance.m_startMessage = "Vanished into the shadows!";
            _instance.m_stopMessageType = MessageHud.MessageType.TopLeft;
            _instance.m_stopMessage = "Stealth broken.";

            if (ObjectDB.instance != null && !ObjectDB.instance.m_StatusEffects.Contains(_instance))
            {
                ObjectDB.instance.m_StatusEffects.Add(_instance);
                Plugin.Log.LogInfo("SE_KnifeStealthBuff registered in ObjectDB.");
            }
        }

        public static void Apply(Player player, float duration)
        {
            if (_instance == null)
            {
                Plugin.Log.LogWarning("SE_KnifeStealthBuff not registered yet.");
                return;
            }

            _instance.m_ttl = duration;
            player.GetSEMan().AddStatusEffect(_instance, true, 0, 0f);
        }

        public static void Remove(Player player)
        {
            var seman = player.GetSEMan();
            var buff = seman.GetStatusEffect(StatusEffectName.GetStableHashCode());
            if (buff != null)
                seman.RemoveStatusEffect(buff);
        }

        public static bool IsActive(Player player)
        {
            return player.GetSEMan()
                         .HaveStatusEffect(StatusEffectName.GetStableHashCode());
        }

        public override string GetIconText()
        {
            var field = typeof(StatusEffect).GetField("m_time",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field == null) return "";
            float elapsed = (float)field.GetValue(this);
            float remaining = Mathf.Max(0f, m_ttl - elapsed);
            if (remaining <= 0.5f) return "";
            return $"{Mathf.CeilToInt(remaining)}s";
        }

        public override string GetTooltipString()
        {
            var field = typeof(StatusEffect).GetField("m_time",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            float elapsed = field != null ? (float)field.GetValue(this) : 0f;
            float remaining = Mathf.Max(0f, m_ttl - elapsed);
            return $"<color=grey>In stealth — strike to unleash!</color>\n" +
                   $"<color=orange>{remaining:F1}s remaining</color>";
        }
    }
}