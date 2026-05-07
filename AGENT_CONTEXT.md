# Agent Context

Light Pilot is a professional adaptive screen comfort utility for Windows.

## Product Rule

One job: make screens comfortable automatically.

Do not turn the app into a dashboard. Normal UI must avoid charts, raw logs, and technical noise.

## Important Defaults

- Content brightness analysis is opt-in and local only.
- Settings live under `%LOCALAPPDATA%\LightPilot`.
- No screenshots, pixels, app content, or window titles should be persisted.
- Brightness control must tolerate unsupported monitors.
- Fullscreen games/videos/presentations should not receive disruptive changes.

## Project Shape

```text
src/LightPilot.Core
src/LightPilot.Infrastructure
src/LightPilot.App
tests/LightPilot.Core.Tests
tests/LightPilot.Infrastructure.Tests
docs/
```

Core should remain deterministic and OS-free. Infrastructure owns Windows APIs. App owns WPF and tray behavior.

## Validation

Run:

```powershell
dotnet build LightPilot.sln
dotnet test LightPilot.sln
```

Do not run native tests that change real monitor brightness unless the user explicitly opts in.
