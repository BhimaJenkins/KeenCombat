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
            _instance.m_icon = LoadBuffIcon();
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

        private static Sprite? LoadBuffIcon()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "icons", "AssassinationBuffIcon.png");

            if (!System.IO.File.Exists(path))
            {
                Plugin.Log.LogWarning($"Stealth buff icon not found: {path}");
                return null;
            }

            byte[] data = System.IO.File.ReadAllBytes(path);
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);

            System.Reflection.MethodInfo? loadImg = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("UnityEngine.ImageConversion");
                if (t == null) continue;
                loadImg = t.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
                if (loadImg != null) break;
            }

            if (loadImg == null) return null;

            loadImg.Invoke(null, new object[] { tex, data });
            tex.Apply();

            return Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        public static void Apply(Player player, float duration)
        {
            if (_instance == null)
            {
                Plugin.Log.LogWarning("SE_KnifeStealthBuff not registered yet.");
                return;
            }

            _instance.m_ttl = duration;
            player.GetSEMan().AddStatusEffect(_instance);
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