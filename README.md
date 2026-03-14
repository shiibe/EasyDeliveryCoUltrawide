# Easy Delivery Co. - Ultrawide Support Mod

Ultrawide camera and UI overlay fixes for Easy Delivery Co.

**Features**
- Unlocks camera viewports for ultrawide displays
- Optional FOV overrides for gameplay and named cameras
- Scales menu, pause, and transition overlays to full width
- Keeps main UI elements at their intended ratio

**Installation**
- r2modman/Thunderstore: install and launch the game
- Manual: copy `EasyDeliveryCoUltrawide.dll` to `BepInEx/plugins/EasyDeliveryCoUltrawide/`

**Configuration**
- Config file: `BepInEx/config/shibe.easydeliveryco.ultrawide.cfg`
- `aspect_ratio`: `display`, `window`, `21:9`, `32:9`, `2.39`
- `fov_global`: set to 0 to use in-game setting
- `fov_rearview`: driving/walking camera override
- `fov_camera`: first-person camera override
- `fov_camera_persp`: fixed perspective camera override

**To Do**
- EasyDeliveryAPI menu integration

**Building**
- Build: `dotnet build EasyDeliveryCoUltrawide/EasyDeliveryCoUltrawide.csproj -c Release`
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.0`
