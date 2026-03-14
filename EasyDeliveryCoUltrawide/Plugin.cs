using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.ultrawide";
        public const string PluginName = "Ultrawide Mod";
        public const string PluginVersion = "1.0.0";

        private const float DefaultAspect = 16f / 9f;
        private const float FovMin = 40f;
        private const float FovMax = 120f;

        private static ConfigEntry<bool> _enableMod;
        private static ConfigEntry<bool> _debugMode;
        private static ConfigEntry<string> _aspectRatio;
        private static ConfigEntry<float> _fovOverride;
        private static ConfigEntry<float> _fovCameraPersp;
        private static ConfigEntry<float> _fovCamera;
        private static ConfigEntry<float> _fovRearViewCam;

        private static ManualLogSource _log;
        private static readonly Dictionary<int, Vector2> _overlayTargetSizes = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, float> _pixelViewAspects = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> _pixelViewSkipAspects = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> _fovLogged = new Dictionary<int, float>();
        private static readonly Dictionary<string, float> _fovOverrideMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, OverlayScaleState> _overlayScaleStates = new Dictionary<int, OverlayScaleState>();
        private static readonly Dictionary<Type, PixelPerfectFields> _pixelPerfectFieldCache = new Dictionary<Type, PixelPerfectFields>();
        private static readonly Dictionary<int, PixelPerfectState> _pixelPerfectStates = new Dictionary<int, PixelPerfectState>();
        private static readonly Dictionary<string, int> _perfCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static float _perfLastLogTime;

        private void Awake()
        {
            _log = Logger;
            _enableMod = Config.Bind("General", "enable_ultrawide_mode", true, "Enable ultrawide fixes.");
            _aspectRatio = Config.Bind("General", "aspect_ratio", "auto", "Aspect ratio override. Examples: auto (display), window, 21:9, 32:9, 2.39.");
            _debugMode = Config.Bind("General", "debug_logging", false, "Log debug information about applied adjustments.");
            _fovOverride = Config.Bind("Camera", "fov_override", 0f, "Override gameplay camera FOV (40-120). Set to 0 to use in-game setting.");
            _fovCameraPersp = Config.Bind("Camera", "fov_camera_persp", 0f, "Override FOV for camera name 'Camera Persp' (Fixed Camera) (40-120). 0 uses global.");
            _fovCamera = Config.Bind("Camera", "fov_camera", 0f, "Override FOV for camera name 'Camera' (First-person view camera) (40-120). 0 uses global.");
            _fovRearViewCam = Config.Bind("Camera", "fov_rearviewcam", 0f, "Override FOV for camera name 'RearViewCam' (Driving/Walking camera) (40-120). 0 uses global.");

            if (!_enableMod.Value)
            {
                _log.LogInfo("Ultrawide mod disabled via config.");
                return;
            }

            var harmony = new Harmony(PluginGuid);
            PatchByName(harmony, "PlayerManager", "SetupCameras", postfix: nameof(PlayerManager_SetupCameras_Postfix));
            PatchByName(harmony, "RaceManager", "StartingCutScene", prefix: nameof(RaceManager_StartingCutScene_Prefix), postfix: nameof(RaceManager_StartingCutScene_Postfix));
            PatchByName(harmony, "PauseSystem", "Start", postfix: nameof(PauseSystem_Start_Postfix));
            PatchByName(harmony, "PauseSystem", "SetResolution", postfix: nameof(PauseSystem_SetResolution_Postfix));
            PatchByName(harmony, "PauseSystem", "SetFullscreen", postfix: nameof(PauseSystem_SetFullscreen_Postfix));
            PatchByName(harmony, "PauseSystem", "UpdateFOV", prefix: nameof(PauseSystem_UpdateFOV_Prefix));
            PatchByName(harmony, "MiniRenderer", "Start", prefix: nameof(MiniRenderer_Start_Prefix));
            PatchByName(harmony, "MiniRenderer", "Start", postfix: nameof(MiniRenderer_Start_Postfix));
            PatchByName(harmony, "pixelPerfectView", "AdjustViewPlane", prefix: nameof(PixelPerfectView_AdjustViewPlane_Prefix));
            PatchByName(harmony, "sHUD", "Init", postfix: nameof(SHud_Init_Postfix));
            PatchByName(harmony, "sHUD", "FadeToBlack", prefix: nameof(SHud_FadeToBlack_Prefix));
            PatchByName(harmony, "sHUD", "DipToBlack", prefix: nameof(SHud_DipToBlack_Prefix));
            PatchByName(harmony, "ScreenSystem", "Init", postfix: nameof(ScreenSystem_Init_Postfix));
            PatchByName(harmony, "ScreenSystem", "DoTransition", postfix: nameof(ScreenSystem_DoTransition_Postfix));
            PatchByName(harmony, "SceneTransition", "Start", postfix: nameof(SceneTransition_Start_Postfix));
            PatchByName(harmony, "sTeleporter", "Teleport", postfix: nameof(Steleporter_Teleport_Postfix));
        }

        private static bool ShouldApply()
        {
            return _enableMod != null && _enableMod.Value;
        }

        private static bool HasFovOverride()
        {
            return _fovOverride != null && _fovOverride.Value > 0.01f;
        }

        private static float GetFovOverride()
        {
            if (!HasFovOverride())
            {
                return 0f;
            }

            return Mathf.Clamp(_fovOverride.Value, FovMin, FovMax);
        }

        private static float GetCameraFovOverride(Camera camera)
        {
            if (camera == null)
            {
                return 0f;
            }

            float namedOverride = GetNamedFovOverride(camera.name);
            if (namedOverride > 0.01f)
            {
                return namedOverride;
            }

            return GetFovOverride();
        }

        private static float GetNamedFovOverride(string cameraName)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
            {
                return 0f;
            }

            if (string.Equals(cameraName, "Camera Persp", StringComparison.OrdinalIgnoreCase))
            {
                return ClampFovConfig(_fovCameraPersp);
            }

            if (string.Equals(cameraName, "Camera", StringComparison.OrdinalIgnoreCase))
            {
                return ClampFovConfig(_fovCamera);
            }

            if (string.Equals(cameraName, "RearViewCam", StringComparison.OrdinalIgnoreCase))
            {
                return ClampFovConfig(_fovRearViewCam);
            }

            return 0f;
        }

        private static float ClampFovConfig(ConfigEntry<float> entry)
        {
            if (entry == null)
            {
                return 0f;
            }

            float value = entry.Value;
            if (value <= 0.01f)
            {
                return 0f;
            }

            return Mathf.Clamp(value, FovMin, FovMax);
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

        private static float GetNormalizedAspect()
        {
            var current = GetWindowAspect();
            var target = GetTargetAspect();
            if (Mathf.Approximately(target, 0f))
            {
                return DefaultAspect;
            }

            return current / target;
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
                ApplyCameraViewport(camera);
            }
        }


        private static void ApplyCameraFov(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (!ShouldApply() || !HasFovOverride())
            {
                return;
            }

            float fov = GetCameraFovOverride(camera);
            if (fov <= 0.01f)
            {
                return;
            }

            camera.fieldOfView = fov;
            LogFovOverride(camera, fov);
        }

        private static void ApplyAllCameraFov()
        {
            if (!ShouldApply() || !HasFovOverride())
            {
                return;
            }

            foreach (var camera in Camera.allCameras)
            {
                ApplyCameraFov(camera);
            }
        }

        private static void ApplyPauseSystemFov(float fov)
        {
            if (fov <= 0.01f)
            {
                return;
            }

            var pauseType = AccessTools.TypeByName("PauseSystem");
            if (pauseType == null)
            {
                return;
            }

            var fovField = AccessTools.Field(pauseType, "FOV");
            if (fovField != null)
            {
                fovField.SetValue(null, fov);
            }

            var pauseSystemField = AccessTools.Field(pauseType, "pauseSystem");
            var pauseInstance = pauseSystemField != null ? pauseSystemField.GetValue(null) : null;
            if (pauseInstance == null)
            {
                return;
            }

            var mainCameraField = AccessTools.Field(pauseType, "mainCamera");
            var mainCamera = mainCameraField != null ? mainCameraField.GetValue(pauseInstance) as Camera : null;
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = fov;
                LogDebug($"Forced PauseSystem main camera FOV to {fov:0.##}.");
            }
        }


        private static bool ShouldOverrideSplitScreen()
        {
            return ShouldApply() && IsUltrawide();
        }

        private static void PlayerManager_SetupCameras_Postfix(object __instance)
        {
            if (!ShouldOverrideSplitScreen())
            {
                return;
            }

            if (__instance == null)
            {
                return;
            }

            var playersField = AccessTools.Field(__instance.GetType(), "players");
            if (playersField == null)
            {
                return;
            }

            var players = playersField.GetValue(__instance) as System.Collections.IEnumerable;
            if (players == null)
            {
                return;
            }

            foreach (var player in players)
            {
                var go = player as GameObject;
                if (go == null)
                {
                    continue;
                }

                var camera = go.GetComponentInChildren<Camera>();
                ApplyCameraViewport(camera);
                ApplyCameraFov(camera);
            }
        }

        private static void RaceManager_StartingCutScene_Prefix(object p, ref Rect __state)
        {
            if (!ShouldOverrideSplitScreen())
            {
                return;
            }

            var cam = GetRacePlayerCamera(p);
            if (cam == null)
            {
                return;
            }

            __state = cam.rect;
            cam.rect = BuildFullRect();
        }

        private static void RaceManager_StartingCutScene_Postfix(object p, Rect __state)
        {
            if (!ShouldOverrideSplitScreen())
            {
                return;
            }

            var cam = GetRacePlayerCamera(p);
            if (cam == null)
            {
                return;
            }

            cam.rect = __state;
        }

        private static void PauseSystem_Start_Postfix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }

            if (__instance != null)
            {
                var mainCameraField = AccessTools.Field(__instance.GetType(), "mainCamera");
                var mainCamera = mainCameraField != null ? mainCameraField.GetValue(__instance) as Camera : null;
                ApplyCameraFov(mainCamera);
            }

            ApplyAllCameras();
            ApplyAllCameraFov();
            ScaleOverlayBackdrops();
        }

        private static void PauseSystem_SetResolution_Postfix()
        {
            if (!ShouldApply())
            {
                return;
            }

            ApplyAllCameras();
            ApplyAllCameraFov();
            ScaleOverlayBackdrops();
        }

        private static void PauseSystem_SetFullscreen_Postfix()
        {
            if (!ShouldApply())
            {
                return;
            }

            ApplyAllCameras();
            ApplyAllCameraFov();
            ScaleOverlayBackdrops();
        }

        private static bool PauseSystem_UpdateFOV_Prefix()
        {
            if (!ShouldApply() || !HasFovOverride())
            {
                return true;
            }

            float targetFov = GetFovOverride();
            ApplyPauseSystemFov(targetFov);

            ApplyAllCameraFov();
            return false;
        }

        private static bool PixelPerfectView_AdjustViewPlane_Prefix(object __instance)
        {
            PerfCount("pixelPerfectView.AdjustViewPlane");
            if (!ShouldApply())
            {
                return true;
            }


            if (__instance == null)
            {
                return true;
            }

            var component = __instance as Component;
            if (component == null)
            {
                return true;
            }

            var type = __instance.GetType();
            if (!_pixelPerfectFieldCache.TryGetValue(type, out var fields))
            {
                fields = new PixelPerfectFields
                {
                    GameCamera = AccessTools.Field(type, "gameCamera"),
                    MenuWidth = AccessTools.Field(type, "menuWidth"),
                    MenuHeight = AccessTools.Field(type, "menuHeight")
                };
                _pixelPerfectFieldCache[type] = fields;
            }

            if (fields.GameCamera == null || fields.MenuWidth == null || fields.MenuHeight == null)
            {
                return true;
            }

            var cam = fields.GameCamera.GetValue(__instance) as Camera;
            if (cam == null)
            {
                return true;
            }

            float menuWidth = (float)fields.MenuWidth.GetValue(__instance);
            float menuHeight = (float)fields.MenuHeight.GetValue(__instance);
            if (menuWidth <= 0.01f || menuHeight <= 0.01f)
            {
                return true;
            }

            float sourceAspect = menuWidth / menuHeight;
            bool isWideTarget = sourceAspect > 1.65f && sourceAspect < 1.9f;
            if (!isWideTarget)
            {
                LogPixelPerfectSkip(component, sourceAspect);
                return true;
            }

            float num = 2f * component.transform.localPosition.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = GetWindowAspect();
            int id = component.GetInstanceID();
            var state = new PixelPerfectState
            {
                Width = Screen.width,
                Height = Screen.height,
                Aspect = aspect,
                Fov = cam.fieldOfView,
                Z = component.transform.localPosition.z
            };

            if (!_pixelPerfectStates.TryGetValue(id, out var previous) || !previous.Equals(state))
            {
                _pixelPerfectStates[id] = state;
                component.transform.localScale = new Vector3(num * aspect, num, 1f);
            }
            LogPixelPerfectScale(component, aspect, sourceAspect);
            return false;
        }

        private static void SHud_Init_Postfix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }

            ScaleBackdropFromField(__instance, "backdrop", Camera.main, "sHUD");
        }

        private static void SHud_FadeToBlack_Prefix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }

            ScaleBackdropFromField(__instance, "backdrop", Camera.main, "sHUD");
        }

        private static void SHud_DipToBlack_Prefix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }

            ScaleBackdropFromField(__instance, "backdrop", Camera.main, "sHUD");
        }

        private static void ScreenSystem_Init_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }


            var menuCameraField = AccessTools.Field(__instance.GetType(), "menuCamera");
            var menuCamera = menuCameraField != null ? menuCameraField.GetValue(__instance) as Camera : null;
            menuCamera = ResolveMenuCamera(menuCamera);
            ScaleBackdropFromField(__instance, "backdrop", menuCamera, "ScreenSystem");
            ScaleOverlaySprites(__instance as Component, menuCamera, "ScreenSystem");
        }

        private static void ScreenSystem_DoTransition_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }


            var menuCameraField = AccessTools.Field(__instance.GetType(), "menuCamera");
            var menuCamera = menuCameraField != null ? menuCameraField.GetValue(__instance) as Camera : null;
            menuCamera = ResolveMenuCamera(menuCamera);
            ScaleBackdropFromField(__instance, "backdrop", menuCamera, "ScreenSystem");
            ScaleOverlaySprites(__instance as Component, menuCamera, "ScreenSystem");
        }

        private static void ScreenSystem_FrameUpdate_Postfix(object __instance)
        {
            PerfCount("ScreenSystem.FrameUpdate");
            if (!ShouldApply() || __instance == null)
            {
                return;
            }


            var pauseType = AccessTools.TypeByName("PauseSystem");
            if (pauseType != null)
            {
                var pausedField = AccessTools.Field(pauseType, "paused");
                if (pausedField != null)
                {
                    var paused = (bool)pausedField.GetValue(null);
                    if (!paused)
                    {
                        return;
                    }
                }
            }

            var menuCameraField = AccessTools.Field(__instance.GetType(), "menuCamera");
            var menuCamera = menuCameraField != null ? menuCameraField.GetValue(__instance) as Camera : null;
            menuCamera = ResolveMenuCamera(menuCamera);
            var owner = __instance as Component;
            if (!ShouldRescaleOverlay(owner, menuCamera))
            {
                return;
            }
            ScaleBackdropFromField(__instance, "backdrop", menuCamera, "ScreenSystem");
            ScaleOverlaySprites(owner, menuCamera, "ScreenSystem");
        }

        private static void MenuScreenTransition_Update_Postfix(object __instance)
        {
            PerfCount("MenuScreenTransition.Update");
            if (!ShouldApply())
            {
                return;
            }


            var menuCamera = ResolveMenuCamera(null);
            var owner = __instance as Component;
            if (!ShouldRescaleOverlay(owner, menuCamera))
            {
                return;
            }
            ScaleBackdropFromField(__instance, "backdrop", menuCamera, "MenuScreenTransition");
            ScaleOverlaySprites(owner, menuCamera, "MenuScreenTransition");
        }

        private static void SceneTransition_Start_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }


            var srField = AccessTools.Field(__instance.GetType(), "sr");
            var sr = srField != null ? srField.GetValue(__instance) as SpriteRenderer : null;
            if (sr == null)
            {
                var component = __instance as Component;
                sr = component != null ? component.GetComponent<SpriteRenderer>() : null;
            }

            ScaleSpriteToCamera(sr, Camera.main, "SceneTransition");
            ScaleOverlaySprites(__instance as Component, Camera.main, "SceneTransition");
        }

        private static void Steleporter_Teleport_Postfix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }


            var hudType = AccessTools.TypeByName("sHUD");
            if (hudType == null)
            {
                return;
            }

            var hud = UnityEngine.Object.FindFirstObjectByType(hudType);
            if (hud == null)
            {
                return;
            }

            ScaleBackdropFromField(hud, "backdrop", Camera.main, "sHUD");
        }

        private static void ScaleOverlayBackdrops()
        {
            if (!ShouldApply())
            {
                return;
            }

            var menuCamera = ResolveMenuCamera(null);
            ScaleBackdropByTypeName("sHUD", "backdrop", Camera.main);
            ScaleBackdropByTypeName("ScreenSystem", "backdrop", menuCamera);
            ScaleBackdropByTypeName("MenuScreenTransition", "backdrop", menuCamera);
        }

        private static void ScaleBackdropByTypeName(string typeName, string fieldName, Camera fallbackCamera)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return;
            }

            var objects = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (objects == null)
            {
                return;
            }

            foreach (var obj in objects)
            {
                ScaleBackdropFromField(obj, fieldName, fallbackCamera, typeName);
            }
        }

        private static void ScaleBackdropFromField(object instance, string fieldName, Camera fallbackCamera, string label)
        {
            if (instance == null)
            {
                return;
            }

            var field = AccessTools.Field(instance.GetType(), fieldName);
            if (field == null)
            {
                return;
            }

            var spriteRenderer = field.GetValue(instance) as SpriteRenderer;
            ScaleSpriteToCamera(spriteRenderer, fallbackCamera, label);
        }

        private static void ScaleOverlaySprites(Component owner, Camera fallbackCamera, string label)
        {
            if (owner == null)
            {
                return;
            }

            var spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                return;
            }

            foreach (var spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer == null || spriteRenderer.sprite == null)
                {
                    continue;
                }

                if (!IsOverlaySprite(spriteRenderer))
                {
                    continue;
                }

                ScaleSpriteToCamera(spriteRenderer, fallbackCamera, label);
            }
        }

        private static bool IsOverlaySprite(SpriteRenderer spriteRenderer)
        {
            string name = spriteRenderer.gameObject.name;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            if (lower.Contains("backdrop") || lower.Contains("overlay") || lower.Contains("fade") || lower.Contains("curtain") || lower.Contains("black"))
            {
                return true;
            }

            if (lower.Contains("square") || lower.Contains("otherbackdrop"))
            {
                return true;
            }

            string path = GetHierarchyPath(spriteRenderer.transform).ToLowerInvariant();
            return path.Contains("pausemenuurp") && (path.Contains("menudisplay") || path.Contains("scenetransition"));
        }

        private static Camera ResolveMenuCamera(Camera menuCamera)
        {
            if (menuCamera != null)
            {
                return menuCamera;
            }

            foreach (var camera in Camera.allCameras)
            {
                if (camera != null && string.Equals(camera.name, "Camera Persp", StringComparison.OrdinalIgnoreCase))
                {
                    return camera;
                }
            }

            return Camera.main;
        }

        private static void ScaleSpriteToCamera(SpriteRenderer spriteRenderer, Camera fallbackCamera, string label)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            Camera cam = fallbackCamera;
            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam == null)
            {
                return;
            }

            Vector2 targetSize = GetCameraWorldSize(cam, spriteRenderer.transform.position);
            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0.01f || spriteSize.y <= 0.01f)
            {
                return;
            }

            Vector3 scale = spriteRenderer.transform.localScale;
            scale.x = targetSize.x / spriteSize.x;
            scale.y = targetSize.y / spriteSize.y;
            spriteRenderer.transform.localScale = scale;
            LogOverlayScale(spriteRenderer, targetSize, cam, label);
        }

        private static Vector2 GetCameraWorldSize(Camera cam, Vector3 worldPosition)
        {
            if (cam.orthographic)
            {
                float height = cam.orthographicSize * 2f;
                return new Vector2(height * cam.aspect, height);
            }

            float distance = Vector3.Dot(worldPosition - cam.transform.position, cam.transform.forward);
            if (Mathf.Abs(distance) < 0.01f)
            {
                distance = cam.nearClipPlane + 0.1f;
            }

            float halfHeight = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float heightPerspective = halfHeight * 2f;
            return new Vector2(heightPerspective * cam.aspect, heightPerspective);
        }

        private static void LogOverlayScale(SpriteRenderer spriteRenderer, Vector2 targetSize, Camera cam, string label)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            int id = spriteRenderer.GetInstanceID();
            if (_overlayTargetSizes.TryGetValue(id, out var previous) && Approximately(previous, targetSize))
            {
                return;
            }

            _overlayTargetSizes[id] = targetSize;
            LogDebug($"Resized {label} overlay to {targetSize.x:0.###}x{targetSize.y:0.###} using {cam.name}.");
        }

        private static void LogPixelPerfectScale(Component component, float aspect, float sourceAspect)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            int id = component.GetInstanceID();
            if (_pixelViewAspects.TryGetValue(id, out var previous) && Mathf.Abs(previous - aspect) < 0.001f)
            {
                return;
            }

            _pixelViewAspects[id] = aspect;
            LogDebug($"Resized pixelPerfectView on {component.name} to aspect {aspect:0.###} (source {sourceAspect:0.###}).");
        }

        private static void LogPixelPerfectSkip(Component component, float sourceAspect)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            int id = component.GetInstanceID();
            if (_pixelViewSkipAspects.TryGetValue(id, out var previous) && Mathf.Abs(previous - sourceAspect) < 0.001f)
            {
                return;
            }

            _pixelViewSkipAspects[id] = sourceAspect;
            LogDebug($"Skipped pixelPerfectView resize on {component.name} (source aspect {sourceAspect:0.###}).");
        }


        private static void LogFovOverride(Camera camera, float fov)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            int id = camera.GetInstanceID();
            if (_fovLogged.TryGetValue(id, out var previous) && Mathf.Abs(previous - fov) < 0.01f)
            {
                return;
            }

            _fovLogged[id] = fov;
            LogDebug($"Forced FOV for {camera.name} ({id}) to {fov:0.##}.");
        }

        private static void LogDebug(string message)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            _log.LogInfo(message);
        }

        private static bool ShouldRescaleOverlay(Component owner, Camera cam)
        {
            if (owner == null || cam == null)
            {
                return true;
            }

            int id = owner.GetInstanceID();
            var state = new OverlayScaleState
            {
                Width = Screen.width,
                Height = Screen.height,
                Aspect = cam.aspect,
                Fov = cam.fieldOfView,
                OrthoSize = cam.orthographicSize,
                IsOrtho = cam.orthographic
            };

            if (_overlayScaleStates.TryGetValue(id, out var previous) && previous.Equals(state))
            {
                return false;
            }

            _overlayScaleStates[id] = state;
            return true;
        }

        private struct OverlayScaleState
        {
            public int Width;
            public int Height;
            public float Aspect;
            public float Fov;
            public float OrthoSize;
            public bool IsOrtho;

            public bool Equals(OverlayScaleState other)
            {
                return Width == other.Width
                    && Height == other.Height
                    && Mathf.Abs(Aspect - other.Aspect) < 0.0001f
                    && Mathf.Abs(Fov - other.Fov) < 0.001f
                    && Mathf.Abs(OrthoSize - other.OrthoSize) < 0.001f
                    && IsOrtho == other.IsOrtho;
            }
        }

        private struct PixelPerfectFields
        {
            public FieldInfo GameCamera;
            public FieldInfo MenuWidth;
            public FieldInfo MenuHeight;
        }

        private struct PixelPerfectState
        {
            public int Width;
            public int Height;
            public float Aspect;
            public float Fov;
            public float Z;

            public bool Equals(PixelPerfectState other)
            {
                return Width == other.Width
                    && Height == other.Height
                    && Mathf.Abs(Aspect - other.Aspect) < 0.0001f
                    && Mathf.Abs(Fov - other.Fov) < 0.001f
                    && Mathf.Abs(Z - other.Z) < 0.0001f;
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "(null)";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;
        }

        private static void MiniRenderer_Start_Prefix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }
        }

        private static void MiniRenderer_Start_Postfix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }

            EnsureMiniRendererRenderTexture(__instance);
        }

        private static void EnsureMiniRendererRenderTexture(object renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var type = renderer.GetType();
            var widthField = AccessTools.Field(type, "width");
            var heightField = AccessTools.Field(type, "height");
            var rtField = AccessTools.Field(type, "rt");
            if (widthField == null || heightField == null || rtField == null)
            {
                return;
            }

            int width = (int)widthField.GetValue(renderer);
            int height = (int)heightField.GetValue(renderer);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var rt = rtField.GetValue(renderer) as RenderTexture;
            if (rt != null && rt.width == width && rt.height == height)
            {
                return;
            }

            int depth = rt != null ? rt.depth : 32;
            var newRt = new RenderTexture(width, height, depth);
            if (rt != null)
            {
                newRt.filterMode = rt.filterMode;
            }

            rtField.SetValue(renderer, newRt);
            UpdateMiniRendererTextures(renderer, newRt);

            var renderersField = AccessTools.Field(type, "renderers");
            var rendererNameField = AccessTools.Field(type, "rendererName");
            if (renderersField != null && rendererNameField != null)
            {
                var dict = renderersField.GetValue(null) as System.Collections.IDictionary;
                var name = rendererNameField.GetValue(renderer) as string;
                if (dict != null && !string.IsNullOrEmpty(name))
                {
                    dict[name] = newRt;
                }
            }
        }

        private static void UpdateMiniRendererTextures(object renderer, RenderTexture rt)
        {
            if (renderer == null || rt == null)
            {
                return;
            }

            var type = renderer.GetType();
            var matField = AccessTools.Field(type, "mat");
            var material = matField != null ? matField.GetValue(renderer) as Material : null;
            if (material != null)
            {
                material.SetTexture("_MainTex", rt);
                material.SetTexture("_AlphaTex", rt);
                return;
            }

            var component = renderer as Component;
            if (component == null)
            {
                return;
            }

            var unityRenderer = component.GetComponent<Renderer>();
            if (unityRenderer == null)
            {
                return;
            }

            unityRenderer.material.SetTexture("_MainTex", rt);
            unityRenderer.material.SetTexture("_AlphaTex", rt);
        }


        private static Camera GetRacePlayerCamera(object racePlayer)
        {
            if (racePlayer == null)
            {
                return null;
            }

            var camField = AccessTools.Field(racePlayer.GetType(), "cam");
            if (camField == null)
            {
                return null;
            }

            return camField.GetValue(racePlayer) as Camera;
        }

        private static void PatchByName(Harmony harmony, string typeName, string methodName, string prefix = null, string postfix = null)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                if (_debugMode != null && _debugMode.Value)
                {
                    _log.LogWarning($"Type '{typeName}' not found for patch {methodName}.");
                }
                return;
            }

            MethodInfo method;
            try
            {
                method = AccessTools.Method(type, methodName);
            }
            catch (AmbiguousMatchException)
            {
                method = ResolveAmbiguousMethod(type, methodName);
            }
            if (method == null)
            {
                if (_debugMode != null && _debugMode.Value)
                {
                    _log.LogWarning($"Method '{typeName}.{methodName}' not found for patch.");
                }
                return;
            }

            HarmonyMethod prefixMethod = null;
            HarmonyMethod postfixMethod = null;

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                prefixMethod = new HarmonyMethod(typeof(Plugin), prefix);
            }

            if (!string.IsNullOrWhiteSpace(postfix))
            {
                postfixMethod = new HarmonyMethod(typeof(Plugin), postfix);
            }

            harmony.Patch(method, prefixMethod, postfixMethod);
        }

        private static MethodInfo ResolveAmbiguousMethod(Type type, string methodName)
        {
            MethodInfo best = null;
            int bestParamCount = int.MaxValue;
            for (var current = type; current != null; current = current.BaseType)
            {
                var methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var candidate in methods)
                {
                    if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int paramCount = candidate.GetParameters().Length;
                    if (paramCount == 0)
                    {
                        return candidate;
                    }

                    if (paramCount < bestParamCount)
                    {
                        bestParamCount = paramCount;
                        best = candidate;
                    }
                }
            }

            if (best == null && _debugMode != null && _debugMode.Value)
            {
                _log.LogWarning($"Ambiguous match for '{type.Name}.{methodName}', but no overload resolved.");
            }

            return best;
        }

        private static void Camera_SetRect_Prefix(Camera __instance, ref Rect __0)
        {
            PerfCount("Camera.set_rect");
            if (ShouldForceViewport(__instance, __0))
            {
                __0 = BuildFullRect();
            }
        }

        private static void PerfCount(string key)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            if (_perfCounts.TryGetValue(key, out var count))
            {
                _perfCounts[key] = count + 1;
            }
            else
            {
                _perfCounts[key] = 1;
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

            foreach (var entry in _perfCounts)
            {
                _log.LogInfo($"Perf [{entry.Key}] calls in 2s: {entry.Value}.");
            }

            _perfCounts.Clear();
            _perfLastLogTime = now;
        }


    }
}
