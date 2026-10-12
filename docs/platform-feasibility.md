# Platform Feasibility

The product is feasible, but “block an app” is a different OS operation on each platform. The shared part is the rule behavior and user experience; the enforcement adapters are separate native implementations.

## Android

**Candidate technology:** Kotlin, Android UsageStats APIs, local Room/SQLite storage, Android notifications, and a native blocking presentation.

Android exposes usage statistics through `UsageStatsManager`, but the user must grant Usage Access in Settings. See the [Android API reference](https://developer.android.com/reference/android/app/usage/UsageStatsManager).

The enforcement mechanism needs an early device test. An Accessibility Service can observe interface transitions, but it is a sensitive permission and cannot be used as a covert or tamper-proof control. Google Play requires a declaration and prominent disclosure/consent for non-accessibility-tool uses, and restricts preventing users from disabling or uninstalling apps. See [Google Play’s Accessibility API policy](https://support.google.com/googleplay/android-developer/answer/10964491). The design must not claim that a user-owned phone is impossible to bypass.

**First proof:** choose one ordinary app, obtain usage permission, measure a short foreground allowance, show a warning, present the block, then recover correctly after reboot and permission removal.

## iOS and iPadOS

**Candidate technology:** Swift, Family Controls, Device Activity, Managed Settings, and the related shield extensions.

Apple provides a system app picker, activity monitoring thresholds, and shields for selected apps. Individual users can authorize their own device through Family Controls; see [AuthorizationCenter](https://developer.apple.com/documentation/FamilyControls/AuthorizationCenter), [DeviceActivityEvent](https://developer.apple.com/documentation/deviceactivity/deviceactivityevent), and [ShieldSettings](https://developer.apple.com/documentation/managedsettings/shieldsettings).

Distribution requires Apple approval for the Family Controls entitlement, including the relevant Screen Time extensions. See [Apple’s entitlement request instructions](https://developer.apple.com/documentation/familycontrols/requesting-the-family-controls-entitlement). Development also requires access to a Mac with a compatible Xcode version; see [Xcode system requirements](https://developer.apple.com/xcode/system-requirements).

**First proof:** request individual authorization, select one app, monitor a short threshold, apply a shield at the threshold, and confirm it clears at reset. Start this proof early because entitlement and Mac access are external dependencies.

## Windows 10 and 11

**Candidate technology:** C#/.NET desktop client, a Windows foreground-process adapter, local storage, and a clearly visible blocking window or supported OS policy integration.

A user-mode focus blocker can detect a selected foreground process and present a block screen, but a person with administrator control may stop or bypass it. AppLocker and App Control for Business provide OS application-control policies, but they are designed for policy-based device administration rather than a lightweight personal app timer. Microsoft explicitly describes AppLocker as defense in depth rather than a security boundary; see the [AppLocker overview](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/applocker/applocker-overview) and [application-control overview](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/appcontrol).

**First proof:** detect a selected foreground application, accumulate time while it is active, present a block at a short limit, and verify the UI communicates if the monitor is stopped.

## Cross-platform implications

- A browser-only HTML/CSS/JavaScript interface cannot enforce limits on other native apps.
- A shared UI toolkit could reduce duplicated settings screens, but it does not remove the need for Kotlin, Swift, and Windows-native enforcement code.
- The first release should be described as a self-management aid. Stronger controls against a device administrator require a different, explicitly managed-device product design.
- Keep usage data local until users have a clear need for sync. Live testing uses consenting team members’ own devices; no third-party or institutional dataset is required.
