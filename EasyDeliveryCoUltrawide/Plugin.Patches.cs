using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            _log = Logger;
            _enableMod = Config.Bind("General", "enable_mod", true, "Enable ultrawide fixes.");
            _aspectRatio = Config.Bind("General", "aspect_ratio", "auto", "Aspect ratio override. Examples: auto (display), window, 21:9, 32:9, 2.39.");
            _debugMode = Config.Bind("General", "debug_logging", false, "Log debug information about applied adjustments.");
            _enableHudFix = Config.Bind("HUD", "enable_hud_fix", true, "Enable HUD scaling and positioning fixes.");

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
            PatchByName(harmony, "IntroDotExe", "Setup", postfix: nameof(IntroDotExe_Setup_Postfix));
            PatchByName(harmony, "ChooseExe", "Setup", postfix: nameof(ChooseExe_Setup_Postfix));
            PatchByName(harmony, "CamDotExe", "Start", postfix: nameof(CamDotExe_Start_Postfix));
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

        private static bool ShouldOverrideSplitScreen()
        {
            return ShouldApply() && IsUltrawide();
        }

        #pragma warning disable IDE1006
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
                ApplyCameraAspect(camera);
                ApplyCameraViewport(camera);
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
                ApplyCameraAspect(mainCamera);
            }

            ApplyAllCameras();
            ScaleOverlayBackdrops();
        }

        private static void PauseSystem_SetResolution_Postfix()
        {
            if (!ShouldApply())
            {
                return;
            }

            ApplyAllCameras();
            ScaleOverlayBackdrops();
        }

        private static void PauseSystem_SetFullscreen_Postfix()
        {
            if (!ShouldApply())
            {
                return;
            }

            ApplyAllCameras();
            ScaleOverlayBackdrops();
        }

        private static void SHud_Init_Postfix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }

            if (ShouldApplyHudFix())
            {
                UpdateHudRendererAspect(__instance);
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

        private static void IntroDotExe_Setup_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            var field = AccessTools.Field(__instance.GetType(), "menuCamera");
            var menuCamera = field != null ? field.GetValue(__instance) as GameObject : null;
            ApplyMenuCameraAspect(menuCamera);
        }

        private static void ChooseExe_Setup_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            var field = AccessTools.Field(__instance.GetType(), "menuCamera");
            var menuCamera = field != null ? field.GetValue(__instance) as GameObject : null;
            ApplyMenuCameraAspect(menuCamera);
        }

        private static void CamDotExe_Start_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            var freeCamField = AccessTools.Field(__instance.GetType(), "freecam");
            if (freeCamField == null)
            {
                return;
            }

            var freeCam = freeCamField.GetValue(__instance) as Component;
            if (freeCam == null)
            {
                return;
            }

            var camField = AccessTools.Field(freeCam.GetType(), "cam");
            if (camField == null)
            {
                return;
            }

            var cam = camField.GetValue(freeCam) as Camera;
            if (cam == null)
            {
                LogDebug("CamDotExe freecam camera not found for aspect fix.");
                return;
            }

            ApplyMenuCameraAspect(cam);
            LogDebug($"Applied CamDotExe freecam aspect fix to {cam.name} ({cam.GetInstanceID()}).");
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

        private static void Steleporter_Teleport_Postfix()
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
    }
}
