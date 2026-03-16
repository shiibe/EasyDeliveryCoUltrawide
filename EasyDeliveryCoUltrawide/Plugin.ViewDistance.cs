using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        private static bool _viewDistanceBaseCaptured;
        private static float _baseShadowDistance;
        private static float _baseLodBias;
        private static int _baseMaximumLodLevel;

        internal static void RefreshViewDistance()
        {
            if (!ShouldApply())
            {
                return;
            }

            CaptureViewDistanceBaseValues();

            int mode = GetViewDistanceMode();
            ApplyViewDistanceQuality(mode);

            // Camera far clip is applied in SCameraController_Update_Postfix each frame.
        }

        private static void CaptureViewDistanceBaseValues()
        {
            if (_viewDistanceBaseCaptured)
            {
                return;
            }

            _baseShadowDistance = QualitySettings.shadowDistance;
            _baseLodBias = QualitySettings.lodBias;
            _baseMaximumLodLevel = QualitySettings.maximumLODLevel;
            _viewDistanceBaseCaptured = true;
        }

        private static void ApplyViewDistanceQuality(int mode)
        {
            if (!_viewDistanceBaseCaptured)
            {
                return;
            }

            if (mode == 1)
            {
                QualitySettings.shadowDistance = _baseShadowDistance;
                QualitySettings.lodBias = _baseLodBias;
                QualitySettings.maximumLODLevel = _baseMaximumLodLevel;
                return;
            }

            float shadowDistance;
            float lodBias;
            switch (mode)
            {
                case 0:
                    shadowDistance = 150f;
                    lodBias = 0.75f;
                    break;
                case 2:
                    shadowDistance = 1200f;
                    lodBias = 3.0f;
                    break;
                case 3:
                    shadowDistance = 3000f;
                    lodBias = 6.0f;
                    break;
                default:
                    shadowDistance = _baseShadowDistance;
                    lodBias = _baseLodBias;
                    break;
            }

            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.lodBias = lodBias;
            QualitySettings.maximumLODLevel = 0;
        }

        #pragma warning disable IDE1006
        private static void SCameraController_Update_Postfix(object __instance)
        {
            if (!ShouldApply())
            {
                return;
            }

            if (!(__instance is sCameraController controller) || controller.cam == null)
            {
                return;
            }

            int mode = GetViewDistanceMode();
            if (mode == 1)
            {
                return;
            }

            float far;
            switch (mode)
            {
                case 0:
                    far = 3000f;
                    break;
                case 2:
                    far = 25000f;
                    break;
                case 3:
                    far = 100000f;
                    break;
                default:
                    far = 10000f;
                    break;
            }

            // Avoid invalid values.
            if (far > controller.cam.nearClipPlane + 1f)
            {
                controller.cam.farClipPlane = far;
            }
        }
        #pragma warning restore IDE1006
    }
}
