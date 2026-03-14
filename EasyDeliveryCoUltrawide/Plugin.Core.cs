using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        private const string PrefKeyFov = "UltrawideFovOverride";
        private const float DefaultAspect = 16f / 9f;

        private static ConfigEntry<bool> _enableMod;
        private static ConfigEntry<bool> _enableHudFix;
        private static ConfigEntry<bool> _debugMode;
        private static ConfigEntry<string> _aspectRatio;

        private static ManualLogSource _log;
        private static readonly Dictionary<int, Vector2> OverlayTargetSizes = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, float> PixelViewAspects = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> PixelViewSkipAspects = new Dictionary<int, float>();
        private static readonly Dictionary<Type, PixelPerfectFields> PixelPerfectFieldCache = new Dictionary<Type, PixelPerfectFields>();
        private static readonly Dictionary<int, PixelPerfectState> PixelPerfectStates = new Dictionary<int, PixelPerfectState>();
        private static readonly Dictionary<string, int> PerfCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, MiniRendererFields> MiniRendererFieldCache = new Dictionary<Type, MiniRendererFields>();
        private static readonly Dictionary<int, Vector3> HudDisplayOriginalScales = new Dictionary<int, Vector3>();
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int AlphaTexId = Shader.PropertyToID("_AlphaTex");
        private static float _perfLastLogTime;

        private static bool ShouldApply()
        {
            return _enableMod != null && _enableMod.Value;
        }

        private static bool ShouldApplyHudFix()
        {
            return ShouldApply() && _enableHudFix != null && _enableHudFix.Value;
        }

        internal static bool GetEnableMod()
        {
            return _enableMod != null && _enableMod.Value;
        }

        internal static void SetEnableMod(bool value)
        {
            if (_enableMod == null)
            {
                return;
            }

            _enableMod.Value = value;
        }

        internal static bool GetEnableHudFix()
        {
            return _enableHudFix != null && _enableHudFix.Value;
        }

        internal static void SetEnableHudFix(bool value)
        {
            if (_enableHudFix == null)
            {
                return;
            }

            _enableHudFix.Value = value;
        }

        internal static bool GetDebugMode()
        {
            return _debugMode != null && _debugMode.Value;
        }

        internal static void SetDebugMode(bool value)
        {
            if (_debugMode == null)
            {
                return;
            }

            _debugMode.Value = value;
        }

        internal static string GetAspectRatioValue()
        {
            return _aspectRatio != null ? _aspectRatio.Value : "auto";
        }

        internal static void SetAspectRatioValue(string value)
        {
            if (_aspectRatio == null)
            {
                return;
            }

            _aspectRatio.Value = value ?? "auto";
        }

        internal static void ApplySavedMenuSettings()
        {
            if (!ShouldApply())
            {
                return;
            }

            float savedFov = PlayerPrefs.GetFloat(PrefKeyFov, -1f);
            if (savedFov >= 1f)
            {
                ApplyFovOverride(savedFov);
            }
        }

        internal static void ApplyFovOverride(float fov)
        {
            var pauseSystem = PauseSystem.pauseSystem;
            if (pauseSystem != null && pauseSystem.mainCamera != null)
            {
                float value = Mathf.InverseLerp(pauseSystem.FOVmin + 0.1f, pauseSystem.FOVmax, fov);
                pauseSystem.UpdateFOV(Mathf.Clamp01(value));
                return;
            }

            PauseSystem.FOV = fov;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.fieldOfView = fov;
            }
        }

        private static float GetTargetAspect()
        {
            if (_aspectRatio == null)
            {
                return GetDisplayAspect();
            }

            if (!TryParseAspect(_aspectRatio.Value, out float aspect, out bool useWindow))
            {
                return GetDisplayAspect();
            }

            if (useWindow)
            {
                return GetWindowAspect();
            }

            if (aspect <= 0.1f)
            {
                return GetDisplayAspect();
            }

            return aspect;
        }

        private static float GetDisplayAspect()
        {
            if (Screen.currentResolution.height == 0)
            {
                return DefaultAspect;
            }

            return (float)Screen.currentResolution.width / Screen.currentResolution.height;
        }

        private static float GetWindowAspect()
        {
            if (Screen.height == 0)
            {
                return DefaultAspect;
            }

            return (float)Screen.width / Screen.height;
        }

        private static bool TryParseAspect(string value, out float aspect, out bool useWindow)
        {
            aspect = 0f;
            useWindow = false;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "display" || normalized == "auto" || normalized == "match")
            {
                return true;
            }

            if (normalized == "default" || normalized == "native" || normalized == "vanilla")
            {
                aspect = DefaultAspect;
                return true;
            }

            if (normalized == "window")
            {
                useWindow = true;
                return true;
            }

            if (normalized.Contains(":"))
            {
                string[] parts = normalized.Split(':');
                if (parts.Length == 2 && float.TryParse(parts[0], out float w) && float.TryParse(parts[1], out float h) && h > 0.01f)
                {
                    aspect = w / h;
                    return true;
                }
            }

            if (float.TryParse(normalized, out float direct) && direct > 0.1f)
            {
                aspect = direct;
                return true;
            }

            return false;
        }

        private static bool IsUltrawide()
        {
            return GetWindowAspect() > DefaultAspect + 0.01f;
        }

        private static Rect BuildFullRect()
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        private static bool ShouldForceViewport(Camera camera, Rect rect)
        {
            if (!ShouldApply() || !IsUltrawide())
            {
                return false;
            }

            if (camera == null)
            {
                return false;
            }

            if (!IsPrimaryGameplayCamera(camera))
            {
                return false;
            }

            if (rect.width >= 0.99f && rect.height >= 0.99f)
            {
                return false;
            }

            bool letterboxVertical = rect.width >= 0.98f && rect.height < 0.98f && rect.x <= 0.01f;
            bool letterboxHorizontal = rect.height >= 0.98f && rect.width < 0.98f && rect.y <= 0.01f;
            return letterboxVertical || letterboxHorizontal;
        }

        private static bool IsPrimaryGameplayCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            if (string.Equals(camera.name, "Camera Persp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(camera.name, "Camera", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(camera.name, "RearViewCam", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return camera == Camera.main;
        }

        private static void ApplyCameraViewport(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (!ShouldApply())
            {
                return;
            }

            if (!IsUltrawide())
            {
                return;
            }

            if (!ShouldForceViewport(camera, camera.rect))
            {
                return;
            }

            camera.rect = BuildFullRect();
            LogDebug($"Resized viewport to full screen for {camera.name} ({camera.GetInstanceID()}).");
        }

        private static void ApplyCameraAspect(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (!ShouldApply() || !IsUltrawide())
            {
                return;
            }

            if (!IsPrimaryGameplayCamera(camera))
            {
                return;
            }

            float targetAspect = GetTargetAspect();
            if (targetAspect <= 0.01f)
            {
                return;
            }

            if (Mathf.Abs(camera.aspect - targetAspect) < 0.0001f)
            {
                return;
            }

            camera.aspect = targetAspect;
            LogDebug($"Forced aspect for {camera.name} ({camera.GetInstanceID()}) to {targetAspect:0.###}.");
        }

        private static void ApplyMenuCameraAspect(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (!ShouldApply() || !IsUltrawide())
            {
                return;
            }

            float targetAspect = GetTargetAspect();
            if (targetAspect <= 0.01f)
            {
                return;
            }

            if (Mathf.Abs(camera.aspect - targetAspect) < 0.0001f)
            {
                return;
            }

            camera.aspect = targetAspect;
            LogDebug($"Forced menu aspect for {camera.name} ({camera.GetInstanceID()}) to {targetAspect:0.###}.");
        }

        private static void ApplyMenuCameraAspect(GameObject menuCameraObject)
        {
            if (menuCameraObject == null)
            {
                return;
            }

            var cam = menuCameraObject.GetComponentInChildren<Camera>(true);
            ApplyMenuCameraAspect(cam);
        }

        private static void ApplyAllCameras()
        {
            if (!ShouldApply())
            {
                return;
            }

            if (!IsUltrawide())
            {
                return;
            }

            foreach (var camera in Camera.allCameras)
            {
                ApplyCameraAspect(camera);
                ApplyCameraViewport(camera);
            }
        }
    }
}
