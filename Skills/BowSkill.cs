using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class BowSkill : IWeaponSkill
    {
        public string SkillName => "Primal Rally";
        public string Description => "Call upon a wild beast to fight by your side.";
        public float Cooldown => Plugin.BowSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Bow";

        private const string ZdoKeyPrimalRally = "KeenCombat_PrimalRally";
        private const string ZdoKeyScale = "KeenCombat_SummonScale";
        private const string ZdoKeyDamageMult = "KeenCombat_SummonDamageMult";
        private const string ZdoKeyHpMult = "KeenCombat_SummonHpMult";
        private const string ZdoKeyOwner = "KeenCombat_SummonOwner";

        private static Character? _activeSummon = null;
        private static Coroutine? _summonCoroutine = null;

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("PrimalRallyIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player)
            => HUD.SE_SkillCooldown.IsOnCooldown(player, CooldownSEName);

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            HUD.SE_SkillCooldown.Apply(player, Cooldown, CooldownSEName);
            player.StartCoroutine(PrimalRallyRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator PrimalRallyRoutine(Player player, ItemDrop.ItemData weapon)
        {
            if (_activeSummon != null && !_activeSummon.IsDead())
                DespawnSummon(_activeSummon);

            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("emote_comehere");

            NetworkedEffects.BroadcastOgg("PetCall.ogg", player.transform.position);

            yield return new WaitForSeconds(1.0f);
            if (player == null || player.IsDead()) yield break;

            var (prefabName, starLevel, bowScale, bowDamageMult, bowHpMult) =
                GetCreatureForBow(weapon);

            float finalScale = bowScale > 0f ? bowScale : GetCreatureScale(prefabName);
            float finalDamageMult = bowDamageMult > 0f ? bowDamageMult : 1.0f;
            float finalHpMult = bowHpMult > 0f ? bowHpMult : 1.0f;

            KC_Log.Debug($"PrimalRally: spawning {prefabName} " +
                         $"(star {starLevel}, scale {finalScale}, " +
                         $"dmg {finalDamageMult}, hp {finalHpMult})");

            var creaturePrefab = ZNetScene.instance?.GetPrefab(prefabName);
            if (creaturePrefab == null)
            {
                KC_Log.Warn($"PrimalRally: prefab '{prefabName}' not found!");
                yield break;
            }

            Vector3 spawnPos = player.transform.position
                             + player.transform.right * 1.5f
                             - player.transform.forward * 1.5f;

            var spawnedObj = Object.Instantiate(creaturePrefab, spawnPos, Quaternion.identity);
            var character = spawnedObj.GetComponent<Character>();

            if (character == null)
            {
                KC_Log.Warn($"PrimalRally: '{prefabName}' has no Character!");
                Object.Destroy(spawnedObj);
                yield break;
            }

            if (starLevel > 0)
            {
                character.SetLevel(starLevel + 1);
                var nviewLevel = spawnedObj.GetComponent<ZNetView>();
                nviewLevel?.GetZDO()?.Set(ZDOVars.s_level, starLevel + 1);
            }

            // Apply HP multiplier
            if (finalHpMult != 1.0f)
            {
                float newMaxHp = character.GetMaxHealth() * finalHpMult;
                character.SetMaxHealth(newMaxHp);
                character.SetHealth(newMaxHp);
            }

            // Force friendly faction
            character.m_faction = Character.Faction.Players;

            // ZDO — sync all values to all clients
            var nview = spawnedObj.GetComponent<ZNetView>();
            if (nview != null && nview.GetZDO() != null)
            {
                nview.GetZDO().Set(ZDOVars.s_tamed, true);
                nview.GetZDO().Set(ZDOVars.s_tamedName, "Ally");
                nview.GetZDO().Set(ZDOVars.s_follow, player.GetPlayerName());
                nview.GetZDO().Set(ZdoKeyPrimalRally, true);
                nview.GetZDO().Set(ZdoKeyScale, finalScale);
                nview.GetZDO().Set(ZdoKeyDamageMult, finalDamageMult);
                nview.GetZDO().Set(ZdoKeyHpMult, finalHpMult);
                nview.GetZDO().Set(ZdoKeyOwner, player.GetPlayerName());
            }

            // Apply scale locally
            if (finalScale > 0f && finalScale != 1f)
                spawnedObj.transform.localScale = Vector3.one * finalScale;

            var tameable = spawnedObj.GetComponent<Tameable>();
            if (tameable != null)
                tameable.Tame();
            else
                spawnedObj.AddComponent<Tameable>();

            var monsterAI = spawnedObj.GetComponent<MonsterAI>();
            var animalAI = spawnedObj.GetComponent<AnimalAI>();

            if (monsterAI != null)
            {
                monsterAI.SetFollowTarget(player.gameObject);
                monsterAI.m_enableHuntPlayer = false;
                monsterAI.m_alertRange = 60f;
                monsterAI.m_viewRange = 60f;
                monsterAI.m_viewAngle = 180f;
                monsterAI.m_hearRange = 60f;
                player.StartCoroutine(ResetAwarenessAfterDelay(monsterAI, null, 10f));
            }
            else if (animalAI != null)
            {
                Object.Destroy(animalAI);
                var newMonsterAI = spawnedObj.AddComponent<MonsterAI>();
                newMonsterAI.SetFollowTarget(player.gameObject);
                newMonsterAI.m_enableHuntPlayer = false;
                newMonsterAI.m_alertRange = 60f;
                newMonsterAI.m_viewRange = 60f;
                newMonsterAI.m_viewAngle = 180f;
                newMonsterAI.m_hearRange = 60f;
                player.StartCoroutine(ResetAwarenessAfterDelay(null, newMonsterAI, 10f));
            }

            _activeSummon = character;
            KC_Log.Debug($"PrimalRally: {prefabName} summoned.");

            if (_summonCoroutine != null)
                player.StopCoroutine(_summonCoroutine);
            _summonCoroutine = player.StartCoroutine(
                SummonTimer(player, character, Plugin.BowSummonDuration.Value));
        }

        public static float GetCreatureScale(string prefabName)
        {
            string mapStr = Plugin.CreatureScaleMap.Value;
            foreach (var entry in mapStr.Split(','))
            {
                var parts = entry.Trim().Split(':');
                if (parts.Length < 2) continue;
                if (parts[0].Trim().Equals(prefabName,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float s))
                        return s;
                }
            }
            return 1.0f;
        }

        private static IEnumerator ResetAwarenessAfterDelay(
            MonsterAI? ai1, MonsterAI? ai2, float delay)
        {
            yield return new WaitForSeconds(delay);
            var ai = ai1 ?? ai2;
            if (ai == null) yield break;
            ai.m_alertRange = 20f;
            ai.m_viewRange = 20f;
            ai.m_viewAngle = 90f;
            ai.m_hearRange = 20f;
        }

        private static IEnumerator SummonTimer(Player player, Character summon, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (summon == null || summon.IsDead())
                {
                    _activeSummon = null;
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (summon != null && !summon.IsDead())
                DespawnSummon(summon);
        }

        public static void DespawnActiveSummon()
        {
            if (_activeSummon != null && !_activeSummon.IsDead())
                DespawnSummon(_activeSummon);
        }

        private static void DespawnSummon(Character summon)
        {
            if (summon == null) return;

            var vfxPrefab = ZNetScene.instance?.GetPrefab("fx_perfectdodge");
            if (vfxPrefab != null)
                Object.Instantiate(vfxPrefab, summon.transform.position,
                                   summon.transform.rotation);

            var nview = summon.GetComponent<ZNetView>();
            if (nview != null && ZNetScene.instance != null)
                ZNetScene.instance.Destroy(summon.gameObject);
            else
                Object.Destroy(summon.gameObject);

            _activeSummon = null;
        }

        private static (string prefab, int star, float scale, float damageMult, float hpMult)
            GetCreatureForBow(ItemDrop.ItemData weapon)
        {
            string bowName = weapon.m_shared.m_name.ToLowerInvariant();
            string mapStr = Plugin.BowCreatureMap.Value;

            var map = new Dictionary<string, (string, int, float, float, float)>();
            foreach (var entry in mapStr.Split(','))
            {
                var parts = entry.Trim().Split(':');
                if (parts.Length < 2) continue;
                string key = parts[0].Trim().ToLowerInvariant();
                string creature = parts[1].Trim();
                int star = parts.Length >= 3 && int.TryParse(parts[2].Trim(), out int s)
                                  ? s : 0;
                float scale = ParseFloat(parts, 3);
                float dmgMult = ParseFloat(parts, 4);
                float hpMult = ParseFloat(parts, 5);
                map[key] = (creature, star, scale, dmgMult, hpMult);
            }

            if (map.TryGetValue(bowName, out var result))
                return result;

            KC_Log.Warn($"PrimalRally: no mapping for '{bowName}' — spawning Neck.");
            return ("Neck", 0, 1.0f, 1.0f, 1.0f);
        }

        private static float ParseFloat(string[] parts, int index)
        {
            if (parts.Length <= index) return 0f;
            return float.TryParse(parts[index].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float v) ? v : 0f;
        }
    }

    // -----------------------------------------------------------------------
    // Apply ZDO scale and HP on all clients when a Primal Rally summon spawns.
    // Retries after 1 second in case ZDO hasn't propagated yet on other clients.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Character), "Start")]
    public static class Character_Start_PrimalRallySync
    {
        static void Postfix(Character __instance)
        {
            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            if (!nview.GetZDO().GetBool("KeenCombat_PrimalRally")) return;

            // Apply immediately
            ApplySync(__instance, nview);

            // Retry after 1 second in case ZDO hasn't fully propagated on other clients
            Plugin.instance.StartCoroutine(ApplySyncDelayed(__instance, nview));
        }

        private static void ApplySync(Character character, ZNetView nview)
        {
            if (character == null || nview == null || nview.GetZDO() == null) return;

            float scale = nview.GetZDO().GetFloat("KeenCombat_SummonScale", 1.0f);
            if (scale > 0f && scale != 1f)
                character.transform.localScale = Vector3.one * scale;

            float hpMult = nview.GetZDO().GetFloat("KeenCombat_SummonHpMult", 1.0f);
            if (hpMult != 1.0f && nview.IsOwner())
            {
                float newMaxHp = character.GetMaxHealth() * hpMult;
                character.SetMaxHealth(newMaxHp);
                character.SetHealth(newMaxHp);
            }

            character.m_faction = Character.Faction.Players;
            KC_Log.Debug($"PrimalRallySync: {character.m_name} scale={scale} hpMult={hpMult}");
        }

        private static IEnumerator ApplySyncDelayed(Character character, ZNetView nview)
        {
            yield return new WaitForSeconds(1.0f);
            ApplySync(character, nview);
        }
    }

    // -----------------------------------------------------------------------
    // Apply damage multiplier from ZDO when a Primal Rally summon attacks.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    public static class Character_Damage_PrimalRallyMult
    {
        static void Prefix(Character __instance, ref HitData hit)
        {
            if (hit == null) return;
            var attacker = hit.GetAttacker();
            if (attacker == null) return;

            var nview = attacker.GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            if (!nview.GetZDO().GetBool("KeenCombat_PrimalRally")) return;

            // Block damage to the player entirely
            if (__instance.IsPlayer())
            {
                hit.m_damage = new HitData.DamageTypes();
                return;
            }

            float dmgMult = nview.GetZDO().GetFloat("KeenCombat_SummonDamageMult", 1.0f);
            if (dmgMult != 1.0f)
                hit.m_damage.Modify(dmgMult);

            if (attacker.m_name == "$enemy_troll")
                hit.m_damage.Modify(0.5f);
        }
    }

    // -----------------------------------------------------------------------
    // Prevent Primal Rally summons from dropping loot.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    public static class CharacterDrop_PrimalRally_NoDrop
    {
        static void Postfix(CharacterDrop __instance,
                            ref List<KeyValuePair<GameObject, int>> __result)
        {
            var nview = __instance.GetComponent<ZNetView>();
            if (nview?.GetZDO()?.GetBool("KeenCombat_PrimalRally") == true)
                __result.Clear();
        }
    }

    // -----------------------------------------------------------------------
    // Prevent Primal Rally summons from damaging player structures.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
    public static class WearNTear_PrimalRally_NoDamage
    {
        static bool Prefix(WearNTear __instance, HitData hit)
        {
            if (hit == null) return true;
            var attacker = hit.GetAttacker();
            if (attacker == null) return true;

            var nview = attacker.GetComponent<ZNetView>();
            if (nview?.GetZDO()?.GetBool("KeenCombat_PrimalRally") == true)
                return false;

            return true;
        }
    }

    // -----------------------------------------------------------------------
    // Despawn active summon when player logs out.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Game), "ContinueLogout")]
    public static class Game_Logout_DespawnSummon
    {
        static void Prefix()
        {
            KC_Log.Debug("PrimalRally: ContinueLogout — despawning summon.");
            BowSkill.DespawnActiveSummon();
        }
    }

    // -----------------------------------------------------------------------
    // Clean up lingering summons on login.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Game), "SpawnPlayer")]
    public static class Game_SpawnPlayer_CleanupSummon
    {
        static void Postfix()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            string playerName = player.GetPlayerName();

            var toDestroy = new List<ZNetView>();
            foreach (var go in ZNetScene.instance.m_instances.Values)
            {
                if (go == null) continue;
                var nview = go.GetComponent<ZNetView>();
                if (nview?.GetZDO() == null) continue;
                if (!nview.GetZDO().GetBool("KeenCombat_PrimalRally")) continue;
                if (nview.GetZDO().GetString("KeenCombat_SummonOwner") != playerName) continue;
                toDestroy.Add(nview);
            }

            foreach (var nview in toDestroy)
            {
                KC_Log.Debug("PrimalRally: cleaning up lingering summon on login.");
                if (nview.IsOwner())
                    nview.Destroy();
            }
        }
    }
}