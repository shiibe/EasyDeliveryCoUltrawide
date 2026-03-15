<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoUltrawide/refs/heads/main/assets/icon.png" alt="Logo" width="128">
  <br>EasyDeliveryCoUltrawide
</h1>
  <p align="center">
    Ultrawide monitor support for Easy Delivery Co.
    <br />
    <a href="#about">About</a>
    ·
    <a href="#screenshots">Screenshots</a>
    ·
    <a href="#features">Features</a>
    ·
    <a href="#installation">Installation</a>
    ·
    <a href="#configuration">Configuration</a>
    ·
    <a href="#fov-menu">FOV Menu</a>
    ·
    <a href="#build">Build</a>
  </p>
</p>
<hr/>

## About
This is a BepInEx + Harmony mod that patches Easy Delivery Co's camera and UI systems to behave better on ultrawide.

- Forces primary gameplay cameras to render full-width on ultrawide (and can override camera aspect to a target ratio)
- Rescales menu/pause/transition overlays so fades/backdrops cover the full screen
- Patches HUD rendering paths (`pixelPerfectView`/`MiniRenderer`) so UI stays readable instead of stretched
- Adds a `wide.exe` entry in the main menu that opens an in-game settings window
- Saves separate 1st/3rd person FOV overrides to PlayerPrefs and applies the correct one at runtime

## Screenshots
![Screenshot 1](https://raw.githubusercontent.com/shiibe/EasyDeliveryCoUltrawide/refs/heads/main/assets/screenshots/1.jpg)
![Screenshot 2](https://raw.githubusercontent.com/shiibe/EasyDeliveryCoUltrawide/refs/heads/main/assets/screenshots/2.jpg)
![Screenshot 3](https://raw.githubusercontent.com/shiibe/EasyDeliveryCoUltrawide/refs/heads/main/assets/screenshots/3.jpg)
![Screenshot 4](https://raw.githubusercontent.com/shiibe/EasyDeliveryCoUltrawide/refs/heads/main/assets/screenshots/4.jpg)
![Screenshot 5](https://raw.githubusercontent.com/shiibe/EasyDeliveryCoUltrawide/refs/heads/main/assets/screenshots/5.jpg)
![Screenshot 6](https://raw.githubusercontent.com/shiibe/EasyDeliveryCoUltrawide/refs/heads/main/assets/screenshots/6.jpg)
![Screenshot 7](https://raw.githubusercontent.com/shiibe/EasyDeliveryCoUltrawide/refs/heads/main/assets/screenshots/7.jpg)

## Features
- Full-width gameplay viewport on ultrawide
- Optional aspect ratio override for gameplay + menu cameras
- Overlay fixes (menus, pause, transitions) scaled to full width
- HUD scaling and positioning fixes (toggleable)
- In-game FOV menu via `wide.exe` in the main menu with separate `1st Per.` / `3rd Per.` sliders (max 110)
- Automatically skips FOV overrides while inside buildings

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`

Install
- r2modman/Thunderstore: install and launch the game
- Manual: copy `EasyDeliveryCoUltrawide.dll` to `BepInEx/plugins/EasyDeliveryCoUltrawide/`

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.ultrawide.cfg`
- `enable_mod`: enable/disable the mod
- `enable_hud_fix`: enable/disable HUD scaling and positioning fixes
- `aspect_ratio`: `auto` (display), `window`, `21:9`, `32:9`, `2.39`

## FOV Menu
- Click `wide.exe` in the main menu to access the settings
- Separate saved values for `1st Per.` and `3rd Per.`
- Saved in PlayerPrefs (not the BepInEx config)
- FOV overrides are skipped while inside buildings

## Build
- Build: `dotnet build EasyDeliveryCoUltrawide/EasyDeliveryCoUltrawide.csproj -c Release`
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.1.0`
