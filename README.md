# MindGate

MindGate is a local-first self-management tool for setting time budgets on apps a person chooses. It warns as a budget runs low, blocks the selected app when the budget is used, and restores access at the configured reset time.

The project targets **Android, iOS/iPadOS, and Windows**. Each operating system needs its own native usage-monitoring and enforcement adapter; a web page alone cannot restrict other apps. The first version keeps rules and usage totals on the device and does not require an account or cloud service.

## First release scope

- User selects supported apps on their own device.
- Each app has an allowance, warning thresholds, and a reset rule.
- Usage counts while the selected app is in the foreground and the device is in use.
- Warnings are issued at configured remaining-time thresholds (initial proposal: 30, 20, and 10 minutes).
- When the allowance is exhausted, the platform adapter blocks access until the rule resets.
- Essential system functions are excluded, and the app explains every permission it requests.
- Rules, counters, and reset state persist locally across restarts.

MindGate is a self-control aid, not a tamper-proof security product. A device owner may be able to disable or remove a self-management app, and enforcement strength differs by platform. See [platform feasibility](docs/platform-feasibility.md) before promising identical behavior everywhere.

## Repository layout

```text
apps/
  android/       Kotlin client and Android enforcement adapter
  ios/           Swift client and Screen Time extensions
  windows/       MindGate.Core and the .NET MindGate.WindowsClient
contracts/       Versioned, platform-neutral rule contract
docs/            Product scope, architecture, platform notes, team workflow
```

## Development status

The Windows prototype is runnable. Its MindGate-style dashboard includes Dashboard, My Apps, Analytics, and Settings sections; users can categorize monitored apps, set daily foreground-use allowances and reset times, receive warnings, and see a block notice at the limit. Rules and counters stay on the device. Android and iOS are currently at the platform-plan stage.

Install the [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/windows), then run the Windows prototype from the repository root:

```powershell
dotnet run --project apps/windows/MindGate.WindowsClient/MindGate.WindowsClient.csproj
```

Run the policy smoke checks with:

```powershell
dotnet run --project apps/windows/tests/MindGate.Core.SmokeTests/MindGate.Core.SmokeTests.csproj
```

## Documents

- [Product brief](docs/product-brief.md)
- [Architecture and runtime workflow](docs/architecture.md)
- [Platform feasibility and constraints](docs/platform-feasibility.md)
- [Team workflow and proposed ownership](docs/team-workflow.md)
- [App rule v1 contract](contracts/app-rule.v1.schema.json)
- [Example daily rule](examples/app-rule.daily.example.json)
