using System;
using HarmonyLib;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        private static float GetFogMultiplierClamped()
        {
            if (!ShouldApply())
            {
                return 1f;
            }

            float m = GetFogMultiplier();
            return Mathf.Clamp(m, 0.1f, 5f);
        }

        private static void SWeatherSystem_UpdateWeather_Postfix()
        {
            if (!ShouldApply())
            {
                return;
            }

            float m = GetFogMultiplierClamped();
            if (Mathf.Abs(m - 1f) < 0.0001f)
            {
                return;
            }

            RenderSettings.fogDensity = Mathf.Max(0.000001f, RenderSettings.fogDensity * m);
        }

        private static void FogVolume_LateUpdate_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            float m = GetFogMultiplierClamped();
            if (Mathf.Abs(m - 1f) < 0.0001f)
            {
                return;
            }

            try
            {
                var pField = AccessTools.Field(__instance.GetType(), "p");
                if (pField == null)
                {
                    return;
                }

                float p = Convert.ToSingle(pField.GetValue(__instance));
                if (Mathf.Abs(p - 1f) < 0.0001f)
                {
                    return;
                }

                RenderSettings.fogDensity = Mathf.Max(0.000001f, RenderSettings.fogDensity * m);
            }
            catch
            {
            }
        }
    }
}
