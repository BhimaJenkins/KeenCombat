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

            // Load 32x32 buff bar icon from icons folder
            _instance.m_icon = LoadBuffIcon();

            if (ObjectDB.instance != null && !ObjectDB.instance.m_StatusEffects.Contains(_instance))
            {
                ObjectDB.instance.m_StatusEffects.Add(_instance);
                Plugin.Log.LogInfo("SE_MaceSkillBuff registered in ObjectDB.");
            }
        }

        private static Sprite? LoadBuffIcon()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "icons", "Bulwark_BuffIcon.png");

            if (!System.IO.File.Exists(path))
            {
                Plugin.Log.LogWarning($"Bulwark buff icon not found: {path}");
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

            if (loadImg == null)
            {
                Plugin.Log.LogWarning("UnityEngine.ImageConversion.LoadImage not found.");
                return null;
            }

            loadImg.Invoke(null, new object[] { tex, data });
            tex.Apply();

            return Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
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

            player.GetSEMan().AddStatusEffect(_instance);
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

    [HarmonyPatch(typeof(Player), nameof(Player.Update))]
    public static class Player_BulwarkRegen_Patch
    {
        private static float _regenTimer = 0f;

        static void Postfix(Player __instance)
        {
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