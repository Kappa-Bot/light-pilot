# Release Checklist

## v0.1.0

1. Run `dotnet build LightPilot.sln`.
2. Run `dotnet test LightPilot.sln`.
3. Run `.\scripts\install-local.ps1`.
4. Run `.\scripts\smoke.ps1`.
5. Run `.\scripts\package-release.ps1 -Version 0.1.0`.
6. Create tag `v0.1.0`.
7. Upload `artifacts\LightPilot-0.1.0-win-x64.zip` to the GitHub release.

Hardware-changing native tests are opt-in only.
