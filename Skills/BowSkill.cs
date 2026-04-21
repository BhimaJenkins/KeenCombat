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

            player.StartCoroutine(Plugin.LoadAudioClip("PetCall.ogg", clip =>
            {
                if (clip == null) return;
                var src = player.GetComponent<AudioSource>()
                       ?? player.gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.PlayOneShot(clip);
            }));

            yield return new WaitForSeconds(1.0f);
            if (player == null || player.IsDead()) yield break;

            var (prefabName, starLevel) = GetCreatureForBow(weapon);

            var creaturePrefab = ZNetScene.instance?.GetPrefab(prefabName);
            if (creaturePrefab == null)
            {
                Plugin.Log.LogWarning($"PrimalRally: prefab '{prefabName}' not found!");
                yield break;
            }

            Vector3 spawnPos = player.transform.position
                             + player.transform.right * 1.5f
                             - player.transform.forward * 1.5f;

            var spawnedObj = Object.Instantiate(creaturePrefab, spawnPos, Quaternion.identity);
            var character = spawnedObj.GetComponent<Character>();

            if (character == null)
            {
                Plugin.Log.LogWarning($"PrimalRally: '{prefabName}' has no Character!");
                Object.Destroy(spawnedObj);
                yield break;
            }

            if (starLevel > 0)
            {
                character.SetLevel(starLevel + 1);
                var nviewLevel = spawnedObj.GetComponent<ZNetView>();
                nviewLevel?.GetZDO()?.Set(ZDOVars.s_level, starLevel + 1);
            }

            if (prefabName == "Troll" || prefabName == "Lox")
                spawnedObj.transform.localScale = Vector3.one * 0.5f;

            character.m_faction = Character.Faction.Players;

            var nview = spawnedObj.GetComponent<ZNetView>();
            if (nview != null && nview.GetZDO() != null)
            {
                nview.GetZDO().Set(ZDOVars.s_tamed, true);
                nview.GetZDO().Set(ZDOVars.s_tamedName, "Ally");
                nview.GetZDO().Set(ZDOVars.s_follow, player.GetPlayerName());
                nview.GetZDO().Set(ZdoKeyPrimalRally, true);
            }

            var tameable = spawnedObj.GetComponent<Tameable>();
            if (tameable != null)
                tameable.Tame();
            else
                spawnedObj.AddComponent<Tameable>();

            var monsterAI = spawnedObj.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                monsterAI.SetFollowTarget(player.gameObject);
                monsterAI.m_enableHuntPlayer = false;
            }

            _activeSummon = character;

            if (_summonCoroutine != null)
                player.StopCoroutine(_summonCoroutine);
            _summonCoroutine = player.StartCoroutine(
                SummonTimer(player, character, Plugin.BowSummonDuration.Value));
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

        private static (string prefab, int star) GetCreatureForBow(ItemDrop.ItemData weapon)
        {
            string bowName = weapon.m_shared.m_name.ToLowerInvariant();
            string mapStr = Plugin.BowCreatureMap.Value;

            var map = new Dictionary<string, (string, int)>();
            foreach (var entry in mapStr.Split(','))
            {
                var parts = entry.Trim().Split(':');
                if (parts.Length < 2) continue;
                string key = parts[0].Trim().ToLowerInvariant();
                string creature = parts[1].Trim();
                int star = parts.Length >= 3 && int.TryParse(parts[2].Trim(), out int s) ? s : 0;
                map[key] = (creature, star);
            }

            if (map.TryGetValue(bowName, out var result))
                return result;

            return ("Neck", 0);
        }
    }

    // -----------------------------------------------------------------------
    // Prevent Primal Rally summons from dropping loot on death.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.OnDeath))]
    public static class CharacterDrop_PrimalRally_NoDrop
    {
        static bool Prefix(CharacterDrop __instance)
        {
            var nview = __instance.GetComponent<ZNetView>();
            if (nview?.GetZDO()?.GetBool("KeenCombat_PrimalRally") == true)
                return false;
            return true;
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
                return false; // Block all structure damage from summons

            return true;
        }
    }

    // -----------------------------------------------------------------------
    // Reduce damage dealt BY tamed Trolls by 50%.
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    public static class Character_Damage_PrimalRallyTrollNerf
    {
        static void Prefix(Character __instance, ref HitData hit)
        {
            if (hit == null) return;
            var attacker = hit.GetAttacker();
            if (attacker == null) return;

            var nview = attacker.GetComponent<ZNetView>();
            if (nview != null && nview.GetZDO() != null)
            {
                if (nview.GetZDO().GetBool("KeenCombat_PrimalRally"))
                {
                    if (attacker.m_name == "$enemy_troll")
                    {
                        hit.m_damage.Modify(0.5f);
                      
                    }
                }
            }
        }
    }
}