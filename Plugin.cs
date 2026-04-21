using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace KeenCombat
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "Bhimas.KeenCombat";
        public const string PluginName = "KeenCombat";
        public const string PluginVersion = "0.1.2";

        internal static ManualLogSource Log = null!;
        internal static Plugin instance = null!;

        internal static ConfigEntry<bool> AutoShieldEnabled = null!;
        internal static ConfigEntry<bool> ExcludeAxe = null!;
        internal static ConfigEntry<bool> ExcludeKnife = null!;
        internal static ConfigEntry<float> HoldThreshold = null!;
        internal static ConfigEntry<KeyCode> SkillKey = null!;
        internal static ConfigEntry<float> SkillWidgetX = null!;
        internal static ConfigEntry<float> SkillWidgetY = null!;

        // Sword Blink Strike
        internal static ConfigEntry<float> SwordSkillCooldown = null!;
        internal static ConfigEntry<float> SwordSkillDamage = null!;
        internal static ConfigEntry<float> SwordBlinkDistance = null!;
        internal static ConfigEntry<float> SwordAnimSpeed = null!;
        internal static ConfigEntry<string> SwordAnimName = null!;
        internal static ConfigEntry<float> SwordHitRadius = null!;
        internal static ConfigEntry<float> SwordHitExtendFront = null!;
        internal static ConfigEntry<float> SwordHitExtendBack = null!;
        internal static ConfigEntry<float> SwordHitHeightTop = null!;
        internal static ConfigEntry<float> SwordHitHeightBottom = null!;

        // Mace Bulwark
        internal static ConfigEntry<float> MaceSkillCooldown = null!;
        internal static ConfigEntry<float> MaceBuffDuration = null!;
        internal static ConfigEntry<float> MaceDamageReduction = null!;
        internal static ConfigEntry<float> MaceRegenPerSec = null!;

        // Axe Frenzy
        internal static ConfigEntry<float> AxeSkillCooldown = null!;
        internal static ConfigEntry<float> AxeBuffDuration = null!;
        internal static ConfigEntry<float> AxeAttackSpeedBonus = null!;
        internal static ConfigEntry<float> AxeMoveSpeedBonus = null!;

        // Atgeir/Spear Falcon Blitz
        internal static ConfigEntry<float> AtgeirSkillCooldown = null!;
        internal static ConfigEntry<float> AtgeirSkillDamage = null!;

        // Crossbow Rapid Fire
        internal static ConfigEntry<float> CrossbowSkillCooldown = null!;
        internal static ConfigEntry<float> CrossbowSkillDamage = null!;

        // Knife Assassination
        internal static ConfigEntry<float> KnifeSkillCooldown = null!;
        internal static ConfigEntry<float> KnifeSkillDamage = null!;
        internal static ConfigEntry<float> KnifeStealthDuration = null!;

        // Greatsword Whirlwind
        internal static ConfigEntry<float> GreatswordSkillCooldown = null!;
        internal static ConfigEntry<float> GreatswordSkillDamage = null!;
        internal static ConfigEntry<float> GreatswordAnimSpeed = null!;
        internal static ConfigEntry<float> GreatswordStartSpeed = null!;
        internal static ConfigEntry<float> GreatswordHoldDuration = null!;
        internal static ConfigEntry<float> GreatswordSecondSpin = null!;
        internal static ConfigEntry<float> GreatswordMoveDistance = null!;

        // Bow Primal Rally
        internal static ConfigEntry<float> BowSkillCooldown = null!;
        internal static ConfigEntry<float> BowSummonDuration = null!;
        internal static ConfigEntry<string> BowCreatureMap = null!;

        // Dual Knife Poison Mayhem
        internal static ConfigEntry<float> DualKnifeSkillCooldown = null!;
        internal static ConfigEntry<float> DualKnifeSkillDamage = null!;
        internal static ConfigEntry<float> DualKnifeLeapBack = null!;
        internal static ConfigEntry<float> DualKnifeLeapUp = null!;
        internal static ConfigEntry<int> DualKnifeNumBombs = null!;
        internal static ConfigEntry<float> DualKnifeSpread = null!;

        // Fist Onslaught
        internal static ConfigEntry<float> FistSkillCooldown = null!;
        internal static ConfigEntry<float> FistSkillDamage = null!;

        // 2H Mace Earthquake
        internal static ConfigEntry<float> TwoHandedMaceSkillCooldown = null!;
        internal static ConfigEntry<float> TwoHandedMaceSkillDamage = null!;

        // Debug
        internal static ConfigEntry<bool> TestMode = null!;
        internal static ConfigEntry<float> GlobalAttackSpeed = null!;

        internal static InputAction? RightTriggerAction = null;

        private Harmony _harmony = null!;

        private sealed class ConfigurationManagerAttributes
        {
            public bool? Browsable;
        }

        private void Awake()
        {
            instance = this;
            Log = Logger;
            Log.LogInfo($"{PluginName} v{PluginVersion} loading...");

            BindConfig();

            RightTriggerAction = new InputAction(
                name: "RightTrigger",
                type: InputActionType.Value,
                binding: "<Gamepad>/rightTrigger");
            RightTriggerAction.Enable();

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} loaded successfully.");
        }

        private void BindConfig()
        {
            var hidden = new ConfigurationManagerAttributes { Browsable = false };

            // Shield
            AutoShieldEnabled = Config.Bind("Shield", "AutoEquipShield", true,
                "Automatically equip the best shield when equipping a one-handed weapon.");
            ExcludeAxe = Config.Bind("Shield", "ExcludeAxe", true,
                "Don't auto-equip a shield when equipping an axe.");
            ExcludeKnife = Config.Bind("Shield", "ExcludeKnife", true,
                "Don't auto-equip a shield when equipping a knife or dual knife.");

            HoldThreshold = Config.Bind("Input", "HoldThreshold", 0.25f,
                new ConfigDescription("Seconds to hold Attack before triggering heavy attack.", null, hidden));
            SkillKey = Config.Bind("Input", "SkillKey", KeyCode.Mouse2,
                new ConfigDescription("Key to activate the equipped weapon's special skill.", null, hidden));

            // Sword Blink Strike
            SwordSkillCooldown = Config.Bind("Sword Blink Strike", "Cooldown", 15f,
                "Cooldown in seconds for Sword Blink Strike.");
            SwordSkillDamage = Config.Bind("Sword Blink Strike", "DamageMultiplier", 1.1f,
                "Damage multiplier (1.0 = weapon base damage).");
            SwordBlinkDistance = Config.Bind("Sword Blink Strike", "BlinkDistance", 8f,
                new ConfigDescription("Maximum blink distance in meters.", null, hidden));
            SwordAnimSpeed = Config.Bind("Sword Blink Strike", "AnimationSpeed", 1.2f,
                new ConfigDescription("Speed multiplier for the blink animation.", null, hidden));
            SwordAnimName = Config.Bind("Sword Blink Strike", "AnimationName", "battleaxe_secondary",
                new ConfigDescription("Animator state name for the blink animation.", null, hidden));
            SwordHitRadius = Config.Bind("Sword Blink Strike", "HitboxRadius", 2.5f,
                new ConfigDescription("Width (radius in meters) of the blink damage capsule.", null, hidden));
            SwordHitExtendFront = Config.Bind("Sword Blink Strike", "HitboxExtendFront", 3.0f,
                new ConfigDescription("How far the hitbox extends past the blink end point.", null, hidden));
            SwordHitExtendBack = Config.Bind("Sword Blink Strike", "HitboxExtendBack", 2.0f,
                new ConfigDescription("How far the hitbox extends behind the blink start point.", null, hidden));
            SwordHitHeightTop = Config.Bind("Sword Blink Strike", "HitboxHeightTop", 4.2f,
                new ConfigDescription("Height of the hitbox above the end position.", null, hidden));
            SwordHitHeightBottom = Config.Bind("Sword Blink Strike", "HitboxHeightBottom", 2.0f,
                new ConfigDescription("How far below ground the hitbox extends.", null, hidden));

            // Mace Bulwark
            MaceSkillCooldown = Config.Bind("Mace Bulwark", "Cooldown", 30f,
                "Cooldown in seconds for Bulwark.");
            MaceBuffDuration = Config.Bind("Mace Bulwark", "BuffDuration", 10f,
                "Duration of the Mace Bulwark buff in seconds.");
            MaceDamageReduction = Config.Bind("Mace Bulwark", "DamageReduction", 0.20f,
                "Damage reduction while Bulwark is active (0.20 = 20% less damage taken).");
            MaceRegenPerSec = Config.Bind("Mace Bulwark", "RegenPerSec", 5f,
                "HP regeneration per second granted by Bulwark.");

            // Axe Frenzy
            AxeSkillCooldown = Config.Bind("Axe Frenzy", "Cooldown", 25f,
                "Cooldown in seconds for Frenzy.");
            AxeBuffDuration = Config.Bind("Axe Frenzy", "BuffDuration", 8f,
                "Duration of the Axe Frenzy buff in seconds.");
            AxeAttackSpeedBonus = Config.Bind("Axe Frenzy", "AttackSpeedBonus", 0.30f,
                "Attack speed bonus while Frenzy is active (0.30 = 30% faster attacks).");
            AxeMoveSpeedBonus = Config.Bind("Axe Frenzy", "MoveSpeedBonus", 0.20f,
                "Move speed bonus while Frenzy is active (0.20 = 20% faster movement).");

            // Atgeir/Spear Falcon Blitz
            AtgeirSkillCooldown = Config.Bind("Atgeir Falcon Blitz", "Cooldown", 20f,
                "Cooldown in seconds for Falcon Blitz.");
            AtgeirSkillDamage = Config.Bind("Atgeir Falcon Blitz", "DamageMultiplier", 0.50f,
                "Damage multiplier per hit (0.50 = 50% of normal attack damage).");

            // Crossbow Rapid Fire
            CrossbowSkillCooldown = Config.Bind("Crossbow Rapid Fire", "Cooldown", 20f,
                "Cooldown in seconds for Rapid Fire.");
            CrossbowSkillDamage = Config.Bind("Crossbow Rapid Fire", "DamageMultiplier", 0.30f,
                "Damage multiplier per shot (0.30 = 30% of normal bolt damage).");

            // Knife Assassination
            KnifeSkillCooldown = Config.Bind("Knife Assassination", "Cooldown", 20f,
                "Cooldown in seconds for Assassination.");
            KnifeSkillDamage = Config.Bind("Knife Assassination", "DamageMultiplier", 2.0f,
                "Damage multiplier for the assassination strike (2.0 = 2x normal damage).");
            KnifeStealthDuration = Config.Bind("Knife Assassination", "StealthDuration", 8f,
                "How long stealth lasts before expiring (seconds).");

            // Greatsword Whirlwind
            GreatswordSkillCooldown = Config.Bind("Greatsword Whirlwind", "Cooldown", 15f,
                "Cooldown in seconds for Whirlwind.");
            GreatswordSkillDamage = Config.Bind("Greatsword Whirlwind", "DamageMultiplier", 0.80f,
                "Damage multiplier per spin hit (0.80 = 80% of normal weapon damage).");
            GreatswordStartSpeed = Config.Bind("Greatsword Whirlwind", "StartSpeed", 1.0f,
                new ConfigDescription("Starting animation speed for the whirlwind.", null, hidden));
            GreatswordAnimSpeed = Config.Bind("Greatsword Whirlwind", "PeakAnimSpeed", 4.0f,
                new ConfigDescription("Peak animation speed for the whirlwind spin.", null, hidden));
            GreatswordHoldDuration = Config.Bind("Greatsword Whirlwind", "SpinDuration", 1.2f,
                new ConfigDescription("Duration of the first spin in seconds.", null, hidden));
            GreatswordSecondSpin = Config.Bind("Greatsword Whirlwind", "SecondSpinDuration", 1.0f,
                new ConfigDescription("Duration of the second spin in seconds.", null, hidden));
            GreatswordMoveDistance = Config.Bind("Greatsword Whirlwind", "MoveDistance", 10.0f,
                new ConfigDescription("How far the player moves forward during Whirlwind (meters).", null, hidden));

            // Bow Primal Rally
            BowSkillCooldown = Config.Bind("Bow Primal Rally", "Cooldown", 120f,
                "Cooldown in seconds for Primal Rally (default 2 minutes).");
            BowSummonDuration = Config.Bind("Bow Primal Rally", "SummonDuration", 60f,
                "How long the summoned creature lasts before despawning (seconds).");
            BowCreatureMap = Config.Bind("Bow Primal Rally", "BowCreatureMap",
                "$item_bow:Boar:0," +
                "$item_bow_finewood:Boar:2," +
                "$item_bow_huntsman:Wolf:0," +
                "$item_bow_draugrfang:BlobElite:0," +
                "$item_bow_snipesnap:Seeker:0," +
                "$item_bow_ashlands:Asksvin:0," +
                "$item_bow_ashlandsroot:Asksvin:0," +
                "$item_bow_ashlandsblood:Asksvin:0," +
                "$item_bow_ashlandsstorm:Asksvin:0," +
                "$bow_trollbone_tw:Troll:0," +
                "$bow_blackmetal_tw:Lox:0," +
                "$greatbow_blackmetal_tw:Lox:1," +
                "$greatbow_dvergr_tw:Dverger:0," +
                "$greatbow_moder_tw:Hatchling:2",
                "Comma-separated bow→creature mappings. Format: bowItemName:CreaturePrefab:starLevel.");

            // Dual Knife Poison Mayhem
            DualKnifeSkillCooldown = Config.Bind("Dual Knife Poison Mayhem", "Cooldown", 20f,
                "Cooldown in seconds for Poison Mayhem.");
            DualKnifeSkillDamage = Config.Bind("Dual Knife Poison Mayhem", "DamageMultiplier", 0.40f,
                "Damage multiplier per bomb hit (0.40 = 40% of weapon damage, applied as direct + poison).");
            DualKnifeLeapBack = Config.Bind("Dual Knife Poison Mayhem", "LeapBackSpeed", 4.5f,
                new ConfigDescription("Backward leap distance in meters.", null, hidden));
            DualKnifeLeapUp = Config.Bind("Dual Knife Poison Mayhem", "LeapUpSpeed", 1.25f,
                new ConfigDescription("Upward leap height in meters.", null, hidden));
            DualKnifeNumBombs = Config.Bind("Dual Knife Poison Mayhem", "NumBombs", 3,
                new ConfigDescription("Number of ooze bombs dropped.", null, hidden));
            DualKnifeSpread = Config.Bind("Dual Knife Poison Mayhem", "BombSpread", 1.5f,
                new ConfigDescription("Spread radius of the bomb cluster in meters.", null, hidden));

            // Fist Onslaught
            FistSkillCooldown = Config.Bind("Fist Onslaught", "Cooldown", 20f,
                "Cooldown in seconds for Onslaught.");
            FistSkillDamage = Config.Bind("Fist Onslaught", "DamageMultiplier", 1.0f,
                "Base damage multiplier (1.0 = normal fist damage). Combo hits use 50%, finisher uses 100%.");

            // 2H Mace Earthquake
            TwoHandedMaceSkillCooldown = Config.Bind("2H Mace Earthquake", "Cooldown", 15f,
                "Cooldown in seconds for Earthquake.");
            TwoHandedMaceSkillDamage = Config.Bind("2H Mace Earthquake", "DamageMultiplier", 0.60f,
                "Damage multiplier per stomp explosion (0.60 = 60% of normal weapon damage).");

            // Debug
            TestMode = Config.Bind("Debug", "TestMode", false,
                new ConfigDescription("When true, enemies hit by skills survive at 1hp.", null, hidden));
            GlobalAttackSpeed = Config.Bind("Debug", "GlobalAttackSpeed", 1.0f,
                "Global attack animation speed multiplier. 1.0 = normal.");

            // HUD
            SkillWidgetX = Config.Bind("HUD", "SkillWidgetX", 605f,
                new ConfigDescription("Horizontal position of the skill slot widget.", null, hidden));
            SkillWidgetY = Config.Bind("HUD", "SkillWidgetY", -44f,
                new ConfigDescription("Vertical position of the skill slot widget.", null, hidden));
        }

        internal static Sprite? LoadEmbeddedSprite(string filename)
        {
            string pluginDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location)!;

            // Try subfolder first (manual install), fall back to flat (R2ModMan)
            string path = System.IO.Path.Combine(pluginDir, "icons", filename);
            if (!System.IO.File.Exists(path))
                path = System.IO.Path.Combine(pluginDir, filename);

            if (!System.IO.File.Exists(path))
            {
                Log.LogWarning($"Icon file not found: {filename}");
                return null;
            }

            byte[] data = System.IO.File.ReadAllBytes(path);
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);

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
                Log.LogWarning("UnityEngine.ImageConversion.LoadImage not found.");
                return null;
            }

            loadImg.Invoke(null, new object[] { tex, data });
            tex.Apply();

            return Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        internal static IEnumerator LoadAudioClip(string filename,
                                                   System.Action<AudioClip?> onLoaded)
        {
            string pluginDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location)!;

            // Try subfolder first (manual install), fall back to flat (R2ModMan)
            string path = System.IO.Path.Combine(pluginDir, "audio", filename);
            if (!System.IO.File.Exists(path))
                path = System.IO.Path.Combine(pluginDir, filename);

            if (!System.IO.File.Exists(path))
            {
                Log.LogWarning($"Audio file not found: {filename}");
                onLoaded(null);
                yield break;
            }

            string uri = "file:///" + path.Replace("\\", "/");

            using var request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.LogWarning($"Failed to load audio clip: {filename} — {request.error}");
                onLoaded(null);
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            clip.name = System.IO.Path.GetFileNameWithoutExtension(filename);
            onLoaded(clip);
        }
    }
}