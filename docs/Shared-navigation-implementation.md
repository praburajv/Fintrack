# Shared navigation implementation

Scope: App Bar, Bottom Navigation, and a standalone Global Display Settings draft page. The existing main dashboard remains Home. No financial-dashboard redesign, database migration, package upgrade, or push is included.

## App Bar

`Pages/Controls/AppBar.cs` is reused by each authenticated page's `Shell.TitleView`, including pushed screens. Per-page instances are held by weak keys so native toolbars never compete for one view instance. It exposes branding, action visibility, commands, optional profile image/initials and notification-count properties.

| Component | Current connection |
| --- | --- |
| Menu | Native Shell hamburger/flyout; native Back is retained on pushed pages |
| Search | Focuses the existing dashboard shop/bill search; hidden elsewhere |
| Notifications | Hidden until a real data source and destination exist |
| User profile | Current-user initials, with Setup / Logout actions |

Header icons use code-native vector paths, not font glyphs. Header actions are hidden on authentication and entry screens. Shared actions have double-tap and critical-operation guards.

## Bottom Navigation

The existing `BottomTaskBar` is reused across its existing screens and added to Home, Income Analytics and monthly data maintenance. It is not added to entry forms. Close Application, backup/restore and other existing utilities remain in More's flyout.

| Item | Existing destination |
| --- | --- |
| Home | `//main` (existing LandingDashboardPage) |
| Reports | Chooser: expense report, monthly budget report, Income/Expense flow |
| Add | Chooser: Expense or Income, then `bill?type=…` |
| Calendar | `//calendar` |
| More | Existing Shell flyout |

Selection follows the completed route, including flyout and Back navigation. Add is an action, not a selected tab. New entries now read the supplied type; existing saves still use separate Income and Expense repositories. Entry pages retain their existing Save/Cancel/Back controls.

## Common / Global display settings

Shared chrome uses the existing theme/color resources and the new `Resources/Styles/SharedDisplay.xaml` font/icon tokens. Existing Setup theme and currency controls remain available.

The new `Pages/GlobalDisplaySettingsPage.cs`, its page model and `Models/GlobalDisplaySettings.cs` are intentionally **not registered in navigation and not connected to preferences/services**. The page can be constructed independently; adding a route/menu entry and app-wide persistence is a future wiring step.

The local draft includes:

- System / Light / Dark appearance.
- Primary, secondary, success, danger and warning colors, with hex validation.
- Existing font families, text size, corner radius, spacing and shadow controls.
- Currency code, number format and date format.
- Animation toggle and local animation preview.
- Locale choice for preview-date formatting. This does not yet translate screens.
- Live sample card, Reset Draft and Validate Draft. App-wide Apply is explicitly unavailable.

The preview uses example amounts and does not convert currencies or read/write financial records. Reset does not reset saved app preferences. A typed draft snapshot is available for later integration.

## Supporting navigation repairs

Login is now a hidden Shell root, consistent with existing startup/logout callers using `//login`. Its old global pushed-route registration was removed, and existing login callers were updated. Setup logout uses the same guarded logout-to-Login action. Login return targets are consumed rather than retained for a subsequent session.

## Verification

- Automated runner: `dotnet run --project tests/SharedNavigationRegression/SharedNavigationRegression.csproj`.
- 35 passing checks cover route classification, form/auth exclusions, selection, draft validation/reset/formatting, immutable snapshots and source-level wiring safeguards. The source checks are not a database CRUD regression test.
- Windows build passed with zero errors. Existing dependency/security warnings remain.
- Final Android build passed with zero errors (33 existing dependency/security warnings).
- Emulator smoke test passed on the final build: registration/login to existing Home, header Search, Expense Report with shared header/profile action, Add Income with bottom navigation hidden on the form, Calendar selection, More/Setup, Profile Logout to Login, and logged-out restart skipping onboarding. Screenshots were inspected for the header, vector icons, active selection and bottom safe-area spacing.
- The temporary emulator package and disposable test accounts were removed after verification. The normal installed app and physical phone were not modified. Test screenshots and the APK remain available; test account data was discarded.
- Android emulator smoke-test helper: `tests/SharedNavigationRegression/AndroidSmoke.ps1`. It requires an APK built with the isolated `com.companyname.fintracker.sharedcheck` application ID and uses only `emulator-5554`. It never clears the normal app's data or installs onto the physical phone. Credentials are generated in memory; no financial entries are saved.
- The settings page's native rendering and future app-wide wiring require separate verification. Its model is covered by the automated checks.

Example isolated Android build:

```powershell
dotnet build .\FinTrack\FinTracker.csproj -f net10.0-android --no-restore `
  -p:IntermediateOutputPath=obj\SharedNavigationCheck\net10.0-android\ `
  -p:OutputPath=bin\SharedNavigationCheck\net10.0-android\ `
  -p:ApplicationId=com.companyname.fintracker.sharedcheck `
  -p:ApplicationTitle='FinTracker UI Check' `
  -p:AndroidPackageFormats=apk -p:EmbedAssembliesIntoApk=true -m:1
```

These command-line identity overrides are for testing only; the project application ID is unchanged.
