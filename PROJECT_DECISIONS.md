# Project Decisions

## Stack

- C# and `.NET 10`
- WPF for the Windows desktop app
- xUnit for tests
- JSON settings, not SQLite
- Local-first architecture with no telemetry or cloud dependency

## Architecture

The solution is split into three production projects:

- `LightPilot.Core`: deterministic adaptive rules, models, profiles, app mapping, and luminance classification.
- `LightPilot.Infrastructure`: Windows adapters for monitor enumeration, DDC/CI, WMI brightness, foreground window context, fullscreen detection, content sampling, startup registration, and settings persistence.
- `LightPilot.App`: WPF UI, tray behavior, settings, and background coordination.

Core has no WPF or Win32 dependency. Infrastructure wraps native APIs behind interfaces so tests can use fakes.

## Safety Defaults

- Auto mode is enabled by default.
- Content brightness analysis is disabled by default.
- Brightness automation clamps to `25%-90%`.
- The engine never targets below an effective `15%` hard floor.
- DDC/CI writes are throttled to avoid rapid hardware calls.
- Fullscreen gaming/video/presentation contexts avoid disruptive brightness changes.
- Auto mode uses gentle steps: at most 3 brightness points and 200K warmth per decision, with cooldown and hysteresis.

## Monitor Control

DDC/CI is the preferred hardware path for external monitors, but it is unreliable across docks, KVMs, cables, firmware, and GPU drivers. Light Pilot treats failed DDC/CI as a per-monitor degraded state rather than an app failure.

WMI brightness is used for laptop/internal panels when available. Software overlay is the fallback because it is predictable and reversible, although it does not reduce panel backlight power.

## UI

The UI is intentionally small and avoids technical labels in the normal path. The main window answers only:

- Is Auto active?
- What mode is current?
- What brightness/warmth is being applied?
- Why?
- Can I pause or open settings?

Advanced details stay in Settings or local diagnostics. Main UI uses human labels such as `Comfortable`, `Soft`, and `Adjusting gently` instead of raw control-layer names.
