using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // KnifeSkill — Assassination
    //
    // Stealth state is stored on the player's ZDO ("KeenCombat_Stealth") so
    // EVERY client knows who is stealthed:
    //   - Enemy sight patches check the flag on whichever player they look at,
    //     so stealth works no matter which client controls the enemy
    //   - StealthVisuals darkens stealthed players on every screen
    //
    // Local state (InStealth, StrikeReady) still drives the caster's own
    // skill icon and input, and is reset on death, logout and new sessions.
    // -----------------------------------------------------------------------
    public class KnifeSkill : IWeaponSkill
    {
        public string SkillName => "Assassination";
        public string Description => "Vanish into the shadows. Strike from stealth for massive damage.";
        public float Cooldown => Plugin.KnifeSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Knife";

        public const string StealthKey = "KeenCombat_Stealth";

        public static bool InStealth = false;
        public static bool StrikeReady = false;
        public static bool IsExecutingStrike = false;

        private static bool _wasCrouching = false;
        private static float _savedRunSpeed = 0f;

        private static Sprite? _stealthIcon = null;
        private static Sprite? _strikeIcon = null;
        private static bool _iconsLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconsLoaded)
                {
                    _stealthIcon = Plugin.LoadEmbeddedSprite("AssassinationStealthIcon.png");
                    _strikeIcon = Plugin.LoadEmbeddedSprite("AssassinationStrikeIcon.png");
                    _iconsLoaded = true;
                }
                return InStealth ? _strikeIcon : _stealthIcon;
            }
        }

        public bool IsOnCooldown(Player player)
            => HUD.SE_SkillCooldown.IsOnCooldown(player, CooldownSEName);

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            if (InStealth)
            {
                if (!StrikeReady) return;
                player.StartCoroutine(ExecuteStrike(player, weapon));
            }
            else
            {
                player.StartCoroutine(EnterStealth(player, weapon));
            }
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        // -------------------------------------------------------------------
        // Networked stealth flag
        // -------------------------------------------------------------------
        public static bool IsStealthed(Character? c)
        {
            if (c == null || !c.IsPlayer()) return false;
            var zdo = c.GetComponent<ZNetView>()?.GetZDO();
            return zdo != null && zdo.GetBool(StealthKey);
        }

        private static void SetStealthFlag(Player player, bool value)
        {
            var nview = player.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && nview.IsOwner())
                nview.GetZDO().Set(StealthKey, value);
        }

        /// <summary>Clears all local stealth state. Safe to call anytime.</summary>
        public static void ResetState()
        {
            InStealth = false;
            StrikeReady = false;
            IsExecutingStrike = false;
            _savedRunSpeed = 0f;
        }

        // -------------------------------------------------------------------
        // Enter stealth
        // -------------------------------------------------------------------
        private static IEnumerator EnterStealth(Player player, ItemDrop.ItemData weapon)
        {
            InStealth = true;
            StrikeReady = false;

            _wasCrouching = player.IsCrouching();
            if (!_wasCrouching)
                player.SetCrouch(true);

            // Save exact run speed so it can be restored precisely
            _savedRunSpeed = player.m_runSpeed;
            player.m_runSpeed = player.m_speed * 0.8f;

            // Mark stealthed for every client (enemy sight + visuals)
            SetStealthFlag(player, true);
            StealthVisuals.Refresh(player);

            // Smoke bomb — broadcast as a local-only visual on every client,
            // so its poison/damage can never spread to other players
            NetworkedEffects.BroadcastVfx("smokebomb_explosion",
                player.transform.position, player.transform.rotation, 0.5f);

            // Broadcast stealth OGG to all clients
            NetworkedEffects.BroadcastOgg("Stealth.ogg", player.transform.position);

            // Apply stealth buff icon
            HUD.SE_KnifeStealthBuff.Apply(player, Plugin.KnifeStealthDuration.Value);

            yield return new WaitForSeconds(0.5f);
            StrikeReady = true;

            float elapsed = 0.5f;
            float maxDuration = Plugin.KnifeStealthDuration.Value;

            while (elapsed < maxDuration)
            {
                // Died or despawned mid-stealth — clean up everything
                if (player == null || player.IsDead())
                {
                    if (player != null) SetStealthFlag(player, false);
                    ResetState();
                    yield break;
                }

                if (IsExecutingStrike) yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (InStealth && !IsExecutingStrike)
            {
                ExitStealth(player);
                HUD.SE_SkillCooldown.Apply(player, Plugin.KnifeSkillCooldown.Value,
                                            "SE_Cooldown_Knife");
            }
        }

        // -------------------------------------------------------------------
        // Strike from stealth
        // -------------------------------------------------------------------
        private static IEnumerator ExecuteStrike(Player player, ItemDrop.ItemData weapon)
        {
            IsExecutingStrike = true;

            HUD.SE_SkillCooldown.Apply(player, Plugin.KnifeSkillCooldown.Value,
                                        "SE_Cooldown_Knife");

            ExitStealth(player);

            Vector3 strikeDir = GameCamera.instance != null
                ? GameCamera.instance.transform.forward
                : player.transform.forward;

            strikeDir.y = 0f;
            strikeDir.Normalize();
            if (strikeDir != Vector3.zero)
                player.transform.rotation = Quaternion.LookRotation(strikeDir);

            // Stab animation — synced to all players by ZSyncAnimation
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("knife_stab2");

            yield return new WaitForSeconds(0.15f);

            // Broadcast backstab VFX to all clients
            Vector3 backstabPos = player.transform.position + strikeDir * 1.5f + Vector3.up;
            NetworkedEffects.BroadcastVfx("fx_backstab",
                backstabPos, Quaternion.LookRotation(strikeDir));

            ApplyStrike(player, weapon, strikeDir);

            IsExecutingStrike = false;
        }

        private static void ApplyStrike(Player player, ItemDrop.ItemData weapon,
                                         Vector3 direction)
        {
            Vector3 origin = player.transform.position + Vector3.up * 1.2f;

            var colliders = Physics.OverlapSphere(
                origin + direction * 2f, 1.5f, SkillMasks.Characters);

            // One hit per enemy, even if it has several colliders
            var alreadyHit = new HashSet<Character>();

            foreach (var col in colliders)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                if (character.m_faction == Character.Faction.Players) continue;
                if (!alreadyHit.Add(character)) continue;

                // Fresh HitData per enemy — Valheim modifies it when applied
                HitData hit = new HitData();
                hit.m_damage = weapon.GetDamage();
                hit.m_damage.Modify(Plugin.KnifeSkillDamage.Value);
                hit.m_pushForce = 3.0f;
                hit.m_staggerMultiplier = 3.0f;
                hit.m_dir = direction;
                hit.m_attacker = player.GetZDOID();
                hit.m_backstabBonus = 4f;
                hit.m_point = character.transform.position;

                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }

        // -------------------------------------------------------------------
        // Exit stealth
        // -------------------------------------------------------------------
        private static void ExitStealth(Player player)
        {
            InStealth = false;
            StrikeReady = false;

            if (!_wasCrouching)
                player.SetCrouch(false);

            // Restore the exact saved run speed
            if (_savedRunSpeed > 0f)
                player.m_runSpeed = _savedRunSpeed;
            _savedRunSpeed = 0f;

            SetStealthFlag(player, false);
            StealthVisuals.Refresh(player);

            HUD.SE_KnifeStealthBuff.Remove(player);
        }
    }

    // -----------------------------------------------------------------------
    // StealthVisuals — darkens stealthed players on EVERY client, driven by
    // the synced ZDO flag. Checks all players a few times per second, and
    // Refresh() applies instantly for the local player.
    // -----------------------------------------------------------------------
    public static class StealthVisuals
    {
        private static readonly Color DarkColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        private static readonly Dictionary<Player, List<(Renderer r, Color original)>> _darkened
            = new Dictionary<Player, List<(Renderer, Color)>>();

        private static Coroutine? _loop;

        public static void Start()
        {
            if (Plugin.instance == null) return;
            if (_loop != null) Plugin.instance.StopCoroutine(_loop);
            _darkened.Clear();
            _loop = Plugin.instance.StartCoroutine(Loop());
        }

        private static IEnumerator Loop()
        {
            var wait = new WaitForSeconds(0.2f);
            var toRemove = new List<Player>();
            while (true)
            {
                yield return wait;

                // Forget players that have despawned
                toRemove.Clear();
                foreach (var p in _darkened.Keys)
                    if (p == null) toRemove.Add(p!);
                foreach (var p in toRemove)
                    _darkened.Remove(p);

                foreach (var p in Player.GetAllPlayers())
                    Refresh(p);
            }
        }

        /// <summary>Darkens or restores a player to match their stealth flag.</summary>
        public static void Refresh(Player? player)
        {
            if (player == null) return;

            bool stealthed = KnifeSkill.IsStealthed(player);
            bool darkened = _darkened.ContainsKey(player);

            if (stealthed && !darkened)
            {
                var saved = new List<(Renderer, Color)>();
                foreach (var r in player.GetComponentsInChildren<Renderer>())
                {
                    if (r.material == null || !r.material.HasProperty("_Color")) continue;
                    saved.Add((r, r.material.color));
                    r.material.color = DarkColor;
                }
                _darkened[player] = saved;
            }
            else if (!stealthed && darkened)
            {
                foreach (var (r, color) in _darkened[player])
                    if (r != null && r.material != null && r.material.HasProperty("_Color"))
                        r.material.color = color;
                _darkened.Remove(player);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Session start: clear any stale stealth state and start the visuals loop
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Game), "Start")]
    public static class Game_Start_StealthReset
    {
        static void Postfix()
        {
            KnifeSkill.ResetState();
            StealthVisuals.Start();
        }
    }

    // -----------------------------------------------------------------------
    // New player spawn (login or respawn after death): clear stale local
    // stealth state and make sure the new character isn't flagged
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Game), "SpawnPlayer")]
    public static class Game_SpawnPlayer_StealthReset
    {
        static void Postfix()
        {
            KnifeSkill.ResetState();

            var player = Player.m_localPlayer;
            var nview = player?.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && nview.IsOwner())
                nview.GetZDO().Set(KnifeSkill.StealthKey, false);
        }
    }

    // -----------------------------------------------------------------------
    // Enemies can't sense stealthed players — works on every client because
    // it checks the synced flag on the target, not local state
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSenseTarget),
        new[] { typeof(Character) })]
    public static class BaseAI_CanSenseTarget_Stealth_Patch
    {
        static bool Prefix(Character target, ref bool __result)
        {
            if (!KnifeSkill.IsStealthed(target)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget))]
    public static class MonsterAI_UpdateTarget_Stealth_Patch
    {
        static bool Prefix(MonsterAI __instance)
        {
            if (!KnifeSkill.IsStealthed(__instance.m_targetCreature)) return true;
            __instance.m_targetCreature = null;
            return false;
        }
    }
}