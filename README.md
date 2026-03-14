# Easy Delivery Co. - Ultrawide Support Mod

Ultrawide camera and UI overlay fixes for Easy Delivery Co.

## Features
- Unlocks camera viewports for ultrawide displays
- Optional FOV overrides for gameplay and named cameras
- Scales menu, pause, and transition overlays to full width
- Keeps main UI elements at their intended ratio

## Screenshots
![Image](https://github.com/user-attachments/assets/3c0d2f53-17d0-421b-8095-6cbfc6afbb5c)
![Image](https://github.com/user-attachments/assets/d0fed05d-823d-4c9b-b7d5-180d59cfead9)
![Image](https://github.com/user-attachments/assets/79a3ee31-b7cb-4058-b6e2-edac25743b98)
![Image](https://github.com/user-attachments/assets/00496282-9bed-4bf5-ac76-21d452cf0119)
![Image](https://github.com/user-attachments/assets/0835a069-76cf-4d48-adf4-0c7e02ed4ad1)


## Installation
- r2modman/Thunderstore: install and launch the game
- Manual: copy `EasyDeliveryCoUltrawide.dll` to `BepInEx/plugins/EasyDeliveryCoUltrawide/`

#### Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.ultrawide.cfg`
- `aspect_ratio`: `auto` (display), `window`, `21:9`, `32:9`, `2.39`
- `fov_global`: set to 0 to use in-game setting
- `fov_rearview`: driving/walking camera override
- `fov_camera`: first-person camera override
- `fov_camera_persp`: fixed perspective camera override

## Planned Features
- Add menu in-game for FOV settings

## Build
- Build: `dotnet build EasyDeliveryCoUltrawide/EasyDeliveryCoUltrawide.csproj -c Release`
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.0`
