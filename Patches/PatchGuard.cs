using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Patches
{
    // -----------------------------------------------------------------------
    // PatchGuard
    //
    // Some mods (e.g. configuration managers that block game input while
    // their window is open) remove patches from Valheim's input functions
    // too broadly when they clean up — wiping out EVERY mod's patches on
    // those functions, not just their own. That silently disables our
    // heavy-attack suppression until the game restarts.
    //
    // This watchdog checks twice a second that our input patches are still
    // applied, and re-applies any that another mod removed.
    // -----------------------------------------------------------------------
    public static class PatchGuard
    {
        private const float Interval = 0.5f;

        // (method we patch, our patch class)
        private static readonly (MethodBase? target, Type patchClass)[] Guarded =
        {
            (AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButton)),     typeof(ZInput_SuppressGetButton)),
            (AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonDown)), typeof(ZInput_SuppressGetButtonDown)),
            (AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonUp)),   typeof(ZInput_SuppressGetButtonUp)),
            (AccessTools.Method(typeof(Player), "InAttack"),                   typeof(InAttack_Override)),
        };

        public static IEnumerator Loop()
        {
            var wait = new WaitForSeconds(Interval);
            while (true)
            {
                yield return wait;
                CheckAndRepair();
            }
        }

        private static void CheckAndRepair()
        {
            var harmony = Plugin.HarmonyInstance;
            if (harmony == null) return;

            foreach (var (target, patchClass) in Guarded)
            {
                if (target == null) continue;
                if (IsApplied(target, patchClass)) continue;

                try
                {
                    harmony.CreateClassProcessor(patchClass).Patch();
                    Plugin.Log.LogWarning(
                        $"PatchGuard: another mod removed KeenCombat's patch on " +
                        $"{target.DeclaringType?.Name}.{target.Name} — re-applied it.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"PatchGuard: failed to re-apply {patchClass.Name}: {e.Message}");
                }
            }
        }

        private static bool IsApplied(MethodBase target, Type patchClass)
        {
            var info = Harmony.GetPatchInfo(target);
            if (info == null) return false;

            return info.Prefixes.Concat(info.Postfixes)
                       .Any(p => p.PatchMethod.DeclaringType == patchClass);
        }
    }
}