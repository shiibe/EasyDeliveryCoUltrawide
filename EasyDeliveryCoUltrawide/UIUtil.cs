using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public class UIUtil
    {
        public DesktopDotExe M;
        public GamepadNavigation Nav;
        public MiniRenderer R;

        public void Label(string name, float x, float y)
        {
            name = LocalizationDictionary.Translate(name);
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.center;
            R.fput(name, x, y);
        }

        public void ValueLabel(string value, float x, float y)
        {
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(value, x, y);
        }

        public bool Button(string name, float x, float y)
        {
            name = LocalizationDictionary.Translate(name);
            if (M.MouseOver((int)x - 2, (int)y, name.Length * 8 + 4, 8))
            {
                M.mouseIcon = 128;
                name = ">" + name;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                }
                if (M.mouseButtonUp)
                {
                    return true;
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(name, x - 4f, y);
            return false;
        }

        public bool? Toggle(string name, bool state, float x, float y)
        {
            name = LocalizationDictionary.Translate(name);
            bool? result = null;
            string text = "[" + (state ? "on" : "off") + "]";
            R.put(text, x + 4f, y);
            if (M.MouseOver((int)x - 2, (int)y, text.Length * 8 + 4, 8))
            {
                M.mouseIcon = 128;
                name = ">" + name;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                }
                if (M.mouseButtonUp)
                {
                    result = !state;
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(name, x - 4f, y);
            return result;
        }

        public float? Slider(string name, float value, float x, float y, ref float mouseYLock)
        {
            name = LocalizationDictionary.Translate(name);
            float? result = null;
            int num = 10;
            for (int i = 0; i < num; i++)
            {
                R.spr(32f, 0f, x + 4f + i * 8f, y, 8f, 8f);
            }
            float x2 = x + value * num * 8f;
            R.spr(0f, 24f, x2, y, 8f, 8f);
            if (M.MouseOver((int)x - 8, (int)y, num * 8 + 16, 8))
            {
                M.mouseIcon = 128;
                name = ">" + name;
                if (Nav is not null && Nav.menuInput.x < 0f)
                {
                    result = Mathf.Clamp01(value - Time.unscaledDeltaTime / 2f);
                }
                if (Nav is not null && Nav.menuInput.x > 0f)
                {
                    result = Mathf.Clamp01(value + Time.unscaledDeltaTime / 2f);
                }
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                    value = Mathf.InverseLerp(x + 4f, x + 4f + num * 8f, M.mouse.x);
                    value = Mathf.Clamp01(value);
                    result = value;
                    if (mouseYLock == 0f)
                    {
                        mouseYLock = M.mouse.y;
                    }
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(name, x - 4f, y);
            return result;
        }
    }
}
