## 1.2.0
- Add `default` aspect mode to disable ultrawide fixes (vanilla presentation) while keeping pixelation usable.
- Improve `aspect_ratio=auto` to use the current window aspect ratio (rounded) to avoid CRT edge smearing.
- Fix world-space HUD labels (interaction + inventory label) drifting when pixelation changes the render texture size.
- Tune `Pixelation` Large strength (less aggressive).
- Increase `View Distance` Max (higher far clip / LOD / shadow distance).
- Add `Fog` multiplier slider to the in-game menu.
- Clean up/standardize in-game menu spacing and labels.

## 1.1.2
- Clean up in-game settings menu labels (rename `Pixelation` header to `Renderer`; put `View Distance` label on the slider).

## 1.1.1
- Add `Pixelation` slider to `wide.exe` menu (None/Finer/Fine/Default/Large).
- Add `View Distance` slider to `wide.exe` menu (Near/Default/Far/Max).
- Apply view distance changes to gameplay camera far clip + quality LOD/shadow distance.
- Add config options to control `wide.exe` icon visibility/position on the main menu desktop.
- Split performance logging into a separate toggle (`perf_logging`).
- Re-categorize config options for easier navigation.
- Misc internal cleanup.

## 1.1.0
- Add in-game mod menu (`wide.exe`) for configuring in-game options.
- Add separate FOV sliders for `1st Per.` and `3rd Per.` (max 110).
- Skip FOV overrides while inside buildings.
- Fix camera stretch in `CamDotExe`.
- Internal refactor/split `Plugin.cs` into partials.

## 1.0.6
- Refresh README screenshots.

## 1.0.5
- Rename `enable_ultrawide_mode` to `enable_mod`.
- Add `enable_hud_fix` toggle to control HUD scaling behavior.

## 1.0.4
- Remove the FOV override patching and related config entries.
- Simplify configuration docs to match current options.

## 1.0.3
- Force menu cameras to use the target aspect ratio on intro/choose screens.
- Apply aspect fixes to primary gameplay cameras on refresh.
- Rescale overlays after FOV overrides to keep transitions aligned.
- Refresh README icon and screenshots.

## 1.0.2
- Default `aspect_ratio` to `auto` and clarify config docs.
- Add README screenshots and refresh packaging notes.

## 1.0.1
- Update manifest/README wording.
- Packaging metadata refresh.

## 1.0.0
- Initial public release.
- Ultrawide camera and UI overlay fixes for Easy Delivery Co.
