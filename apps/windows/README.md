# Windows client

The Windows client is being built with **C#/.NET**. `AppStopper.Core` implements the shared rule model, runtime validation, and a platform-independent usage-budget state machine. The WinForms prototype adds a MindGate-inspired dashboard with a first-limit empty state and summaries for tracked apps, use this cycle, and remaining allowance summed across app limits. It also has a running desktop-app picker, daily limits, local state, foreground-process sampling, warning notifications, and a full-screen block notice when a selected app reaches its limit. Closing the dashboard hides it to the system tray so monitoring continues; double-click the tray icon to reopen settings, or choose **Exit App Stopper** from its context menu to quit.

From the repository root, run the Windows prototype with:

```powershell
dotnet run --project apps/windows/AppStopper.WindowsClient/AppStopper.WindowsClient.csproj
```

Rules and counters are stored in `%LOCALAPPDATA%\AppStopper\windows-state.json`. While an app is blocked, its rule cannot be removed and monitoring cannot be paused from the dashboard; those controls unlock at the scheduled reset. This is still an early desktop proof: the picker lists running desktop processes with readable executable paths, and the block notice is a user-mode overlay that can be bypassed by someone who administers the PC, for example by closing the client or changing its local state. It does not yet cover packaged Windows apps, idle-time policy, startup recovery, or robust enforcement after the client is closed.

Run the policy smoke checks with:

```powershell
dotnet run --project apps/windows/tests/AppStopper.Core.SmokeTests/AppStopper.Core.SmokeTests.csproj
```
