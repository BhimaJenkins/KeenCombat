using HarmonyLib;
using UnityEngine;

namespace KeenCombat.HUD
{
    // -----------------------------------------------------------------------
    // SE_AxeSkillBuff — Frenzy
    //
    // Attack speed applied by setting animator.speed in Attack.Start prefix.
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

        public static void Register()
        {
            if (_instance != null) return;

            _instance = ScriptableObject.CreateInstance<SE_AxeSkillBuff>();
            _instance.name = StatusEffectName;
            _instance.m_name = "Frenzy";
            _instance.m_icon = LoadBuffIcon();
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

        private static Sprite? LoadBuffIcon()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "icons", "FrenzyBuffIcon.png");

            if (!System.IO.File.Exists(path))
            {
                Plugin.Log.LogWarning($"Frenzy buff icon not found: {path}");
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

            player.GetSEMan().AddStatusEffect(_instance);
            Plugin.Log.LogInfo($"Frenzy applied: +{attackSpeedBonus * 100f}% atk speed, " +
                               $"+{moveSpeedBonus * 100f}% move speed for {duration}s");
        }

        public static SE_AxeSkillBuff? GetActiveBuff(Character character)
        {
            return character.GetSEMan()
                            .GetStatusEffect(StatusEffectName.GetStableHashCode())
                            as SE_AxeSkillBuff;
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

            // Restore animator speed
            var animator = player.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.speed = 1f;

            Plugin.Log.LogInfo("Frenzy expired — speed restored.");
        }

        public override string GetTooltipString()
        {
            return $"<color=red>+{AttackSpeedBonus * 100f:F0}% Attack Speed</color>\n" +
                   $"<color=orange>+{MoveSpeedBonus * 100f:F0}% Move Speed</color>";
        }
    }

    // -----------------------------------------------------------------------
    // Set animator.speed when Frenzy is active at the start of each attack.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    public static class Attack_Start_FrenzySpeed_Patch
    {
        static void Prefix(Attack __instance)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var buff = SE_AxeSkillBuff.GetActiveBuff(player);
            if (buff == null) return;

            var animator = player.GetComponentInChildren<Animator>();
            if (animator == null) return;

            animator.speed = 1f + buff.AttackSpeedBonus;
        }
    }

    // -----------------------------------------------------------------------
    // Restore animator.speed to 1f when an attack ends and buff is gone.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Attack), nameof(Attack.Stop))]
    public static class Attack_Stop_FrenzySpeed_Patch
    {
        static void Postfix()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            // If buff is still active keep the speed boosted
            var buff = SE_AxeSkillBuff.GetActiveBuff(player);
            if (buff != null) return;

            var animator = player.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.speed = 1f;
        }
    }
}