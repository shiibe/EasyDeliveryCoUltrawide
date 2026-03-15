using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        private const string PrefKeyFovLegacy = "UltrawideFovOverride";
        internal const string PrefKeyFovThirdPerson = "UltrawideFovOverride_ThirdPerson";
        internal const string PrefKeyFovFirstPerson = "UltrawideFovOverride_FirstPerson";
        internal const string PrefKeyPixelationMode = "UltrawidePixelationMode";
        private const string PrefKeyPixelationModeVersion = "UltrawidePixelationModeVersion";
        private const float DefaultAspect = 16f / 9f;

        private static ConfigEntry<bool> _enableMod;
        private static ConfigEntry<bool> _enableHudFix;
        private static ConfigEntry<bool> _debugMode;
        private static ConfigEntry<bool> _perfLogging;
        private static ConfigEntry<float> _perfLogIntervalSeconds;
        private static ConfigEntry<string> _aspectRatio;

        private static ConfigEntry<bool> _desktopMenuIconVisible;
        private static ConfigEntry<string> _desktopMenuIconX;
        private static ConfigEntry<string> _desktopMenuIconY;

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

        private static int _pixelationMode = -1;

        private static bool _insideLookupInit;
        private static Type _ambianceType;
        private static FieldInfo _ambiancePlayerInsideField;
        private static UnityEngine.Object _ambianceInstance;

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

            if (TryGetSavedFov(IsFirstPersonViewActive(), out float savedFov))
            {
                ApplyFovOverride(savedFov);
            }

            GetPixelationMode();
            RefreshPixelation();
        }

        internal static int GetPixelationMode()
        {
            if (_pixelationMode >= 0)
            {
                return _pixelationMode;
            }

            int raw = PlayerPrefs.GetInt(PrefKeyPixelationMode, 3);
            int version = PlayerPrefs.GetInt(PrefKeyPixelationModeVersion, 1);

            // v1 mapping: 0 Disable, 1 Fine, 2 Default, 3 Large
            // v2 mapping: 0 None, 1 Finer, 2 Fine, 3 Default, 4 Large
            if (version < 2 && PlayerPrefs.HasKey(PrefKeyPixelationMode))
            {
                int migrated = raw;
                switch (raw)
                {
                    case 0:
                        migrated = 0;
                        break;
                    case 1:
                        migrated = 2;
                        break;
                    case 2:
                        migrated = 3;
                        break;
                    case 3:
                        migrated = 4;
                        break;
                }
                raw = migrated;
                PlayerPrefs.SetInt(PrefKeyPixelationModeVersion, 2);
                PlayerPrefs.SetInt(PrefKeyPixelationMode, raw);
            }

            _pixelationMode = Mathf.Clamp(raw, 0, 4);
            return _pixelationMode;
        }

        internal static void SavePixelationMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, 4);
            _pixelationMode = mode;
            PlayerPrefs.SetInt(PrefKeyPixelationModeVersion, 2);
            PlayerPrefs.SetInt(PrefKeyPixelationMode, mode);
            RefreshPixelation();
        }

        internal static int GetPixelationDivisor()
        {
            switch (GetPixelationMode())
            {
                case 0:
                    return 1;
                case 1:
                    return 2;
                case 2:
                    return 3;
                case 3:
                    return 4;
                case 4:
                    return 5;
                default:
                    return 3;
            }
        }

        internal static bool TryGetSavedFov(bool firstPerson, out float fov)
        {
            string key = firstPerson ? PrefKeyFovFirstPerson : PrefKeyFovThirdPerson;
            fov = PlayerPrefs.GetFloat(key, -1f);
            if (fov >= 1f)
            {
                return true;
            }

            if (!firstPerson)
            {
                fov = PlayerPrefs.GetFloat(PrefKeyFovLegacy, -1f);
                if (fov >= 1f)
                {
                    return true;
                }
            }

            fov = 0f;
            return false;
        }

        internal static float GetSavedFovOrDefault(bool firstPerson, float fallback)
        {
            return TryGetSavedFov(firstPerson, out float fov) ? fov : fallback;
        }

        internal static void SaveFovOverride(bool firstPerson, float fov)
        {
            PlayerPrefs.SetFloat(firstPerson ? PrefKeyFovFirstPerson : PrefKeyFovThirdPerson, fov);
            if (IsFirstPersonViewActive() == firstPerson)
            {
                ApplyFovOverride(fov);
            }
        }

        internal static bool IsFirstPersonViewActive()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<sCameraController>();
            return controller != null && controller.firstPerson && !controller.fixedPerspective;
        }

        internal static void ApplyFovOverride(float fov)
        {
            if (IsPlayerInsideBuilding())
            {
                return;
            }

            var pauseSystem = PauseSystem.pauseSystem;
            if (pauseSystem != null && pauseSystem.mainCamera != null)
            {
                PauseSystem.FOV = fov;
                pauseSystem.mainCamera.fieldOfView = fov;
                return;
            }

            PauseSystem.FOV = fov;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.fieldOfView = fov;
            }
        }

        private static bool IsPlayerInsideBuilding()
        {
            try
            {
                if (!_insideLookupInit)
                {
                    _insideLookupInit = true;
                    _ambianceType = AccessTools.TypeByName("sAmbiance");
                    _ambiancePlayerInsideField = _ambianceType != null ? AccessTools.Field(_ambianceType, "playerInside") : null;
                }

                if (_ambianceType == null || _ambiancePlayerInsideField == null)
                {
                    return false;
                }

                if (_ambianceInstance == null)
                {
                    _ambianceInstance = UnityEngine.Object.FindFirstObjectByType(_ambianceType);
                }

                if (_ambianceInstance == null)
                {
                    return false;
                }

                var insideGo = _ambiancePlayerInsideField.GetValue(_ambianceInstance) as GameObject;
                return insideGo != null && insideGo.activeSelf;
            }
            catch
            {
                return false;
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
