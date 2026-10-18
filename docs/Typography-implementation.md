# Typography implementation

Implemented 2026-10-16 for Android and Windows. This change concerns general interface text and the containers necessary to display it.

## Behavior

- Inter Regular (400) for body text and text inputs, Medium (500) for buttons and radio-button actions, Semibold (600) for existing headings and emphasized labels.
- Existing font sizes and formatting metadata remain intact. Automatic OS text scaling stays enabled; no extra scale multiplication or text shrinking is introduced.
- App-owned truncated labels can wrap. The shared app bar has a minimum height of 52 instead of a fixed height, and bottom navigation labels can wrap.
- Login buttons can grow. Login and registration account-link rows constrain their explanatory text so that the action remains visible with enlarged text.
- Chart descendants, icon glyphs, explicit specialist fonts, and the independent Global Display Settings draft preview are excluded. Native platform chrome retains its platform typography.

The policy is applied centrally by `FinTrack/Services/InterfaceTypography.cs` after controls are loaded, when their ancestor chain can be checked. Native Android and Windows weight mapping selects the exact bundled font weight while retaining existing FontAttributes and bindings.

## Scope and provenance

Five existing files changed relative to the saved working-tree baseline: `FinTrackerProgram.cs`, `AppBar.cs`, `BottomTaskBar.xaml.cs`, `LoginPage.xaml`, and `RegisterPage.xaml`. The new policy, three fonts, licence, and focused regression project are additions. Earlier uncommitted work is preserved.

Chart files and global style dictionaries are byte-for-byte unchanged from the pre-typography baseline. The font registrations retain the existing default font first, so chart defaults are preserved. No database, backup/restore, search, SQL, ETL, authentication, or transaction logic was changed by this implementation.

Fonts are the static Regular, Medium, and SemiBold TTFs from the official [Inter 4.1 release](https://github.com/rsms/inter/releases/tag/v4.1). The release's SIL Open Font License is bundled in `FinTrack/Resources/Raw/inter-license.txt`.

## Validation

| Check | Result |
| --- | --- |
| Android net10.0 build and deployment to emulator-5554 | Passed, 0 errors; 22 existing package warnings |
| Windows net10.0-windows10.0.19041.0 build | Passed, 0 errors; 11 existing package warnings |
| Typography regression | 30 passed |
| Shared navigation regression | 79 passed |
| Bill reference regression | 84 passed |
| SQL/ETL regression | 58 passed |
| Financial account migration/isolation regression | 23 passed |
| Total automated checks | 274 passed |
| Android emulator visual check | Login at font_scale 1.0 and 2.0; registration at 2.0 |
| Typography-only rollback | Passed in two isolated copies, including preservation of a later unrelated edit |

Typography checks cover font roles, actual TTF weights, scaling, original font sizes/formatting, dynamic emphasis, wrapping, chart exclusions/reparenting, icon and draft-preview exclusions, and unchanged chart/style source hashes. These tests use MAUI stubs; platform builds and emulator checks provide additional evidence.

Enlarged-text testing found and corrected Login button clipping and an overflowing account-link row. The emulator text scale was restored to its original 1.0 afterward. No account was created and no financial data was changed. Physical devices were not used. Authenticated screens and charts were not visually tested, and Windows runtime visual testing remains outstanding. Fixed containers elsewhere may need individual device review at extreme accessibility settings; this implementation does not resize charts.

## Roll back only this implementation

Keep the entire `docs/rollback/typography-20261016` folder. It contains the pre-change snapshots, source hashes, an inverse patch, the addition manifest, and the rollback script.

From the repository root, check rollback readiness without changing anything:

```powershell
.\docs\rollback\typography-20261016\Rollback-Typography.ps1 -CheckOnly
```

To remove only this typography implementation:

```powershell
.\docs\rollback\typography-20261016\Rollback-Typography.ps1
```

The script checks all added-file hashes and patch applicability before changing files. It reverses only the five existing-file edits and removes the eight listed additions. Unchanged existing files are restored byte-for-byte from their original snapshots, including encoding and line endings. Later edits in existing files are retained where the inverse patch can apply. Conflicting edits or modified added files cause the preflight to stop for manual merging. It does not reset Git, restore database backups, or remove this report or the rollback evidence. Rebuild and redeploy after rollback to replace installed binaries.

The rollback was exercised only in temporary copies. The working application still contains the typography implementation.
