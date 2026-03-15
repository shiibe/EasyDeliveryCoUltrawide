using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public class UltrawideMenuWindow : MonoBehaviour
    {
        public const string FileName = "wide";
        public const string ListenerName = "UltrawideMenu";
        public const string ListenerData = "listener_UltrawideMenu";

        private float _mouseYLock;
        private UIUtil _util;

        public void FrameUpdate(DesktopDotExe.WindowView view)
        {
            if (view == null)
            {
                return;
            }

            _util ??= new UIUtil();
            
            _util.M = view.M;
            _util.R = view.R;
            _util.Nav = view.M.nav;

            Rect p = new Rect(view.position * 8f, view.size * 8f);
            p.position += new Vector2(8f, 8f);

            if (_util.M.mouseButtonUp)
            {
                _mouseYLock = 0f;
            }

            if (_mouseYLock > 0f)
            {
                _util.M.mouse.y = _mouseYLock;
            }

            DrawMenu(p);
        }

        public void BackButtonPressed()
        {
        }

        private void DrawMenu(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float y = p.y + 10f;
            float line = 12f;

            _util.Label("Ultrawide Mod", p.x + p.width / 2f, y);
            y += line + 4f;

            _util.Label("FOV", p.x + p.width / 2f, y);
            y += line;

            float fovMin;
            float fovMax;
            GetFovRange(out fovMin, out fovMax);

            float currentFov = GetCurrentCameraFov();

            float thirdFov = Mathf.Clamp(Plugin.GetSavedFovOrDefault(firstPerson: false, fallback: currentFov), fovMin, fovMax);
            float thirdValue = Mathf.InverseLerp(fovMin, fovMax, thirdFov);
            _util.ValueLabel($"{thirdFov:0}", p.x + p.width - 12f, y);
            float? newThirdValue = _util.Slider("3rd Per.", thirdValue, center, y, ref _mouseYLock);
            if (newThirdValue.HasValue)
            {
                float newFov = Mathf.Lerp(fovMin, fovMax, newThirdValue.Value);
                Plugin.SaveFovOverride(firstPerson: false, fov: newFov);
            }

            y += line;

            float firstFov = Mathf.Clamp(Plugin.GetSavedFovOrDefault(firstPerson: true, fallback: currentFov), fovMin, fovMax);
            float firstValue = Mathf.InverseLerp(fovMin, fovMax, firstFov);
            _util.ValueLabel($"{firstFov:0}", p.x + p.width - 12f, y);
            float? newFirstValue = _util.Slider("1st Per.", firstValue, center, y, ref _mouseYLock);
            if (newFirstValue.HasValue)
            {
                float newFov = Mathf.Lerp(fovMin, fovMax, newFirstValue.Value);
                Plugin.SaveFovOverride(firstPerson: true, fov: newFov);
            }

            y += line + 4f;

            _util.Label("Renderer", p.x + p.width / 2f, y);
            y += line;

            int pixelMode = Plugin.GetPixelationMode();
            string pixelLabel = pixelMode switch
            {
                0 => "None",
                1 => "Finer",
                2 => "Fine",
                3 => "Default",
                4 => "Large",
                _ => "Default"
            };
            _util.ValueLabel(pixelLabel, p.x + p.width - 12f, y);

            float pixelValue = Mathf.Clamp01(pixelMode / 4f);
            float? newPixelValue = _util.Slider("Pixelation", pixelValue, center, y, ref _mouseYLock);
            if (newPixelValue.HasValue)
            {
                int newMode = Mathf.Clamp(Mathf.RoundToInt(newPixelValue.Value * 4f), 0, 4);
                if (newMode != pixelMode)
                {
                    Plugin.SavePixelationMode(newMode);
                }
            }

            y += line + 4f;

            int viewMode = Plugin.GetViewDistanceMode();
            string viewLabel = viewMode switch
            {
                0 => "Near",
                1 => "Default",
                2 => "Far",
                3 => "Max",
                _ => "Default"
            };
            _util.ValueLabel(viewLabel, p.x + p.width - 12f, y);

            float viewValue = Mathf.Clamp01(viewMode / 3f);
            float? newViewValue = _util.Slider("View Distance", viewValue, center, y, ref _mouseYLock);
            if (newViewValue.HasValue)
            {
                int newMode = Mathf.Clamp(Mathf.RoundToInt(newViewValue.Value * 3f), 0, 3);
                if (newMode != viewMode)
                {
                    Plugin.SaveViewDistanceMode(newMode);
                }
            }

        }

        private static float GetCurrentCameraFov()
        {
            var pauseSystem = PauseSystem.pauseSystem;
            if (pauseSystem != null && pauseSystem.mainCamera != null)
            {
                return pauseSystem.mainCamera.fieldOfView;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                return cam.fieldOfView;
            }

            return 70f;
        }

        private static void GetFovRange(out float min, out float max)
        {
            min = 50f;
            max = 110f;
        }
    }
}
