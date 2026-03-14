using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        private static void LogPixelPerfectScale(Component component, float aspect, float sourceAspect)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            int id = component.GetInstanceID();
            if (PixelViewAspects.TryGetValue(id, out var previous) && Mathf.Abs(previous - aspect) < 0.001f)
            {
                return;
            }

            PixelViewAspects[id] = aspect;
            LogDebug($"Resized pixelPerfectView on {component.name} to aspect {aspect:0.###} (source {sourceAspect:0.###}).");
        }

        private static void LogPixelPerfectSkip(Component component, float sourceAspect)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            int id = component.GetInstanceID();
            if (PixelViewSkipAspects.TryGetValue(id, out var previous) && Mathf.Abs(previous - sourceAspect) < 0.001f)
            {
                return;
            }

            PixelViewSkipAspects[id] = sourceAspect;
            LogDebug($"Skipped pixelPerfectView resize on {component.name} (source aspect {sourceAspect:0.###}).");
        }
    }
}
