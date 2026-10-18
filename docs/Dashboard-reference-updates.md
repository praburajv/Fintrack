# Dashboard, reference and report updates

Implemented on 2026-10-16 after approval of the read-only analysis.

## Changes

- **Financial Dashboard greeting:** shares the outer dashboard width with the company selector, overrides inherited border padding with zero so the artwork shares those edges, uses a minimum height of 176 instead of a fixed 142, and grows to fit content. Greeting/name/company, period selection and the closing message occupy separate automatic-height rows. Long names wrap. The background image follows the content's measured bounds; the rounded clip still updates with the card size. The period picker retains its selection and busy-state bindings.
- **Search prompt:** both Financial Dashboard and Dash cool display `Search Shop or Ref`.
- **Bill Reference:** shared income/expense entry uses this exact label with a separate optional/15-character hint. Permitted characters are ASCII letters, digits and `! @ # & - _`. Interior spaces, quotes, percent signs, backslashes, controls and other characters remain invalid. Blank input becomes NULL; the limit is 15 characters. Note rules are unchanged.
- **Search behavior:** shop-prefix behavior remains intact. Both transaction tables accept permitted-symbol prefixes. LIKE metacharacters are escaped, so `_` is literal in a reference. Parameterized queries, account isolation, pagination, cancellation and selection behavior remain in place. Reserved characters are URL-encoded when opening analysis.
- **Income Analytics Dash:** category headers now invoke the existing toggle command with the selected category. The command and group model already support detail visibility and arrow notifications. Income data, totals and charts are unchanged.
- **Monthly Budget Report:** reuses `PeriodNavigationControl` for the bordered previous / Month Year / next selector, bound to the existing month text and commands. Budget calculations, loading guards and report preview behavior are unchanged.

## Database and backup compatibility

The previous reference implementation installed an alphanumeric-only CHECK constraint. Existing tables with that exact constraint are rebuilt transactionally using their complete original definition with only the reference character rule changed. Both transaction-table upgrades participate in the same transaction. Columns/data/IDs, unrelated constraints/defaults, indexes, custom triggers, views/referencing rows, and AUTOINCREMENT high-water marks are preserved. The new constraint marks an upgraded table, preventing repeat rebuilds. The reference insert/update triggers are replaced with the new allowlist, including for databases with an unconstrained existing reference column. Foreign keys are checked before committing a rebuild, and connection pragma settings are restored on success/failure.

The procedure follows SQLite's [documented table-rebuild sequence](https://www.sqlite.org/lang_altertable.html#making_other_kinds_of_table_schema_changes). No direct writable-schema changes are used.

Complete and selected-month backup/restore already call the shared reference validators and schema migration. They now accept and preserve the new symbols. Complete archives include both income and expense; selected-month archives retain their existing expense-only scope. Older archives without references restore NULL, and older constrained archives are upgraded. Unsupported reference values are rejected before destructive restore steps. Backup format remains version 1. Older application versions with the original restrictive validation should not be used to restore archives containing the new symbols.

## Automated verification

| Suite | Passing checks |
| --- | ---: |
| Bill reference, real SQLite migration/search/backup/restore, income toggle | 139 |
| Shared navigation and affected XAML wiring | 86 |
| SQL/ETL regression | 58 |
| Typography regression | 30 |
| Financial account migration/isolation | 23 |
| Total | 336 |

Migration tests cover successful and failed upgrades of both legacy constrained tables, original IDs/values, defaults/checks, explicit indexes, custom trigger behavior, child rows, views, deleted AUTOINCREMENT high-water marks, idempotence and connection-setting restoration. Search tests cover all six symbols across both transaction types, literal underscore matching, unchanged shop punctuation/paging/account isolation, and encoded navigation to the correct analysis records. Backup tests cover both archive types, symbols, older archives, invalid data and safety rollback.

Android and Windows builds passed with zero errors (22 and 11 existing package warnings respectively). The normal Android build was deployed to emulator-5554 without clearing application data. Typography rollback preflight still passes; chart files and global style dictionaries match the saved pre-typography hashes.

## Android runtime verification

Verified on emulator-5554 using a newly installed, separate `com.companyname.fintracker.sharedcheck` package with a disposable local account and fictional financial fixtures. The normal application's database was not cleared or seeded, and no physical device was used.

- Greeting inspected with a long display name at OS font scales 1.0 and 2.0: text wraps into separate rows without overlap, the card grows, and its artwork edges align with the company selector. Visual testing exposed inherited border padding, which was explicitly removed from this card.
- Requested search placeholder inspected; `AB_` displayed the matching income and expense references and excluded the `ABX` record.
- Income category header expanded to show both fictional transactions and their references, then collapsed again.
- Shared transaction form displayed **Bill Reference** and the separate optional/15-character hint.
- Monthly Budget Report displayed the bordered month control. Previous/next navigation worked, including December 2026 to January 2027 and back.

Evidence screenshots are in `docs/verification/dashboard-reference-20261016`. The isolated test package and its disposable data were removed after verification, and the emulator's original font scale of 1.0 was restored. The final normal Android build was deployed, and both final platform builds passed with zero errors.

Windows visual testing remains outstanding. At font scale 2.0, separate issues were visible in the existing app-bar company subtitle (clipping) and bottom-navigation labels (awkward word breaks). Those controls are outside this follow-up's changes. The requested greeting itself remained readable. Automated XAML assertions are structural checks, not substitutes for device measurements.

## Files changed in this follow-up

Application files: `Pages/FinancialDashboardPage.xaml`, `Pages/LandingDashboardPage.xaml`, `Pages/BillEntryPage.xaml`, `Pages/IncomeAnalysisPage.xaml`, `Pages/MonthlyBudgetReportPage.xaml`, `Utilities/TransactionInput.cs`, `Data/BillReferenceSchema.cs`, and `Services/DashboardSearchService.cs`.

Regression files: `tests/BillReferenceRegression/Program.cs`, its project file, and `tests/SharedNavigationRegression/Program.cs`. Documentation: this report, `Bill-reference-implementation.md`, and the verification screenshots. Other pre-existing working-tree changes were retained.
