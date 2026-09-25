using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // StaffSkeletonSkill — Charred Requiem
    //
    // Summons Charred Archers that fight for the caster.
    // Cost scales with weapon upgrade level (configurable via LevelTiers):
    //   Level 1: 80 Eitr, 30% max HP, 1 archer
    //   Level 2: 75 Eitr, 25% max HP, 1 archer
    //   Level 3: 65 Eitr, 25% max HP, 1 archer
    //   Level 4: 60 Eitr, 25% max HP, 2 archers
    //
    // - Health cost can't kill you: blocked if current HP <= cost
    // - No cooldown: gated by Eitr + health
    // - Archers last 10 minutes (configurable)
    // - Recast removes your previous archers and summons fresh ones
    // - Despawn on logout, cleaned up on login
    //
    // Archers use the KeenCombat_PrimalRally ZDO flag, so all Primal Rally
    // protections in BowSkill.cs apply automatically (no drops, no structure
    // damage, no player damage, faction sync, login cleanup).
    // -----------------------------------------------------------------------
    public class StaffSkeletonSkill : IWeaponSkill
    {
        public string SkillName => "Charred Requiem";
        public string Description => "Sacrifice Eitr and blood to raise Charred Archers " +
                                        "that fight at your side.";
        public float Cooldown => 0f;
        public string CooldownSEName => "SE_Cooldown_StaffSkeleton";

        private const string ZdoKeyPrimalRally = "KeenCombat_PrimalRally";
        private const string ZdoKeyOwner = "KeenCombat_SummonOwner";

        // Custom summon sound — change this to your OGG file name
        private const string SummonOgg = "CharredRequiem.ogg";

        private static readonly List<Character> _activeSummons = new List<Character>();
        private static Coroutine? _timerCoroutine = null;

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("CharredRequiemIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player) => false;

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            var (eitrCost, hpPercent, count) = GetTier(weapon.m_quality);
            float costMult = MagicCostScaler.GetMultiplier(player, weapon);
            eitrCost *= costMult;
            hpPercent *= costMult;

            float hpCost = player.GetMaxHealth() * hpPercent;

            // Eitr check — Valheim's HUD shows its own "not enough Eitr" feedback
            if (!player.HaveEitr(eitrCost))
            {
                SkillFeedback.NotEnoughEitr();
                return;
            }

            // Health check — never let the cost kill the caster
            if (player.GetHealth() <= hpCost)
            {
                player.Message(MessageHud.MessageType.Center, "Not enough health");
                return;
            }

            player.UseEitr(eitrCost);
            player.SetHealth(player.GetHealth() - hpCost);

            player.StartCoroutine(SummonRoutine(player, count));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        // -----------------------------------------------------------------------
        // Summon routine
        // -----------------------------------------------------------------------
        private static IEnumerator SummonRoutine(Player player, int count)
        {
            // Remove previous archers first
            DespawnAllSummons();

            // Block the staff's normal attack from firing during the cast
            Patches.StaffCastGuard.Begin(player, 1.5f);

            // Cast emote — syncs to all players automatically
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("emote_challenge");

            // Caster VFX at the right hand (falls back to feet), audio stripped
            NetworkedEffects.BroadcastVfxSilent("fx_summon_skeleton",
                GetCasterHandPosition(player),
                player.transform.rotation);

            // Custom summon sound — the only audio for this skill
            NetworkedEffects.BroadcastOgg(SummonOgg, player.transform.position);

            yield return new WaitForSeconds(1.0f);
            if (player == null || player.IsDead()) yield break;

            string prefabName = Plugin.StaffSkeletonPrefab.Value;
            var prefab = ZNetScene.instance?.GetPrefab(prefabName);
            if (prefab == null)
            {
                KC_Log.Warn($"CharredRequiem: prefab '{prefabName}' not found!");
                yield break;
            }

            for (int i = 0; i < count; i++)
            {
                // First archer behind-right, second behind-left
                float side = (i % 2 == 0) ? 1.5f : -1.5f;
                Vector3 spawnPos = player.transform.position
                                 + player.transform.right * side
                                 - player.transform.forward * 1.5f;

                // Spawn VFX at the archer's feet, audio stripped
                NetworkedEffects.BroadcastVfxSilent("fx_summon_spirit_spawn",
                    spawnPos,
                    Quaternion.identity);

                var summon = SpawnArcher(player, prefab, spawnPos);
                if (summon != null)
                    _activeSummons.Add(summon);
            }

            // Fresh timer for this batch — runs on the plugin so it survives player death
            if (_timerCoroutine != null)
                Plugin.instance.StopCoroutine(_timerCoroutine);
            _timerCoroutine = Plugin.instance.StartCoroutine(
                SummonTimer(Plugin.StaffSkeletonDuration.Value));
        }

        // -----------------------------------------------------------------------
        // Returns the caster's right hand world position, or feet as fallback
        // -----------------------------------------------------------------------
        private static Vector3 GetCasterHandPosition(Player player)
        {
            var visEquip = player.GetComponent<VisEquipment>();
            if (visEquip != null && visEquip.m_rightHand != null)
                return visEquip.m_rightHand.position;

            return player.transform.position;
        }

        private static Character? SpawnArcher(Player player, GameObject prefab, Vector3 pos)
        {
            var obj = Object.Instantiate(prefab, pos, Quaternion.identity);
            var character = obj.GetComponent<Character>();

            if (character == null)
            {
                KC_Log.Warn("CharredRequiem: summoned prefab has no Character!");
                Object.Destroy(obj);
                return null;
            }

            character.m_faction = Character.Faction.Players;

            // ZDO flags — synced to all clients. The PrimalRally flag hooks
            // into the existing sync, no-drop, no-structure-damage, no-player-
            // damage and login-cleanup patches in BowSkill.cs.
            var nview = obj.GetComponent<ZNetView>();
            if (nview != null && nview.GetZDO() != null)
            {
                nview.GetZDO().Set(ZDOVars.s_tamed, true);
                nview.GetZDO().Set(ZDOVars.s_tamedName, "Charred Ally");
                nview.GetZDO().Set(ZDOVars.s_follow, player.GetPlayerName());
                nview.GetZDO().Set(ZdoKeyPrimalRally, true);
                nview.GetZDO().Set(ZdoKeyOwner, player.GetPlayerName());
            }

            var tameable = obj.GetComponent<Tameable>();
            if (tameable != null)
                tameable.Tame();
            else
                obj.AddComponent<Tameable>();

            var monsterAI = obj.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                monsterAI.SetFollowTarget(player.gameObject);
                monsterAI.m_enableHuntPlayer = false;

                // Short awareness boost so they engage quickly, then normal
                monsterAI.m_alertRange = 60f;
                monsterAI.m_viewRange = 60f;
                monsterAI.m_viewAngle = 180f;
                monsterAI.m_hearRange = 60f;
                Plugin.instance.StartCoroutine(ResetAwarenessAfterDelay(monsterAI, 10f));
            }

            return character;
        }

        private static IEnumerator ResetAwarenessAfterDelay(MonsterAI ai, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ai == null) yield break;
            ai.m_alertRange = 20f;
            ai.m_viewRange = 20f;
            ai.m_viewAngle = 90f;
            ai.m_hearRange = 20f;
        }

        // -----------------------------------------------------------------------
        // Timer — despawns all archers when duration expires
        // -----------------------------------------------------------------------
        private static IEnumerator SummonTimer(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Drop dead or destroyed archers from the list
                _activeSummons.RemoveAll(s => s == null || s.IsDead());
                if (_activeSummons.Count == 0)
                {
                    _timerCoroutine = null;
                    yield break;
                }

                elapsed += 1f;
                yield return new WaitForSeconds(1f);
            }

            _timerCoroutine = null;
            DespawnAllSummons();
        }

        // -----------------------------------------------------------------------
        // Despawn — used by recast, timer expiry and logout
        // -----------------------------------------------------------------------
        public static void DespawnAllSummons()
        {
            foreach (var summon in _activeSummons)
            {
                if (summon == null) continue;

                // Poof VFX visible to all players, audio stripped
                NetworkedEffects.BroadcastVfxSilent("fx_perfectdodge",
                    summon.transform.position,
                    summon.transform.rotation);

                var nview = summon.GetComponent<ZNetView>();
                if (nview != null && ZNetScene.instance != null)
                    ZNetScene.instance.Destroy(summon.gameObject);
                else
                    Object.Destroy(summon.gameObject);
            }

            _activeSummons.Clear();

            if (_timerCoroutine != null)
            {
                Plugin.instance.StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }
        }

        // -----------------------------------------------------------------------
        // Tier lookup — parses LevelTiers config.
        // Format per entry: eitrCost:hpPercent:summonCount, comma separated,
        // one entry per weapon level starting at level 1.
        // Weapons above the last listed level use the last entry.
        // -----------------------------------------------------------------------
        private static (float eitr, float hpPercent, int count) GetTier(int quality)
        {
            var tiers = Plugin.StaffSkeletonLevelTiers.Value.Split(',');
            int index = Mathf.Clamp(quality - 1, 0, tiers.Length - 1);

            var parts = tiers[index].Trim().Split(':');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var style = System.Globalization.NumberStyles.Float;

            float eitr = parts.Length > 0 &&
                float.TryParse(parts[0].Trim(), style, inv, out float e) ? e : 80f;
            float hp = parts.Length > 1 &&
                float.TryParse(parts[1].Trim(), style, inv, out float h) ? h : 0.30f;
            int count = parts.Length > 2 &&
                int.TryParse(parts[2].Trim(), out int c) ? c : 1;

            return (eitr, hp, Mathf.Max(1, count));
        }
    }

    // -----------------------------------------------------------------------
    // Despawn Charred Archers when the player logs out.
    // (Login cleanup is already handled by Game_SpawnPlayer_CleanupSummon
    // in BowSkill.cs since archers carry the PrimalRally flag + owner name.)
    // -----------------------------------------------------------------------
    [HarmonyPatch(typeof(Game), "ContinueLogout")]
    public static class Game_Logout_DespawnCharredArchers
    {
        static void Prefix()
        {
            StaffSkeletonSkill.DespawnAllSummons();
        }
    }
}