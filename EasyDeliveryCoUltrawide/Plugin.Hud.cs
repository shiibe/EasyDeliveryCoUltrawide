using System;
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
