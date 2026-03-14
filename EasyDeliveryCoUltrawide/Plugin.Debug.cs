using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        private static void LogDebug(string message)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            _log.LogInfo(message);
        }

        private static void PerfCount(string key)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            if (PerfCounts.TryGetValue(key, out var count))
            {
                PerfCounts[key] = count + 1;
            }
            else
            {
                PerfCounts[key] = 1;
            }

            float now = Time.realtimeSinceStartup;
            if (_perfLastLogTime <= 0f)
            {
                _perfLastLogTime = now;
                return;
            }

            if (now - _perfLastLogTime < 2f)
            {
                return;
            }

            foreach (var entry in PerfCounts)
            {
                _log.LogInfo($"Perf [{entry.Key}] calls in 2s: {entry.Value}.");
            }

            PerfCounts.Clear();
            _perfLastLogTime = now;
        }
    }
}
