# Manual Validation

Run these checks after `dotnet build` and `dotnet test` pass.

## Launch

- Start `LightPilot.App`.
- Confirm the compact main window opens.
- Confirm the main window uses simple labels such as `Comfortable`, `Soft`, and `Adjusting gently`.
- Confirm closing the window hides it instead of terminating the app.
- Confirm the tray icon remains available.

## Tray

- Left click opens the main window.
- Right click shows Auto on/off, Pause 30 min, Pause until tomorrow, Current mode, Settings, Exit.
- Pause changes the main window state.
- Resume returns to gradual automatic adjustment.
- Exit shuts down tray icon and app process.

## Settings

- Change comfort intensity and restart the app.
- Confirm settings persist under `%LOCALAPPDATA%\LightPilot\settings.json`.
- Confirm content brightness analysis is off by default.
- Confirm reset restores safe defaults.

## Detection

- Open a browser, VS Code, VLC, and a fullscreen app.
- Confirm current mode/reason updates without crashes.
- Confirm fullscreen video/game contexts avoid brightness jumps.
- Confirm warmth changes gradually rather than jumping to a very warm tone.

## Monitor Safety

- Run on a monitor without DDC/CI support.
- Confirm the app does not crash.
- Confirm the app remains understandable without showing technical control details in the main window.

## Packaging

```powershell
dotnet publish src/LightPilot.App/LightPilot.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
