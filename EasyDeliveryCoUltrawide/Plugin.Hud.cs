using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        internal static void RefreshHudDisplayScale()
        {
            if (!ShouldApplyHudFix())
            {
                return;
            }

            var hudType = AccessTools.TypeByName("sHUD");
            if (hudType == null)
            {
                return;
            }

            var hud = UnityEngine.Object.FindFirstObjectByType(hudType) as Component;
            if (hud == null)
            {
                return;
            }

            MonoBehaviour miniRenderer = FindMiniRenderer(hud);
            if (miniRenderer == null)
            {
                return;
            }

            EnsureMiniRendererRenderTexture(miniRenderer);
            ApplyHudDisplayScale(miniRenderer);
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

        private struct MiniRendererFields
        {
            public FieldInfo Width;
            public FieldInfo Height;
        }

        private struct SHudWorldToHudPointFields
        {
            public FieldInfo Navigation;
            public FieldInfo R;
        }

        private struct HudNavigationFields
        {
            public FieldInfo Car;
        }

        private struct HudCarFields
        {
            public FieldInfo CarCamera;
        }

        private struct MiniRendererSizeFields
        {
            public FieldInfo Width;
            public FieldInfo Height;
        }

        private static readonly Dictionary<Type, SHudWorldToHudPointFields> SHudWorldToHudPointFieldCache = new Dictionary<Type, SHudWorldToHudPointFields>();
        private static readonly Dictionary<Type, HudNavigationFields> HudNavigationFieldCache = new Dictionary<Type, HudNavigationFields>();
        private static readonly Dictionary<Type, HudCarFields> HudCarFieldCache = new Dictionary<Type, HudCarFields>();
        private static readonly Dictionary<Type, MiniRendererSizeFields> MiniRendererSizeFieldCache = new Dictionary<Type, MiniRendererSizeFields>();

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
            if (!PixelPerfectFieldCache.TryGetValue(type, out var fields))
            {
                fields = new PixelPerfectFields
                {
                    GameCamera = AccessTools.Field(type, "gameCamera"),
                    MenuWidth = AccessTools.Field(type, "menuWidth"),
                    MenuHeight = AccessTools.Field(type, "menuHeight")
                };
                PixelPerfectFieldCache[type] = fields;
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

            if (!PixelPerfectStates.TryGetValue(id, out var previous) || !previous.Equals(state))
            {
                PixelPerfectStates[id] = state;
                component.transform.localScale = new Vector3(num * aspect, num, 1f);
            }
            LogPixelPerfectScale(component, aspect, sourceAspect);
            return false;
        }

        private static bool SHud_WorldToHUDPoint_Prefix(object __instance, Vector3 worldPoint, ref Vector2 __result)
        {
            if (!ShouldApply() || __instance == null)
            {
                return true;
            }

            try
            {
                if (!TryGetHudCarCamera(__instance, out Camera cam) || cam == null)
                {
                    return true;
                }

                var camTransform = cam.transform;
                if (camTransform == null)
                {
                    return true;
                }

                if (Vector3.Dot(camTransform.forward, worldPoint - camTransform.position) < 0f)
                {
                    __result = new Vector2(-100f, -100f);
                    return false;
                }

                Vector3 p = cam.WorldToScreenPoint(worldPoint);
                p.z = 0f;

                float srcW = GetCameraPixelWidth(cam);
                float srcH = GetCameraPixelHeight(cam);
                if (srcW <= 0.01f || srcH <= 0.01f)
                {
                    return true;
                }

                float refW = srcW;
                float refH = srcH;
                if (_pixelDefaultRt != null && _pixelDefaultRt.width > 0 && _pixelDefaultRt.height > 0)
                {
                    refW = _pixelDefaultRt.width;
                    refH = _pixelDefaultRt.height;
                }

                float x = p.x * (refW / srcW);
                float y = p.y * (refH / srcH);
                y = refH - y;
                x -= refW * 0.5f;
                y -= refH * 0.5f;

                if (!TryGetHudMiniRendererSize(__instance, out float hudW, out float hudH))
                {
                    hudW = srcW;
                    hudH = srcH;
                }

                float outX = x + hudW * 0.5f;
                float outY = y + hudH * 0.5f;

                __result = new Vector2(Mathf.Round(outX), Mathf.Round(outY));
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool TryGetHudCarCamera(object hudInstance, out Camera cam)
        {
            cam = null;
            if (hudInstance == null)
            {
                return false;
            }

            var hudType = hudInstance.GetType();
            if (!SHudWorldToHudPointFieldCache.TryGetValue(hudType, out var hudFields))
            {
                hudFields = new SHudWorldToHudPointFields
                {
                    Navigation = AccessTools.Field(hudType, "navigation"),
                    R = AccessTools.Field(hudType, "R")
                };
                SHudWorldToHudPointFieldCache[hudType] = hudFields;
            }

            if (hudFields.Navigation == null)
            {
                return false;
            }

            var navigation = hudFields.Navigation.GetValue(hudInstance);
            if (navigation == null)
            {
                return false;
            }

            var navType = navigation.GetType();
            if (!HudNavigationFieldCache.TryGetValue(navType, out var navFields))
            {
                navFields = new HudNavigationFields
                {
                    Car = AccessTools.Field(navType, "car")
                };
                HudNavigationFieldCache[navType] = navFields;
            }

            if (navFields.Car == null)
            {
                return false;
            }

            var car = navFields.Car.GetValue(navigation);
            if (car == null)
            {
                return false;
            }

            var carType = car.GetType();
            if (!HudCarFieldCache.TryGetValue(carType, out var carFields))
            {
                carFields = new HudCarFields
                {
                    CarCamera = AccessTools.Field(carType, "carCamera")
                };
                HudCarFieldCache[carType] = carFields;
            }

            if (carFields.CarCamera == null)
            {
                return false;
            }

            cam = carFields.CarCamera.GetValue(car) as Camera;
            return cam != null;
        }

        private static bool TryGetHudMiniRendererSize(object hudInstance, out float width, out float height)
        {
            width = 0f;
            height = 0f;
            if (hudInstance == null)
            {
                return false;
            }

            var hudType = hudInstance.GetType();
            if (!SHudWorldToHudPointFieldCache.TryGetValue(hudType, out var hudFields))
            {
                hudFields = new SHudWorldToHudPointFields
                {
                    Navigation = AccessTools.Field(hudType, "navigation"),
                    R = AccessTools.Field(hudType, "R")
                };
                SHudWorldToHudPointFieldCache[hudType] = hudFields;
            }

            if (hudFields.R == null)
            {
                return false;
            }

            var miniRenderer = hudFields.R.GetValue(hudInstance);
            if (miniRenderer == null)
            {
                return false;
            }

            var mrType = miniRenderer.GetType();
            if (!MiniRendererSizeFieldCache.TryGetValue(mrType, out var fields))
            {
                fields = new MiniRendererSizeFields
                {
                    Width = AccessTools.Field(mrType, "width"),
                    Height = AccessTools.Field(mrType, "height")
                };
                MiniRendererSizeFieldCache[mrType] = fields;
            }

            if (fields.Width == null || fields.Height == null)
            {
                return false;
            }

            width = Convert.ToSingle(fields.Width.GetValue(miniRenderer));
            height = Convert.ToSingle(fields.Height.GetValue(miniRenderer));
            return width > 0.01f && height > 0.01f;
        }

        private static float GetCameraPixelWidth(Camera cam)
        {
            if (cam == null)
            {
                return 0f;
            }

            var rt = cam.targetTexture;
            if (rt != null && rt.width > 0)
            {
                return rt.width;
            }

            if (cam.pixelWidth > 0)
            {
                return cam.pixelWidth;
            }

            return Screen.width;
        }

        private static float GetCameraPixelHeight(Camera cam)
        {
            if (cam == null)
            {
                return 0f;
            }

            var rt = cam.targetTexture;
            if (rt != null && rt.height > 0)
            {
                return rt.height;
            }

            if (cam.pixelHeight > 0)
            {
                return cam.pixelHeight;
            }

            return Screen.height;
        }

        private static void MiniRenderer_Start_Postfix(object __instance)
        {
            if (!ShouldApplyHudFix())
            {
                return;
            }

            EnsureMiniRendererRenderTexture(__instance);
            ApplyHudDisplayScale(__instance);
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
                material.SetTexture(MainTexId, rt);
                material.SetTexture(AlphaTexId, rt);
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

            unityRenderer.material.SetTexture(MainTexId, rt);
            unityRenderer.material.SetTexture(AlphaTexId, rt);
        }

        private static void UpdateHudRendererAspect(object hudInstance)
        {
            if (!ShouldApplyHudFix())
            {
                return;
            }

            if (hudInstance == null)
            {
                return;
            }

            float windowAspect = GetWindowAspect();
            if (windowAspect <= 0.01f)
            {
                return;
            }

            var component = hudInstance as Component;
            if (component == null)
            {
                return;
            }

            MonoBehaviour miniRenderer = FindMiniRenderer(component);

            if (miniRenderer == null)
            {
                return;
            }

            var type = miniRenderer.GetType();
            if (!MiniRendererFieldCache.TryGetValue(type, out var fields))
            {
                fields = new MiniRendererFields
                {
                    Width = AccessTools.Field(type, "width"),
                    Height = AccessTools.Field(type, "height")
                };
                MiniRendererFieldCache[type] = fields;
            }

            if (fields.Width == null || fields.Height == null)
            {
                return;
            }

            int width = (int)fields.Width.GetValue(miniRenderer);
            int height = (int)fields.Height.GetValue(miniRenderer);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            int targetWidth = Mathf.Clamp(Mathf.RoundToInt(height * windowAspect), 1, 8192);
            if (width == targetWidth)
            {
                return;
            }

            fields.Width.SetValue(miniRenderer, targetWidth);
            EnsureMiniRendererRenderTexture(miniRenderer);
            LogDebug($"Adjusted HUD MiniRenderer width to {targetWidth} for aspect {windowAspect:0.###}.");
        }

        private static void ApplyHudDisplayScale(object renderer)
        {
            if (!ShouldApplyHudFix())
            {
                return;
            }

            if (renderer == null)
            {
                return;
            }

            var component = renderer as Component;
            if (component == null)
            {
                return;
            }

            var type = renderer.GetType();
            if (!MiniRendererFieldCache.TryGetValue(type, out var fields))
            {
                fields = new MiniRendererFields
                {
                    Width = AccessTools.Field(type, "width"),
                    Height = AccessTools.Field(type, "height")
                };
                MiniRendererFieldCache[type] = fields;
            }

            if (fields.Width == null || fields.Height == null)
            {
                return;
            }

            int width = (int)fields.Width.GetValue(renderer);
            int height = (int)fields.Height.GetValue(renderer);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var meshRenderers = component.GetComponentsInParent<MeshRenderer>(true);
            if (meshRenderers == null || meshRenderers.Length == 0)
            {
                return;
            }

            var rt = GetRendererTexture(renderer);
            foreach (var meshRenderer in meshRenderers)
            {
                if (meshRenderer == null)
                {
                    continue;
                }

                var material = meshRenderer.sharedMaterial;
                if (material == null)
                {
                    continue;
                }

                if (rt != null && material.mainTexture != rt && material.GetTexture(MainTexId) != rt)
                {
                    continue;
                }

                var transform = meshRenderer.transform;
                int id = transform.GetInstanceID();
                if (!HudDisplayOriginalScales.TryGetValue(id, out var baseScale))
                {
                    baseScale = transform.localScale;
                    HudDisplayOriginalScales[id] = baseScale;
                }

                float windowAspect = GetWindowAspect();
                if (windowAspect <= 0.01f)
                {
                    return;
                }

                float rendererAspect = width / (float)height;
                float scaleX = windowAspect / rendererAspect;
                var newScale = new Vector3(baseScale.x * scaleX, baseScale.y, baseScale.z);
                transform.localScale = newScale;
                LogDebug($"Scaled HUD display to {scaleX:0.###}x (aspect {windowAspect:0.###}).");
            }
        }

        private static Texture GetRendererTexture(object renderer)
        {
            var type = renderer.GetType();
            var rtField = AccessTools.Field(type, "rt");
            if (rtField == null)
            {
                return null;
            }

            return rtField.GetValue(renderer) as Texture;
        }

        private static MonoBehaviour FindMiniRenderer(Component component)
        {
            if (component == null)
            {
                return null;
            }

            MonoBehaviour miniRenderer = null;
            var parent = component.transform;
            while (parent != null)
            {
                foreach (var candidate in parent.GetComponents<MonoBehaviour>())
                {
                    if (candidate != null && string.Equals(candidate.GetType().Name, "MiniRenderer", StringComparison.Ordinal))
                    {
                        miniRenderer = candidate;
                        break;
                    }
                }

                if (miniRenderer != null)
                {
                    break;
                }

                parent = parent.parent;
            }

            return miniRenderer;
        }
    }
}
