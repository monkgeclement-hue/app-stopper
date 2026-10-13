# MindGate — Product Brief

## Problem

People often decide in advance how much time they want to spend in selected apps, but reminders alone can be easy to dismiss. Built-in controls also differ across phones and computers. The project will test whether a small, transparent tool can help a person follow their own app-use limits across the devices they use.

The system should count active use of apps the person selected, warn before their allowance runs out, and temporarily restrict those apps after the limit. It must state clearly that people who own and administer their devices may be able to disable a self-control tool.

## Goal

Build a privacy-first app-limiting system for Android, iOS/iPadOS, and Windows that lets a person set per-app usage budgets and gives consistent warnings and lockout behavior using the enforcement mechanisms each operating system permits.

## Intended users

The first audience is people who want help managing their own time in chosen apps: students, workers, and anyone who wants more deliberate screen habits. The first release is not a remote parental-control or employee-surveillance product.

## Objectives

1. Let a user choose supported apps on their own device and define a per-app usage allowance.
2. Support a clear reset rule: a daily reset at a chosen local time or a cooldown after the allowance is exhausted.
3. Warn at configured remaining-time thresholds and show the next reset time.
4. Apply a native block when the allowance is exhausted and remove it when the rule resets.
5. Preserve rules and usage counters across app restarts and device restarts.
6. Keep app selections and usage history local by default, with no account, analytics upload, or cloud sync in the first release.
7. Test platform permissions, enforcement, restart behavior, time zones, and reset boundaries on real devices before describing the feature as reliable.

## Core rule semantics

Each rule contains a selected app reference, an allowed foreground-use duration, warning thresholds, and one reset policy. “60 minutes per day, resets at 04:00” and “60 minutes of use, then a 24-hour cooldown” are different policies and must be presented as such. For the first usable version, the team should choose one reset mode and add the other only after the first is tested.

The proposed state flow is `READY → WARNING → LIMIT_REACHED/BLOCKED → RESET`. A warning threshold fires once per rule per reset cycle. Only foreground use while the device is in use counts; background playback and time with the screen off do not count unless a later, explicit feature says otherwise.

## Scope

### First release

- Native clients and local rule storage for Android, iOS/iPadOS, and Windows.
- User-selected apps only; one budget per selected app.
- Remaining-time warnings and a visible block screen/shield.
- Daily reset or cooldown behavior, after the team selects and tests the initial mode.
- Local history sufficient to show usage today, remaining time, and next reset.
- Permission disclosure, permission-status checks, and a safe recovery path.

### Out of scope initially

- Guaranteed tamper-proof blocking when the user controls the device administrator account.
- Remote parental controls, employee monitoring, or administrator policy management.
- Cross-device accounts and cloud sync.
- Website blocking, recommendation algorithms, social features, or advertising.
- Blocking emergency calling, security, accessibility, or other essential system functions.

## Privacy and safety principles

- Keep selected app references, rule settings, and usage totals on-device in the first release.
- Request only the platform permissions needed for the feature, explain them before the OS prompt, and offer a way to review or remove rules.
- Never claim to prevent a user from uninstalling or disabling the app on a personally administered device.
- Exclude phone/emergency functions and essential system apps from selectable targets.
- Provide a clear recovery path if permissions are removed, the clock changes, or the blocking service stops.
- Do not silently upload usage data. Any future sync must be opt-in, documented, and designed separately.

## Success criteria for the first vertical slice

On each supported OS, a tester can choose one non-essential app, set a short test allowance, see a warning, reach a block, restart the device without losing the rule, and regain access at the reset boundary. The app reports unsupported permissions or enforcement limits honestly rather than silently implying the block is active.
