using System;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        private static void DesktopDotExe_Setup_Postfix(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            var desktop = __instance as DesktopDotExe;
            if (desktop == null)
            {
                return;
            }

            DesktopDotExe.File existingFile = null;
            foreach (var file in desktop.files)
            {
                if (file != null && string.Equals(file.name, UltrawideMenuWindow.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    existingFile = file;
                    break;
                }
            }

            if (existingFile == null)
            {
                var file = new DesktopDotExe.File(desktop.R, desktop)
                {
                    name = UltrawideMenuWindow.FileName,
                    type = DesktopDotExe.FileType.exe,
                    data = UltrawideMenuWindow.ListenerData,
                    icon = 7,
                    iconHover = 7,
                    position = new Vector2(5.5f, 3.25f),
                    visible = true,
                    cantFolder = false
                };
                desktop.files.Add(file);
            }
            else
            {
                existingFile.icon = 7;
                existingFile.iconHover = 7;
                existingFile.position = new Vector2(5.5f, 3.25f);
            }

            var root = desktop.transform;
            if (root.Find(UltrawideMenuWindow.ListenerName) == null)
            {
                var listener = new GameObject(UltrawideMenuWindow.ListenerName);
                listener.transform.SetParent(root, false);
                listener.AddComponent<UltrawideMenuWindow>();
            }
        }
    }
}
