namespace CtF
{
    public static class CtFLogger
    {
        private static bool _enabled;

        public static void SetEnabled(bool enabled) => _enabled = enabled;

        public static void Log(string msg)
        {
            if (!_enabled) return;
            try { UnityEngine.Debug.Log("[CtF] " + msg); } catch { }
        }

        public static void Warn(string msg)
        {
            if (!_enabled) return;
            try { UnityEngine.Debug.LogWarning("[CtF] " + msg); } catch { }
        }

        public static void Error(string msg)
        {
            try { UnityEngine.Debug.LogError("[CtF] " + msg); } catch { }
        }
    }
}
