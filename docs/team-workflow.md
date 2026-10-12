# Team Workflow

## Repository and branches

The GitHub repository is being renamed to `app-stopper`. Keep `main` as the integration branch. The existing team branches remain `team/lephallo1` and `team/rexlesenyeho` during the scope migration so collaborators do not lose their branch references.

After the migration pull request is merged, each teammate should synchronize their local branch with the new `main` only after preserving any uncommitted or unpublished work. Continue to use focused pull requests into `main`; do not push directly to `main`.

## Proposed module ownership

- **Lephallo1 — product core and Windows:** maintain the rule contract and product decisions, build the first Windows client/foreground-use adapter, and coordinate integration and releases.
- **Rexlesenyeho — Android:** build the Kotlin client, app usage monitoring, permission flow, local rule storage, warnings, and the initial blocking proof.
- **Shared iOS spike:** agree on Mac access and entitlement steps, then implement the Swift Screen Time proof together or assign it once platform access and team capacity are known.
- **Shared:** agree on warning/reset semantics, review platform claims, test recovery and safety behavior, and review pull requests.

These assignments are a starting proposal. Do not begin dependent modules until both contributors agree on the policy contract and the first reset mode.

## Integration contract

Use [`app-rule.v1.schema.json`](../contracts/app-rule.v1.schema.json) as the shared rule shape. Each platform stores its own opaque app selection reference locally. Do not assume that a package ID, Apple application token, Windows executable path, or rule selection can be transferred unchanged to another platform.

Before each milestone is merged:

1. Run the platform’s unit tests and build.
2. Test a short allowance on a real device or a clearly identified simulator.
3. Verify warnings fire once, a block appears at zero, and access returns at reset.
4. Test app/service restart, OS restart, permission removal, and a clock/time-zone boundary.
5. Confirm essential apps remain available and the status screen reports enforcement health.

## First milestones

1. Agree on daily-reset versus cooldown semantics and finalize the rule contract.
2. Run three feasibility spikes: Android use-to-block, iOS threshold-to-shield, and Windows foreground-use-to-block.
3. Select the initial reset mode and build the first end-to-end vertical slice.
4. Add recovery, privacy, and platform-specific permission UX.
5. Integrate platform clients and decide whether cloud sync is actually needed.
