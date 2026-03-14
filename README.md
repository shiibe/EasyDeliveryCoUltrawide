<img width="128" height="128" alt="Image" src="https://github.com/user-attachments/assets/5c852127-6fa1-48b4-b94d-84fd1b0ddee4" />

# Easy Delivery Co. - Ultrawide Support Mod

Ultrawide support via camera and UI overlay fixes for Easy Delivery Co. This mod unlocks viewport scaling for wide displays, keeps HUD elements readable, and ensures menus and transitions stretch cleanly across ultrawide aspect ratios.

## Features
- Unlocks camera viewports for ultrawide displays
- Optional aspect ratio overrides for gameplay and menu cameras
- Scales menu, pause, and transition overlays to full width
- Keeps main UI elements at their intended ratio

## Screenshots
![Image](https://github.com/user-attachments/assets/74a46046-f038-424a-acd6-f011500c42f3)
![Image](https://github.com/user-attachments/assets/00b2b607-1fa5-49ac-862b-189d106130f2)
![Image](https://github.com/user-attachments/assets/2488dec6-3343-4b19-a3fd-cd77d2ed697f)
![Image](https://github.com/user-attachments/assets/cfc22226-5060-4a26-bdb7-5f4499647363)
![Image](https://github.com/user-attachments/assets/6ed2cd4b-fc39-4cd4-a2ec-96696b296299)
![Image](https://github.com/user-attachments/assets/5cb6e2db-a187-4a89-9578-7c8f676062f9)
![Image](https://github.com/user-attachments/assets/0de278c2-399b-4e10-a043-de3ac7eca0df)

## Installation
- r2modman/Thunderstore: install and launch the game
- Manual: copy `EasyDeliveryCoUltrawide.dll` to `BepInEx/plugins/EasyDeliveryCoUltrawide/`

#### Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.ultrawide.cfg`
- `enable_mod`: enable/disable the mod
- `enable_hud_fix`: enable/disable HUD scaling and positioning fixes
- `aspect_ratio`: `auto` (display), `window`, `21:9`, `32:9`, `2.39`

## Planned Features
- Add menu in-game for FOV settings

## Build
- Build: `dotnet build EasyDeliveryCoUltrawide/EasyDeliveryCoUltrawide.csproj -c Release`
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.5`
