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
        public const string PluginVersion = "1.0.1";

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
        internal static ConfigEntry<string> CrossbowProjectileVfx = null!;
        internal static ConfigEntry<float> CrossbowProjectileSpeed = null!;

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
        internal static ConfigEntry<string> CreatureScaleMap = null!;

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
        internal static ConfigEntry<string> TwoHandedMaceSwingOgg = null!;

        // Staff of Embers — Meteor Strike
        internal static ConfigEntry<float> StaffFireballEitrCost = null!;
        internal static ConfigEntry<float> StaffFireballDamageMultiplier = null!;
        internal static ConfigEntry<float> StaffFireballDamageRadius = null!;
        internal static ConfigEntry<float> StaffFireballRadius = null!;
        internal static ConfigEntry<float> StaffFireballDuration = null!;
        internal static ConfigEntry<float> StaffFireballIntervalMin = null!;
        internal static ConfigEntry<float> StaffFireballIntervalMax = null!;

        // Staff of Frost — Ice Nova
        internal static ConfigEntry<float> StaffIceShardsEitrCost = null!;
        internal static ConfigEntry<float> StaffIceShardsDamageMultiplier = null!;
        internal static ConfigEntry<float> StaffIceShardsRadius = null!;
        internal static ConfigEntry<float> StaffIceShardsDuration = null!;
        internal static ConfigEntry<float> StaffIceShardsSlowDuration = null!;

        // Staff of Fracturing — Ragnarök
        internal static ConfigEntry<float> StaffClusterbombMinEitr = null!;
        internal static ConfigEntry<float> StaffClusterbombMaxEitr = null!;
        internal static ConfigEntry<float> StaffClusterbombMaxChargeTime = null!;
        internal static ConfigEntry<float> StaffClusterbombMinDamageMult = null!;
        internal static ConfigEntry<float> StaffClusterbombMaxDamageMult = null!;

        // Staff of the Wild — Regeneration
        internal static ConfigEntry<float> StaffGreenRootsEitrCost = null!;
        internal static ConfigEntry<float> StaffGreenRootsHealPercent = null!;
        internal static ConfigEntry<float> StaffGreenRootsDuration = null!;
        internal static ConfigEntry<float> StaffGreenRootsRadius = null!;

        // Thunderblood Staff — Thunderstruck
        internal static ConfigEntry<float> StaffThunderbloodEitrCost = null!;
        internal static ConfigEntry<float> StaffThunderbloodDamagePercent = null!;
        internal static ConfigEntry<float> StaffThunderbloodRadius = null!;
        internal static ConfigEntry<float> StaffThunderbloodIntervalMin = null!;
        internal static ConfigEntry<float> StaffThunderbloodIntervalMax = null!;
        internal static ConfigEntry<float> StaffThunderbloodDuration = null!;
        internal static ConfigEntry<string> StaffThunderbloodCastVfx = null!;
        internal static ConfigEntry<string> StaffThunderbloodCastSfx = null!;
        internal static ConfigEntry<string> StaffThunderbloodStrikeVfx = null!;
        internal static ConfigEntry<string> StaffThunderbloodStrikeSfx = null!;
        internal static ConfigEntry<float> StaffThunderbloodStrikeHeight = null!;

        // Shield Staff — Sanctuary
        internal static ConfigEntry<float> StaffShieldEitrCost = null!;
        internal static ConfigEntry<float> StaffShieldHealthPercent = null!;
        internal static ConfigEntry<float> StaffShieldRadius = null!;
        internal static ConfigEntry<float> StaffShieldDuration = null!;
        internal static ConfigEntry<float> StaffShieldSlowPercent = null!;
        internal static ConfigEntry<string> StaffShieldCastVfx = null!;
        internal static ConfigEntry<string> StaffShieldCastSfx = null!;
        internal static ConfigEntry<string> StaffShieldDomeVfx = null!;
        internal static ConfigEntry<string> StaffShieldDomeColor = null!;
        internal static ConfigEntry<float> StaffShieldDomeHeightOffset = null!;
        internal static ConfigEntry<float> StaffShieldDomeInsideBoost = null!;
        internal static ConfigEntry<float> StaffShieldCastVfxHeight = null!;
        internal static ConfigEntry<float> StaffShieldBubbleDelay = null!;
        internal static ConfigEntry<float> StaffShieldDomeFadeTime = null!;

        // Magic Cost Scaling
        internal static ConfigEntry<bool> MagicCostScalingEnabled = null!;
        internal static ConfigEntry<float> MagicCostPenaltyPerStep = null!;
        internal static ConfigEntry<float> MagicCostLevelsPerStep = null!;

        // Skeleton Staff — Charred Requiem
        internal static ConfigEntry<string> StaffSkeletonLevelTiers = null!;
        internal static ConfigEntry<float> StaffSkeletonDuration = null!;
        internal static ConfigEntry<string> StaffSkeletonPrefab = null!;

        // Debug
        internal static ConfigEntry<bool> TestMode = null!;
        internal static ConfigEntry<float> GlobalAttackSpeed = null!;

        internal static InputAction? RightTriggerAction = null;

        private Harmony _harmony = null!;

        // Shared so PatchGuard can re-apply patches another mod removed
        internal static Harmony? HarmonyInstance;

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
            HarmonyInstance = _harmony;

            // Re-apply our input patches if another mod removes them
            StartCoroutine(Patches.PatchGuard.Loop());

            Log.LogInfo($"{PluginName} loaded successfully.");
        }

        private void BindConfig()
        {
            var hidden = new ConfigurationManagerAttributes { Browsable = false };

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
            CrossbowProjectileVfx = Config.Bind("Crossbow Rapid Fire", "ProjectileVfx", "auto",
                "Visible projectile for each shot — damage lands when it arrives. " +
                "'auto' = the bolt you have loaded (or a default bolt if none). " +
                "Enter a prefab name to use a specific projectile, or leave blank for instant hitscan.");
            CrossbowProjectileSpeed = Config.Bind("Crossbow Rapid Fire", "ProjectileSpeed", 80f,
                "Projectile travel speed in meters per second.");

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
                "$item_bow:Neck:2," +
                "$item_bow_finewood:Boar:2:0:0:3.0," +
                "$item_bow_huntsman:Bjorn:0," +
                "$item_bow_draugrfang:Abomination:0:0:0.5," +
                "$item_bow_snipesnap:Lox:0," +
                "$item_bow_ashlands:Morgen:0:0:0.5," +
                "$item_bow_ashlandsstorm:FallenValkyrie:0," +
                "$item_bow_ashlandsroot:Asksvin:1," +
                "$item_bow_ashlandsblood:Fenring_Cultist:2," +
                "$item_bow_gold_frostfire:StoneGolem:2," +
                "$item_bow_gold_bloodlightning:Morgen:0," +
                "$bow_trollbone_tw:Troll:0," +
                "$bow_blackmetal_tw:Lox:0," +
                "$greatbow_blackmetal_tw:Lox:1," +
                "$greatbow_dvergr_tw:Dverger:0," +
                "$greatbow_moder_tw:Hatchling:2," +
                "$blackbow_tca:SeekerBrute:1," +
                "$thoridal_tca:Gjall:2",
                "Comma-separated bow→creature mappings. " +
                "Format: bowItemName:CreaturePrefab:starLevel[:scale[:damageMult[:hpMult]]].");
            CreatureScaleMap = Config.Bind("Bow Primal Rally", "CreatureScaleMap",
                "Troll:0.5,Bjorn:0.7,Lox:0.5,Abomination:0.4,StoneGolem:0.5," +
                "FulingBerserker:0.6,Gjall:0.4,Volture:0.6,Morgen:0.6,Barka:0.3," +
                "GammelTroll:0.5,Eikthyr:0.6,gd_king:0.4,Bonemass:0.4,Dragon:0.5," +
                "GoblinKing:0.3,SeaderQueen:0.3,Fader:0.4,FimbulBringer:0.25,FallenValkyrie:0.5",
                "Default scale per creature prefab name. Format: CreaturePrefab:scale.");

            // Dual Knife Poison Mayhem
            DualKnifeSkillCooldown = Config.Bind("Dual Knife Poison Mayhem", "Cooldown", 20f,
                "Cooldown in seconds for Poison Mayhem.");
            DualKnifeSkillDamage = Config.Bind("Dual Knife Poison Mayhem", "DamageMultiplier", 0.40f,
                "Damage multiplier per bomb hit.");
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
                "Base damage multiplier. Combo hits use 50%, finisher uses 100%.");

            // 2H Mace Earthquake
            TwoHandedMaceSkillCooldown = Config.Bind("2H Mace Earthquake", "Cooldown", 15f,
                "Cooldown in seconds for Earthquake.");
            TwoHandedMaceSkillDamage = Config.Bind("2H Mace Earthquake", "DamageMultiplier", 0.60f,
                "Damage multiplier per stomp explosion.");
            TwoHandedMaceSwingOgg = Config.Bind("2H Mace Earthquake", "SwingOgg", "2hMaceSwing.ogg",
                "Custom OGG played on each normal-attack swing of a two-handed mace. " +
                "Leave blank (or if the file is missing) to use the greatsword swing sounds. " +
                "Requires a game restart to change.");

            // Staff of Embers — Meteor Strike
            StaffFireballEitrCost = Config.Bind("Staff of Embers Meteor Strike", "EitrCost", 50f,
                "Eitr cost to cast Meteor Strike.");
            StaffFireballDamageMultiplier = Config.Bind("Staff of Embers Meteor Strike", "DamageMultiplier", 1.0f,
                "Multiplier applied to each meteor's fire damage.");
            StaffFireballDamageRadius = Config.Bind("Staff of Embers Meteor Strike", "DamageRadius", 12f,
                "Radius in meters of each meteor's damage on impact.");
            StaffFireballRadius = Config.Bind("Staff of Embers Meteor Strike", "FieldRadius", 4.5f,
                "Radius of the meteor strike field around the cast position.");
            StaffFireballDuration = Config.Bind("Staff of Embers Meteor Strike", "Duration", 12f,
                "How long the meteor field lasts in seconds.");
            StaffFireballIntervalMin = Config.Bind("Staff of Embers Meteor Strike", "IntervalMin", 0.5f,
                "Minimum seconds between meteor impacts.");
            StaffFireballIntervalMax = Config.Bind("Staff of Embers Meteor Strike", "IntervalMax", 1.25f,
                "Maximum seconds between meteor impacts.");

            // Staff of Frost — Ice Nova
            StaffIceShardsEitrCost = Config.Bind("Staff of Frost Ice Nova", "EitrCost", 30f,
                "Eitr cost to cast Ice Nova.");
            StaffIceShardsDamageMultiplier = Config.Bind("Staff of Frost Ice Nova", "DamageMultiplier", 2.0f,
                "Multiplier applied to weapon frost damage.");
            StaffIceShardsRadius = Config.Bind("Staff of Frost Ice Nova", "Radius", 12f,
                "Radius of the Ice Nova AOE in meters.");
            StaffIceShardsDuration = Config.Bind("Staff of Frost Ice Nova", "FreezeDuration", 8f,
                "How long enemies are frozen in place (seconds).");
            StaffIceShardsSlowDuration = Config.Bind("Staff of Frost Ice Nova", "SlowDuration", 3f,
                "How long the post-freeze movement slow lasts (seconds). Set to 0 to disable.");

            // Staff of Fracturing — Ragnarök
            StaffClusterbombMinEitr = Config.Bind("Staff of Fracturing Ragnarok", "MinEitrCost", 35f,
                "Eitr cost for an instant cast (no charge).");
            StaffClusterbombMaxEitr = Config.Bind("Staff of Fracturing Ragnarok", "MaxEitrCost", 60f,
                "Eitr cost for a fully charged cast (3 seconds).");
            StaffClusterbombMaxChargeTime = Config.Bind("Staff of Fracturing Ragnarok", "MaxChargeTime", 3f,
                "Time in seconds to reach full charge.");
            StaffClusterbombMinDamageMult = Config.Bind("Staff of Fracturing Ragnarok", "MinDamageMultiplier", 1.2f,
                "Damage multiplier for an instant cast.");
            StaffClusterbombMaxDamageMult = Config.Bind("Staff of Fracturing Ragnarok", "MaxDamageMultiplier", 3.0f,
                "Damage multiplier for a fully charged cast.");

            // Staff of the Wild — Regeneration
            StaffGreenRootsEitrCost = Config.Bind("Staff of the Wild Regeneration", "EitrCost", 60f,
                "Eitr cost to cast Regeneration.");
            StaffGreenRootsHealPercent = Config.Bind("Staff of the Wild Regeneration", "HealPercent", 0.30f,
                "Total HP healed as a percent of max HP (0.30 = 30% max HP healed over duration).");
            StaffGreenRootsDuration = Config.Bind("Staff of the Wild Regeneration", "Duration", 6f,
                "Duration in seconds over which the total heal is applied. " +
                "HealPerSecond = (MaxHP * HealPercent) / Duration.");
            StaffGreenRootsRadius = Config.Bind("Staff of the Wild Regeneration", "Radius", 12f,
                "Radius in meters to heal friendly players, summons and tamed creatures.");

            // Thunderblood Staff — Thunderstruck
            StaffThunderbloodEitrCost = Config.Bind("Thunderblood Staff Thunderstruck", "EitrCost", 100f,
                "Eitr cost to cast Thunderstruck.");
            StaffThunderbloodDamagePercent = Config.Bind("Thunderblood Staff Thunderstruck", "DamagePercent", 0.3f,
                "Lightning damage per strike as a fraction of the staff's total weapon damage (0.3 = 30%).");
            StaffThunderbloodRadius = Config.Bind("Thunderblood Staff Thunderstruck", "Radius", 30f,
                "Radius in meters around the player in which enemies can be struck.");
            StaffThunderbloodIntervalMin = Config.Bind("Thunderblood Staff Thunderstruck", "IntervalMin", 8f,
                "Minimum seconds an enemy must stay in range before each strike.");
            StaffThunderbloodIntervalMax = Config.Bind("Thunderblood Staff Thunderstruck", "IntervalMax", 15f,
                "Maximum seconds an enemy must stay in range before each strike.");
            StaffThunderbloodDuration = Config.Bind("Thunderblood Staff Thunderstruck", "Duration", 600f,
                "How long Thunderstruck lasts in seconds (600 = 10 minutes).");
            StaffThunderbloodCastVfx = Config.Bind("Thunderblood Staff Thunderstruck", "CastVfx", "vfx_shieldgenerator_startup",
                "VFX attached to the caster when cast (its built-in audio is removed). Leave blank for none.");
            StaffThunderbloodCastSfx = Config.Bind("Thunderblood Staff Thunderstruck", "CastSfx", "",
                "Optional extra vanilla SFX prefab played on cast, alongside the custom OGG. Leave blank for none.");
            StaffThunderbloodStrikeVfx = Config.Bind("Thunderblood Staff Thunderstruck", "StrikeVfx", "fx_eikthyr_forwardshockwave",
                "VFX played on each strike, spawned above the enemy and pointed straight down. Leave blank for none.");
            StaffThunderbloodStrikeSfx = Config.Bind("Thunderblood Staff Thunderstruck", "StrikeSfx", "",
                "SFX prefab played on each strike. If set, StrikeVfx's built-in audio is replaced. Leave blank for none.");
            StaffThunderbloodStrikeHeight = Config.Bind("Thunderblood Staff Thunderstruck", "StrikeHeight", 15f,
                "How high above the enemy the strike VFX spawns before travelling down to the ground.");

            // Shield Staff — Sanctuary
            StaffShieldEitrCost = Config.Bind("Shield Staff Sanctuary", "EitrCost", 60f,
                "Eitr cost to cast Sanctuary.");
            StaffShieldHealthPercent = Config.Bind("Shield Staff Sanctuary", "HealthPercent", 0.25f,
                "Health cost as a fraction of max HP (0.25 = 25%). Cast is blocked if it would kill you.");
            StaffShieldRadius = Config.Bind("Shield Staff Sanctuary", "Radius", 7f,
                "Radius of the dome in meters.");
            StaffShieldDuration = Config.Bind("Shield Staff Sanctuary", "Duration", 10f,
                "How long the dome lasts in seconds.");
            StaffShieldSlowPercent = Config.Bind("Shield Staff Sanctuary", "SlowPercent", 0.70f,
                "How much enemies and enemy projectiles inside are slowed (0.70 = 70% slower). Max 0.95.");
            StaffShieldCastVfx = Config.Bind("Shield Staff Sanctuary", "CastVfx", "vfx_shieldgenerator_refuel",
                "VFX attached to the caster on cast, tinted to DomeColor with its built-in audio removed. " +
                "Leave blank for none.");
            StaffShieldCastSfx = Config.Bind("Shield Staff Sanctuary", "CastSfx", "",
                "Optional extra vanilla SFX prefab played on cast, alongside the custom sounds. Leave blank for none.");
            StaffShieldCastVfxHeight = Config.Bind("Shield Staff Sanctuary", "CastVfxHeight", -0.5f,
                "Height offset in meters for the cast VFX relative to the caster. Negative = lower.");
            StaffShieldBubbleDelay = Config.Bind("Shield Staff Sanctuary", "BubbleDelay", 1.0f,
                "Seconds after casting before the dome appears.");
            StaffShieldDomeFadeTime = Config.Bind("Shield Staff Sanctuary", "DomeFadeTime", 0.25f,
                "Seconds the dome takes to fade in when it appears and fade out when it ends. 0 = no fade.");
            StaffShieldDomeVfx = Config.Bind("Shield Staff Sanctuary", "DomeVfx", "vfx_StaffShield",
                "VFX prefab used as the dome visual.");
            StaffShieldDomeColor = Config.Bind("Shield Staff Sanctuary", "DomeColor", "#00E4FF",
                "Dome tint as a hex color (#RRGGBB). Original transparency is kept.");
            StaffShieldDomeHeightOffset = Config.Bind("Shield Staff Sanctuary", "DomeHeightOffset", -0.2f,
                "Height of the dome's center relative to the ground, as a fraction of its radius. " +
                "0 = half dome, 0.3 = more of the sphere visible, -0.2 = flatter dome.");
            StaffShieldDomeInsideBoost = Config.Bind("Shield Staff Sanctuary", "DomeInsideBoost", 2f,
                "Opacity and glow multiplier for the dome as seen from inside. " +
                "1 = same as outside, higher = more visible from inside.");

            // Magic Cost Scaling
            MagicCostScalingEnabled = Config.Bind("Magic Cost Scaling", "Enabled", true,
                "Increase staff skill Eitr and Blood Magic HP costs when the caster's magic " +
                "skill (Elemental Magic or Blood Magic) is below 100. " +
                "All staff skill cost defaults are balanced for skill level 100.");
            MagicCostPenaltyPerStep = Config.Bind("Magic Cost Scaling", "PenaltyPerStep", 0.01f,
                "Extra cost added per step below skill level 100 (0.01 = 1%).");
            MagicCostLevelsPerStep = Config.Bind("Magic Cost Scaling", "LevelsPerStep", 5f,
                "Skill levels per step. With defaults, skill 1 costs +20% and skill 100 costs +0%.");

            // Skeleton Staff — Charred Requiem
            StaffSkeletonLevelTiers = Config.Bind("Skeleton Staff Charred Requiem", "LevelTiers",
                "80:0.30:1,75:0.25:1,65:0.25:1,60:0.25:2",
                "Cost and summon count per weapon upgrade level, comma separated, starting at level 1. " +
                "Format per level: eitrCost:healthPercent:summonCount. " +
                "healthPercent is a fraction of max HP (0.25 = 25%). " +
                "Levels above the last entry use the last entry.");
            StaffSkeletonDuration = Config.Bind("Skeleton Staff Charred Requiem", "SummonDuration", 600f,
                "How long the Charred Archers last in seconds (600 = 10 minutes).");
            StaffSkeletonPrefab = Config.Bind("Skeleton Staff Charred Requiem", "SummonPrefab", "Charred_Archer",
                "Creature prefab to summon.");

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

            string path = System.IO.Path.Combine(pluginDir, "icons", filename);
            if (!System.IO.File.Exists(path))
                path = System.IO.Path.Combine(pluginDir, filename);
            if (!System.IO.File.Exists(path))
                path = System.IO.Path.Combine(pluginDir, "KeenCombat", "icons", filename);
            if (!System.IO.File.Exists(path))
                path = System.IO.Path.Combine(pluginDir, "KeenCombat", filename);

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

        /// <summary>
        /// Finds a custom audio file in any of the supported folder layouts
        /// (manual install, R2ModMan flattened, Gale/SteamOS). Null if missing.
        /// </summary>
        internal static string? FindAudioFile(string filename)
        {
            string pluginDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location)!;

            string[] candidates =
            {
                System.IO.Path.Combine(pluginDir, "audio", filename),
                System.IO.Path.Combine(pluginDir, filename),
                System.IO.Path.Combine(pluginDir, "KeenCombat", "audio", filename),
                System.IO.Path.Combine(pluginDir, "KeenCombat", filename),
            };

            foreach (var candidate in candidates)
                if (System.IO.File.Exists(candidate))
                    return candidate;

            return null;
        }

        internal static IEnumerator LoadAudioClip(string filename,
                                                   System.Action<AudioClip?> onLoaded)
        {
            string? path = FindAudioFile(filename);

            if (path == null)
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