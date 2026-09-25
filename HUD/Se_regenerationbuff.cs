using UnityEngine;

namespace KeenCombat.HUD
{
    // -----------------------------------------------------------------------
    // SE_RegenerationBuff
    //
    // Applied to all friendly characters in range by StaffGreenRootsSkill.
    // Heals a configurable % of max HP per second over a configurable duration.
    // Refreshes timer if cast again before expiry.
    // -----------------------------------------------------------------------
    public class SE_RegenerationBuff : StatusEffect
    {
        public const string StatusEffectName = "SE_RegenerationBuff";

        private static SE_RegenerationBuff? _instance;

        public float HealPerSecond = 0f;
        public float TotalDuration = 6f;

        private float _healAccumulator = 0f;

        public static void Register()
        {
            if (_instance != null) return;

            _instance = ScriptableObject.CreateInstance<SE_RegenerationBuff>();
            _instance.name = StatusEffectName;
            _instance.m_name = "Regeneration";
            _instance.m_icon = LoadBuffIcon();
            _instance.m_startMessageType = MessageHud.MessageType.TopLeft;
            _instance.m_startMessage = "Regeneration!";
            _instance.m_stopMessageType = MessageHud.MessageType.TopLeft;
            _instance.m_stopMessage = "Regeneration faded.";

            if (ObjectDB.instance != null && !ObjectDB.instance.m_StatusEffects.Contains(_instance))
            {
                ObjectDB.instance.m_StatusEffects.Add(_instance);
                Plugin.Log.LogInfo("SE_RegenerationBuff registered in ObjectDB.");
            }
        }

        private static Sprite? LoadBuffIcon()
        {
            return Plugin.LoadEmbeddedSprite("RegenerationBuffIcon.png");
        }

        /// <summary>
        /// Apply or refresh the regeneration buff on a character.
        /// healPercent: total % of max HP to heal (e.g. 0.30 = 30%)
        /// duration: time in seconds over which healing occurs
        /// </summary>
        public static void Apply(Character character, float healPercent, float duration)
        {
            if (_instance == null)
            {
                Plugin.Log.LogWarning("SE_RegenerationBuff not registered yet.");
                return;
            }

            float healPerSecond = (character.GetMaxHealth() * healPercent) / duration;

            // Check if already active — refresh timer and update heal rate
            var existing = character.GetSEMan()
                .GetStatusEffect(StatusEffectName.GetStableHashCode())
                as SE_RegenerationBuff;

            if (existing != null)
            {
                // Refresh — reset time and update heal rate
                existing.m_ttl = duration;
                existing.TotalDuration = duration;
                existing.HealPerSecond = healPerSecond;

                // Reset internal timer via reflection
                var field = typeof(StatusEffect).GetField("m_time",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                field?.SetValue(existing, 0f);
                return;
            }

            // Fresh application
            var newInstance = ScriptableObject.CreateInstance<SE_RegenerationBuff>();
            newInstance.name = StatusEffectName;
            newInstance.m_name = "Regeneration";
            newInstance.m_icon = _instance.m_icon;
            newInstance.m_ttl = duration;
            newInstance.TotalDuration = duration;
            newInstance.HealPerSecond = healPerSecond;
            newInstance.m_startMessage = "";
            newInstance.m_stopMessage = "";

            character.GetSEMan().AddStatusEffect(newInstance, true, 0, 0f);
        }

        public static bool IsActive(Character character)
        {
            return character.GetSEMan()
                .HaveStatusEffect(StatusEffectName.GetStableHashCode());
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            // Accumulate heal and apply in whole HP chunks each second
            _healAccumulator += HealPerSecond * dt;

            if (_healAccumulator >= 1f)
            {
                float toHeal = Mathf.Floor(_healAccumulator);
                _healAccumulator -= toHeal;

                if (m_character != null)
                    m_character.Heal(toHeal);
            }
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
            return $"<color=green>Regenerating {HealPerSecond:F1} HP/sec</color>\n" +
                   $"<color=lime>{remaining:F1}s remaining</color>";
        }
    }
}