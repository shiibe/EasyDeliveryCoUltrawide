<img width="128" height="128" alt="Image" src="https://github.com/user-attachments/assets/5c852127-6fa1-48b4-b94d-84fd1b0ddee4" />

# Easy Delivery Co. - Ultrawide Support Mod

Ultrawide camera and UI overlay fixes for Easy Delivery Co.

## Features
- Unlocks camera viewports for ultrawide displays
- Optional FOV overrides for gameplay and named cameras
- Scales menu, pause, and transition overlays to full width
- Keeps main UI elements at their intended ratio

## Screenshots
![Image](https://github.com/user-attachments/assets/74a46046-f038-424a-acd6-f011500c42f3)
![Image](https://github.com/user-attachments/assets/f70c3cc4-60c9-4049-aed4-45fd60015ebf)
![Image](https://github.com/user-attachments/assets/e79b30dc-b45d-4e84-af9f-bac758e97cce)
![Image](https://github.com/user-attachments/assets/936045aa-ec3a-4b76-bf15-f09a25003034)
![Image](https://github.com/user-attachments/assets/e87b4c2a-e66e-431f-b301-9bd7b770daea)
![Image](https://github.com/user-attachments/assets/52984af7-e949-4c3d-a01b-4caa055d8484)


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
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.3`
