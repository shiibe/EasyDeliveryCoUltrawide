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
            if (_perfLogging == null || !_perfLogging.Value)
            {
                return;
            }

            if (string.IsNullOrEmpty(key))
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

            float interval = 2f;
            if (_perfLogIntervalSeconds != null)
            {
                interval = Mathf.Max(0.25f, _perfLogIntervalSeconds.Value);
            }

            if (now - _perfLastLogTime < interval)
            {
                return;
            }

            float elapsed = Mathf.Max(0.0001f, now - _perfLastLogTime);

            foreach (var entry in PerfCounts)
            {
                _log.LogInfo($"Perf [{entry.Key}] calls in {elapsed:0.##}s: {entry.Value}.");
            }

            PerfCounts.Clear();
            _perfLastLogTime = now;
        }
    }
}
