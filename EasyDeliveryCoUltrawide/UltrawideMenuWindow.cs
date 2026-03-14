using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public class UltrawideMenuWindow : MonoBehaviour
    {
        public const string FileName = "wide";
        public const string ListenerName = "UltrawideMenu";
        public const string ListenerData = "listener_UltrawideMenu";

        private const string PrefKeyFov = "UltrawideFovOverride";

        private float _mouseYLock;
        private UIUtil _util;

        public void FrameUpdate(DesktopDotExe.WindowView view)
        {
            if (view == null)
            {
                return;
            }

            if (_util == null)
            {
                _util = new UIUtil();
            }

            _util.M = view.M;
            _util.R = view.R;
            _util.nav = view.M.nav;

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

            DrawMenu(p, view);
        }

        public void BackButtonPressed()
        {
        }

        private void DrawMenu(Rect p, DesktopDotExe.WindowView view)
        {
            float center = p.x + p.width / 2f - 16f;
            float y = p.y + 10f;
            float line = 12f;

            _util.Label("Ultrawide Mod", p.x + p.width / 2f, y);
            y += line + 4f;

            _util.Label("Camera", p.x + p.width / 2f, y);
            y += line;

            float fovMin;
            float fovMax;
            GetFovRange(out fovMin, out fovMax);
            float fov = Mathf.Clamp(GetCurrentFov(), fovMin, fovMax);
            float fovValue = Mathf.InverseLerp(fovMin, fovMax, fov);
            _util.ValueLabel($"{fov:0}", p.x + p.width - 12f, y);
            float? newFovValue = _util.Slider("FOV", fovValue, center, y, ref _mouseYLock);
            if (newFovValue.HasValue)
            {
                float newFov = Mathf.Lerp(fovMin, fovMax, newFovValue.Value);
                ApplyFov(newFov);
            }

        }

        private static float GetCurrentFov()
        {
            float saved = PlayerPrefs.GetFloat(PrefKeyFov, -1f);
            if (saved >= 1f)
            {
                return saved;
            }

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

        private static void ApplyFov(float fov)
        {
            PlayerPrefs.SetFloat(PrefKeyFov, fov);
            Plugin.ApplyFovOverride(fov);
        }

        private static void GetFovRange(out float min, out float max)
        {
            min = 50f;
            max = 110f;
        }
    }
}
