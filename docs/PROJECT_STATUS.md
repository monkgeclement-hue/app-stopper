# MindGate Project Status

Last updated: 2026-10-14  
Code baseline: `845ef56` on `codex/mindgate-reference-dashboard`  
Integration: [PR #4](https://github.com/monkgeclement-hue/mindgate/pull/4) is open; these changes are on the feature branch, not yet merged to `main`.

## Where we are

MindGate is a local-first app-time manager for people who want help following limits they set for themselves. The Windows client is the only implemented native client so far. Its selected-app tracking and limit flow can be run locally; the broader Windows-and-Android product still needs platform work, real-device testing, and recovery checks before it is ready to release.

The current coding focus is functionality and reliability. The visual direction is intentionally open for later review; the current pages should not be treated as the final Curb-inspired interface.

## Completed on the current Windows branch

- C#/.NET 10 Windows client with Dashboard, Apps, Analytics, and Settings sections.
- Local app rules with categories, supported desktop-app selection, daily budgets, reset times, warnings, and usage counters.
- Foreground-app tracking and a blocking overlay when a selected app reaches its limit.
- Local rule persistence, legacy App Stopper state migration, pause/resume controls, system-tray operation, and a single-instance guard.
- Optional launch at Windows sign-in.
- Optional idle-aware counting: users can pause usage counting after 1–60 minutes without keyboard or mouse input. It is off by default.
- Release build and existing policy smoke checks pass on the current branch.

## Current limitations to keep visible

- Windows blocking is a user-mode overlay. MindGate must remain running, and someone with control of the PC can stop or bypass it; it is not OS-level tamper-proof enforcement.
- Microsoft Store packaged apps are not supported by the current Windows picker.
- Analytics shows the active usage cycle; it does not yet retain weekly history.
- The Android and iOS folders contain planning notes, not implemented native clients.
- The successful build and core smoke checks do not replace the end-to-end tests below on a real Windows session.

## Active work

There is no uncommitted coding task in progress right now. The current Windows changes are committed and pushed to PR #4. The next active milestone is to test the full limit-and-recovery flow, record failures, and fix them before treating this Windows slice as dependable.

## Next work, in order

1. **Verify Windows behavior end to end.** Add a short limit to a non-essential desktop app; confirm usage increments only while it is foregrounded, warnings occur once at the configured thresholds, the block appears at the limit, and access returns at reset. Also test idle detection on/off, pause/resume, MindGate restart, Windows sign-in startup, sleep/wake, and local state recovery.
2. **Harden monitoring and recovery.** Investigate and handle what users see if MindGate exits, loses access to a process, or the selected app closes while limited. State clearly whenever Windows cannot enforce a rule. Do not promise stronger enforcement than the operating system integration provides.
3. **Add useful local history.** Persist daily totals so Analytics can show trends beyond the active cycle, while keeping usage data on-device.
4. **Build the Android vertical slice (Rexlesenyeho).** Start with Kotlin, explicit Usage Access, one selected app, foreground-time accounting, a warning, a block flow, local persistence, and recovery when permission is removed. Agree on rule/reset behavior before integrating it with the shared contract.
5. **Revisit iOS later.** Defer implementation until Mac/Xcode access, Apple Family Controls entitlement needs, and the team’s capacity are confirmed.

## Team split and shared decisions

- **Lephallo1 + Codex:** product decisions, shared rule contract, Windows client, integration, and release coordination.
- **Rexlesenyeho:** Android client and Android-specific permission, usage, and blocking work on a separate branch/PR.
- **Both:** agree on limit/reset semantics, test recovery and permission-loss behavior, review platform claims, and merge through pull requests.
- **Product boundaries:** personal self-management; local-first data; no remote parental/employee monitoring or cloud sync in the first release. AI insights are deferred until the deterministic tracking flow is reliable and a privacy design is agreed.

## How we will keep this current

At the end of each meaningful coding session, update this file: move finished items to **Completed**, describe the task actually being worked on under **Active work**, and keep **Next work** ordered by dependency. Link the relevant commit or PR and record the checks that passed.
