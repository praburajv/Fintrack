# Bill reference implementation

## Behaviour

- `Bill_transcation` and `income_transaction` store nullable `bill_reference_no CHAR(15)`. Existing rows keep NULL. References accept ASCII letters, digits, and `! @ # & - _`, preserve case/leading zeros, and permit duplicates. Internal spaces and other characters are rejected.
- Shared transaction entry labels the optional field **Bill Reference**, with a 15-character limit and a permitted-character hint. Manual notes accept ASCII letters, digits, spaces and line breaks, up to 1,000 characters. Unchanged historical notes remain editable alongside other fields. ETL descriptions and recurring-generated notes retain their existing content.
- Persistence validates references and manual notes; SQLite constraints/triggers also validate references. Queries remain parameterized.
- Bill Analysis displays separate expense/income transaction lists beneath the charts. Lists follow company/shop/year and show references when supplied. Reference search opens the matching year with an exact reference filter on the lists; clearing it restores the full lists. Charts continue showing annual totals. Returning to the page reloads its data.
- Both dashboards use the **Search Shop or Ref** prompt and shared shop/reference search. Shop names and references use case-insensitive prefix matching, within the active company. Shop punctuation is preserved; SQL LIKE wildcard characters in user input are escaped and matched literally, including the newly permitted underscore in references. Reference suggestions identify shop, date, type and amount; duplicate references remain separate. Results are paged in groups of 20 with Show more results. Changing companies clears selections and cancels stale searches.
- Initial launch/sign-in and post-restore selection use `CompanySelectionPage` at the existing `companies` route. `FinancialAccountCardsPage` retains its management controls at `companymanagement`, reached through the existing Financial Accounts menu. Selection keeps authentication, operation guards, currency application and pending destinations.

## Backup compatibility

Complete database backups preserve references in both transaction tables. Complete restore validates references before replacement, migrates older schemas, and retains the existing safety-copy/rollback mechanism.

The 2026-10-16 reference-policy upgrade replaces the earlier alphanumeric-only CHECK constraint through a transactional table rebuild. It preserves columns, existing data and IDs, unrelated constraints/defaults, explicit indexes/triggers, referencing rows/views, and AUTOINCREMENT high-water marks. Reference triggers are replaced with the expanded policy, and foreign keys are checked before committing a rebuild. Failed migrations roll back both transaction tables and restore the connection's pragma settings. Repeated initialization does not rebuild an already-upgraded table.

Selected-month JSON backups now include expense references. Older JSON without the field restores NULL. Invalid references are rejected before deleting selected-month data. Historical notes are restored without applying the new manual-entry restriction. Selected-month backups retain their existing expense-only scope; complete backups include income too.

The additive field retains backup format version 1, keeping supported older archives readable by this application version. Reference preservation by older application versions is not guaranteed.

## Verification

The regression harness links the real repositories, schema migration, search service, analysis service/page models and backup service; only platform/UI infrastructure is substituted. It uses a temporary database, never an installed application's data.

```powershell
dotnet run --project tests/BillReferenceRegression/BillReferenceRegression.csproj
dotnet run --project tests/FinancialAccountRegression/FinancialAccountRegression.csproj
dotnet run --project tests/SharedNavigationRegression/SharedNavigationRegression.csproj
dotnet build FinTrack/FinTracker.csproj -f net10.0-android --no-restore
dotnet build FinTrack/FinTracker.csproj -f net10.0-windows10.0.19041.0 --no-restore
```

Coverage includes validation boundaries, manual/historical/imported notes, old-schema migration, reference updates/clearing, database rejection, mixed shop/reference suggestions, the ABT prefix example, duplicate references, punctuation/wildcard handling, pagination, cancellation, rapid typing, company switching, correct navigation, summary reconciliation, both backup types, older archives, invalid archives and rollback after replacement.

Latest automated results (2026-10-16): 139 bill-reference checks, 23 financial-account checks, 86 shared-navigation checks, 58 SQL/ETL checks and 30 typography checks passed (336 total). Android and Windows builds completed with zero errors (22 and 11 existing dependency warnings respectively). The expanded checks include permitted symbols, literal underscore matching, reserved-character navigation, income category toggle notifications, transactional migration success/failure, complete/selected-month symbol round trips, and restoring an older constrained archive. See `Dashboard-reference-updates.md` for this follow-up change and runtime verification limits.
