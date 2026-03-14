using System;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        internal static void RefreshHudBackdrop()
        {
            var hudType = HarmonyLib.AccessTools.TypeByName("sHUD");
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
            var type = HarmonyLib.AccessTools.TypeByName(typeName);
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

            var field = HarmonyLib.AccessTools.Field(instance.GetType(), fieldName);
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
            if (OverlayTargetSizes.TryGetValue(id, out var previous) && Approximately(previous, targetSize))
            {
                return;
            }

            OverlayTargetSizes[id] = targetSize;
            LogDebug($"Resized {label} overlay to {targetSize.x:0.###}x{targetSize.y:0.###} using {cam.name}.");
        }
    }
}
