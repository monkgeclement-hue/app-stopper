# MindGate for Windows

MindGate for Windows is built with **C#/.NET**. `MindGate.Core` implements the shared rule model, runtime validation, and platform-independent usage-budget state machine. The Windows client has Dashboard, My Apps, Analytics, and Settings sections, MindGate’s app categories, daily limits, on-device usage tracking, warning notifications, and a block notice when a selected app reaches its limit. Analytics currently shows the active cycle; weekly history is not stored yet. Closing the window hides MindGate in the system tray so monitoring continues; double-click the tray icon to reopen it, or choose **Exit MindGate** to quit.

The Windows interface is currently **C# WinForms**, not HTML/CSS/JavaScript. The browser prototype is a visual reference; its dashboard layout is being translated into the Windows client while the rule engine and enforcement stay in C#.

From the repository root, run the Windows prototype with:

```powershell
dotnet run --project apps/windows/MindGate.WindowsClient/MindGate.WindowsClient.csproj
```

Rules and counters are stored in `%LOCALAPPDATA%\MindGate\windows-state.json`. Existing data in `%LOCALAPPDATA%\AppStopper\windows-state.json` is migrated on first launch after the rename. While an app is blocked, its rule cannot be removed and monitoring cannot be paused; those controls unlock at the scheduled reset. This is still an early desktop proof: the picker lists running desktop processes with readable executable paths, and the block notice is a user-mode overlay that can be bypassed by someone who administers the PC, for example by closing MindGate or changing its local state. Packaged Windows apps, idle-time policy, startup recovery, and robust enforcement after the app is closed are not covered yet.

Run the policy smoke checks with:

```powershell
dotnet run --project apps/windows/tests/MindGate.Core.SmokeTests/MindGate.Core.SmokeTests.csproj
```
