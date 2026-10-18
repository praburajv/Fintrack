# Complete top-bar theme synchronization

Scope: the Android status-bar backing and existing shared App Bar now use the
same selected `PrimaryDark` color. No dashboard redesign or authentication,
onboarding, data, currency, or navigation-route changes were introduced.

## Implementation

- `Platforms/Android/AndroidTopBarTheme.cs` observes the existing theme service.
  It colors native `AppBarLayout` containers, including their top-inset area.
  Android's status clock, battery and network icons stay white, matching the
  existing white App Bar foreground on all four current dark brand palettes.
- Theme updates are posted to the Android UI thread after MAUI appearance updates.
  Native layout changes cover newly created Shell toolbars. A weak per-app-bar
  color cache prevents repainting during routine layout passes, including when
  Material wraps the background drawable. Lifecycle refreshes invalidate the
  cache. No padding, inset listeners, navigation-bar flags, or content backgrounds
  are changed.
- `MainActivity` reapplies the top-bar appearance on resume, window focus and
  configuration changes, and releases event subscriptions on destruction.
  Existing backup/export activity-result handling is retained.
- `AppShell` ignores theme-selector initialization events during construction
  and initial layout, preserving saved System, Light or Dark appearance.
- Three unused PNG design references with invalid Android resource names are
  excluded from `MauiImage` packaging. Their files are retained unchanged; the
  active `fintracker_welcome_dashboard.png` is still packaged.

| Theme | Status backing and App Bar | Status icons |
| --- | --- | --- |
| Red | `#8E1B12` | White |
| Blue | `#1246A4` | White |
| Green | `#055B35` | White |
| Purple | `#2B0B98` | White |

## Verification

Verified on 8 October 2026:

- Android APK build: succeeded, zero errors, 33 existing package warnings.
- Windows build: succeeded, zero errors, 11 existing package warnings.
- Console model, navigation-policy and source-guard regression suite: 42 checks
  passed. Its cached NuGet vulnerability-feed warning remains.
- Android 15 / API 35 emulator: Red/Light, Blue/Light, Green/Dark, Purple/System
  and Blue/Dark all passed exact screenshot-pixel checks for equal status-strip
  and toolbar colors, plus readable white status-clock text.
- Selected System and Dark appearances and brand color survived process restart.
- Header search, existing main dashboard, Expense Report, Income entry navigation
  and Calendar remained usable. Bottom navigation was still hidden on entry
  forms and login. The introduction menu item remained available.
- First-launch welcome continuation, local registration/login, authenticated
  session restoration directly to Home, logout directly to Login, and returning
  logged-out startup without onboarding all passed.

[Verified screenshots](C:/Users/vishvanathanp/.codex/visualizations/2026/10/07/01a11709-d058-7ed1-ab17-258ddebba7ab/top-bar-theme-verified)
include all palette combinations and restart/logout states. The
[Blue Expense Report screenshot](C:/Users/vishvanathanp/.codex/visualizations/2026/10/07/01a11709-d058-7ed1-ab17-258ddebba7ab/top-bar-theme-verified/theme-report.png)
also confirms the native Back icon and shared header retain their layout.

The smoke test uses only `emulator-5554` and the separate
`com.companyname.fintracker.sharedcheck` application. It creates a temporary local
account without printing credentials, and saves no income or expense entries.
The normal installed application and physical device are not modified.
The disposable test application/account were removed after verification. The
APK and screenshots are retained; test-account data is not recoverable.

Rebuild and deploy the normal application from Visual Studio to see the correction
in your installed app. No uninstall or data reset is required. Older Android,
physical-device runtime, iOS and Mac Catalyst were not tested in this run.

## Platform rationale

Android 15+ renders behind the transparent system status bar; changing only the
legacy window status-bar color is insufficient. See the official
[Android 15 behavior changes](https://developer.android.com/about/versions/15/behavior-changes-15)
and [edge-to-edge guidance](https://developer.android.com/develop/ui/views/layout/edge-to-edge).
The matching MAUI 10.0.20
[inset listener](https://github.com/dotnet/maui/blob/10.0.20/src/Core/src/Platform/Android/MauiWindowInsetListener.cs)
applies top padding to the native app-bar container. This correction colors that
container while preserving MAUI's existing inset behavior. Older Android also
receives the legacy window color, but was not device-tested in this run.

Existing AndroidX dependency-constraint and SQLite vulnerability warnings remain
outside this UI correction. No packages were upgraded or warnings suppressed.
Global display-settings draft remains unwired, as previously requested.
