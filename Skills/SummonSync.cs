using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // SummonSync
    //
    // Keeps Primal Rally / Charred Requiem summons correct on EVERY client.
    //
    // The original one-shot sync (on Character.Start + 1 second retry) could
    // miss: the other client might apply it before the ZDO data arrived, or
    // Valheim could overwrite the scale afterwards (e.g. star-level visuals).
    // This loop re-checks all summons every half second and fixes any drift.
    //
    // Scale is only enforced when a summon has an explicit non-1 scale stored,
    // so normal-size summons keep Valheim's own star-level size bump.
    // -----------------------------------------------------------------------
    public static class SummonSync
    {
        private const float Interval = 0.5f;
        private static Coroutine? _loop;

        public static void Start()
        {
            if (Plugin.instance == null) return;
            if (_loop != null) Plugin.instance.StopCoroutine(_loop);
            _loop = Plugin.instance.StartCoroutine(Loop());
        }

        private static IEnumerator Loop()
        {
            var wait = new WaitForSeconds(Interval);
            while (true)
            {
                yield return wait;

                foreach (var c in Character.GetAllCharacters())
                {
                    if (c == null) continue;

                    var zdo = c.GetComponent<ZNetView>()?.GetZDO();
                    if (zdo == null || !zdo.GetBool("KeenCombat_PrimalRally")) continue;

                    // Friendly on every client
                    c.m_faction = Character.Faction.Players;

                    // Enforce stored scale (skip if none or normal size)
                    float scale = zdo.GetFloat("KeenCombat_SummonScale", -1f);
                    if (scale <= 0f || Mathf.Approximately(scale, 1f)) continue;

                    if (Mathf.Abs(c.transform.localScale.x - scale) > 0.01f)
                        c.transform.localScale = Vector3.one * scale;
                }
            }
        }
    }

    // Start (or restart) the sync loop each time a world session begins
    [HarmonyPatch(typeof(Game), "Start")]
    public static class Game_Start_SummonSync
    {
        static void Postfix()
        {
            SummonSync.Start();
        }
    }
}