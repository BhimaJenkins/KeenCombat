namespace KeenCombat
{
    // -----------------------------------------------------------------------
    // KC_Log — Centralized logging for KeenCombat
    //
    // Usage:
    //   KC_Log.Info("message")    — always logs
    //   KC_Log.Warn("message")    — always logs as warning
    //   KC_Log.Error("message")   — always logs as error
    //   KC_Log.Debug("message")   — only logs when VerboseLogging = true
    //
    // To enable debug logging, set VerboseLogging = true below or
    // flip it at runtime via a config entry.
    // -----------------------------------------------------------------------
    public static class KC_Log
    {
        /// <summary>
        /// Set to true to enable Debug() log calls.
        /// Flip this when diagnosing issues, set back to false before release.
        /// </summary>
        public static bool VerboseLogging = false;

        public static void Info(string msg)
            => Plugin.Log.LogInfo(msg);

        public static void Warn(string msg)
            => Plugin.Log.LogWarning(msg);

        public static void Error(string msg)
            => Plugin.Log.LogError(msg);

        /// <summary>
        /// Only logs when VerboseLogging is true.
        /// Use for temp diagnostic logs — no need to delete them,
        /// just leave VerboseLogging = false in release builds.
        /// </summary>
        public static void Debug(string msg)
        {
            if (VerboseLogging)
                Plugin.Log.LogInfo($"[DEBUG] {msg}");
        }
    }
}