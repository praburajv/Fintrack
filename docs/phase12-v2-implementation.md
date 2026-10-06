# Phase 12 v2 implementation report

Implemented against `impl_phase12_GeneralImprovement_v2.txt`, after the explicit GO. Existing unrelated working-tree changes were preserved. No application/user database or connected-device installation was modified during verification; tests used isolated temporary data.

## Income Expense List

Removed the Shop Visit heading, chart, query and unused handler from this list. Its FullYear mode already hid that chart, so this removes the Dashboard integration without removing shared Shop Visit components or the intended charts on other pages.

Income categories now expand to individual bills, using the same `FinancialCategoryTemplate` as Expense. Rows expose the existing Edit icon/command, with Income monetary values still green. Edit uses the existing `BillEntryPage` / `BillEntryPageModel`, passing the original ID and `type=Income`. Its existing validation, locked transaction type, Income repository update and `BillChangedMessage` refresh remain in use. No separate edit form was introduced.

## Monthly deletion

The old monthly operation deleted all selected-month expenses. That behavior is retained as an explicit **All monthly expenses + imported Income** scope. A **Select one ETL import** scope additionally restricts both sides to that import within the selected month. Credit-only months now enable Delete.

Both transaction tables receive an additive, nullable `EtlImportId` column. New Debit/Credit imports share a unique identifier containing the source filename, import timestamp and unique token. Provenance is inserted with the transaction row, survives ordinary editing, and does not change Debit/Credit routing or duplicate detection. Existing/manual/duplicate rows are not automatically relabelled.

Proven imported Income is selected by month and, when chosen, import ID. Untracked older Income is never automatically deleted based on category, shop or payment mode. The whole-month workflow offers a record-by-record review: only records explicitly confirmed as older ETL Credits are included. Users can keep them or cancel. An old imported Credit whose shop/date has been edited can still be reviewed in its current month.

Deletion of Expense and applicable Income uses one SQLite transaction. It verifies the reviewed IDs and values, selected month/import, and current scope before deleting. New scoped records, edited/deleted records or SQL failures abort and roll back the operation. Other months, other imports in import-specific mode, and unapproved manual Income remain protected. Counts, progress and affected-month refresh notifications are updated after success.

Important: older Income has no reliable historical import identity. It requires individual confirmation; choosing to keep untracked Income intentionally leaves it in place. The application does not claim that kept records have been cleaned up.

## Shop Visit

Daily Dash did not have a Shop Visit chart in the inspected source. It now reuses the shared chart for each existing Daily, Weekly and Monthly section, with that section's exact date bounds. Expense Report and Expense Statement use the same shared component.

The shared chart opens the existing `DailyBillListPage` through one route helper, passing shop ID, start date and exclusive end date. The bill-list validation now accepts shop-plus-range navigation without requiring a single-day date. Its title reflects the range, and reused query state is reset to prevent stale filters.

No pre-existing Shop Visit ASC/DESC control was present. The existing bill-list button/arrow sorting pattern was reused for **Visits ASC/DESC**, with shop-name/ID tie-breakers. The displayed list, palette and selected index use the same ordered objects, so each bar retains its shop, visit count and total amount. Existing non-target chart consumers retain their original source order and event-based navigation.

The page-level processing overlay, application icon, spinner and bottom-control screen lock are reused while sorting or opening bills. Selection is cleared to allow repeated taps after returning.

## Category and PDF ordering

`ReportBillOrdering` centralizes case-insensitive Shop Name ASC, then transaction date DESC and ID DESC. It is applied inside each Expense Report category and by the statement builder. Category grouping, totals and bill counts remain unchanged; global grouping on unrelated pages was not altered.

The statement preview and PDF writer consume the same ordered snapshot. View PDF and Share PDF continue to use the same cached PDF generation path. Changing statement Shop Visit sort order updates its snapshot and invalidates that PDF cache. The existing PDF Shop Visit output is preserved.

## Backup compatibility

Full backups retain the new columns naturally. Selected-month Expense JSON backups preserve Expense import IDs; older JSON without that optional property remains compatible and restores with unknown provenance. The existing selected-month restore remains Expense-only and does not remove Income.

Old full restores add missing provenance columns, including when repository instances have already been initialized. Regression testing also exposed a Windows temporary-backup file lock from retained pooled connections. Backup-service connections now disable pooling so disposed temporary files can be hashed/read/deleted; the general repository connection architecture is unchanged.

## Files changed in this implementation

| File | Reason |
| --- | --- |
| [MyMExpense.xaml](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/MyMExpense.xaml) | Shared Income/Expense category and editable bill template; remove list Shop Visit UI. |
| [MyMExpense.xaml.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/MyMExpense.xaml.cs) | Remove unused Shop Visit navigation handler. |
| [ExpenseListPageModel.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/PageModels/ExpenseListPageModel.cs) | Remove list-specific Shop Visit queries; guarded edit navigation and processing rendering. |
| [DailyDashPage.xaml](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/DailyDashPage.xaml) | Reuse daily/weekly/monthly Shop Visit charts. |
| [DailyDashPageModel.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/PageModels/DailyDashPageModel.cs) | Load period-matched shop visits and expose shared overlay state. |
| [ShopVisitsChart.xaml](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/Controls/ShopVisitsChart.xaml) | Shared ASC/DESC button and ordered chart dataset. |
| [ShopVisitsChart.xaml.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/Controls/ShopVisitsChart.xaml.cs) | Sort, synchronized selections/palette, reusable period-aware drilldown and busy state. |
| [ShopVisitOrdering.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Utilities/ShopVisitOrdering.cs) | Shared deterministic visit ordering. |
| [ShopBillNavigation.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Utilities/ShopBillNavigation.cs) | Validate and build existing shop/range bill-list route. |
| [DailyBillListPageModel.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/PageModels/DailyBillListPageModel.cs) | Accept shop/range drilldowns, display range and clear stale query filters. |
| [ExpenseReportPage.xaml](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/ExpenseReportPage.xaml) | Bind shared chart sort/navigation/overlay properties. |
| [ExpenseReportPage.xaml.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/ExpenseReportPage.xaml.cs) | Remove duplicated handler superseded by the shared chart path. |
| [ExpenseReportPageModel.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/PageModels/ExpenseReportPageModel.cs) | Apply category-local shop ordering and preserve chart sort in statements. |
| [ReportBillOrdering.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Utilities/ReportBillOrdering.cs) | Shared Category-to-Shop ordering rule. |
| [ExpenseStatementBuilder.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Services/ExpenseStatementBuilder.cs) | Build statement/PDF rows using shared ordering. |
| [ExpenseStatementSnapshot.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Models/ExpenseStatementSnapshot.cs) | Copy an immutable snapshot with updated Shop Visit ordering. |
| [ExpenseStatementPreviewPage.xaml](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/ExpenseStatementPreviewPage.xaml) | Shared chart navigation, sorting and processing binding. |
| [ExpenseStatementPreviewPageModel.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/PageModels/ExpenseStatementPreviewPageModel.cs) | Synchronize snapshot sort and invalidate the shared PDF cache. |
| [EtlImportSchema.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Data/EtlImportSchema.cs) | Serialized additive provenance migration; unknown rows stay unknown. |
| [BillTransactionRepository.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Data/BillTransactionRepository.cs) | Migrate schema and write Debit provenance on insert only. |
| [IncomeTransactionRepository.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Data/IncomeTransactionRepository.cs) | Migrate schema and write Credit provenance on insert only. |
| [EtlPdfImportService.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Services/EtlPdfImportService.cs) | Pass the same unique import ID to newly created Debit/Credit rows. |
| [MonthlyDeletionPlan.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Models/MonthlyDeletionPlan.cs) | Scoped review snapshots and deletion result counts. |
| [MonthlyFinancialDeletionService.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Services/MonthlyFinancialDeletionService.cs) | Provenance counts, safe month/import plans and transactional deletion. |
| [ExpenseMonthMaintenanceItem.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Models/ExpenseMonthMaintenanceItem.cs) | Imported/untracked Income counts and credit-only deletion availability. |
| [MonthlyExpenseDataPage.xaml](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Pages/MonthlyExpenseDataPage.xaml) | Show Income counts and enable Delete for applicable credit-only months. |
| [MonthlyExpenseDataPageModel.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/PageModels/MonthlyExpenseDataPageModel.cs) | Scope/import selection, individual legacy review, confirmation and guarded deletion. |
| [BackupService.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/Services/BackupService.cs) | Preserve provenance through backup/restore and release temporary Windows file handles. |
| [MauiProgram.cs](C:/src/HuntCrocMaui/MauiApp1/MauiApp1/MauiProgram.cs) | Register the shared monthly deletion service. |
| [Phase12Regression.csproj](C:/src/HuntCrocMaui/MauiApp1/tests/Phase12Regression/Phase12Regression.csproj) | Link actual implementation code into an isolated regression harness. |
| [PlatformStubs.cs](C:/src/HuntCrocMaui/MauiApp1/tests/Phase12Regression/PlatformStubs.cs) | Substitute only platform services; redirect data/cache to temporary test paths. |
| [Program.cs](C:/src/HuntCrocMaui/MauiApp1/tests/Phase12Regression/Program.cs) | Eighteen executable regression checks. |
| [README.md](C:/src/HuntCrocMaui/MauiApp1/tests/Phase12Regression/README.md) | Test command, isolation and device-testing limitations. |
| [This report](C:/src/HuntCrocMaui/MauiApp1/docs/phase12-v2-implementation.md) | Implementation details, changed-file inventory and verification evidence. |

## Verification

```powershell
dotnet build MauiApp1/CrocHuntMPluto.csproj -f net10.0-windows10.0.19041.0 --no-restore -v quiet
dotnet build MauiApp1/CrocHuntMPluto.csproj -f net10.0-android --no-restore -v quiet
dotnet run --project tests/Phase12Regression/Phase12Regression.csproj --no-restore -v quiet
```

Final Windows build: success, 0 errors / 11 warnings. Final Android build: success, 0 errors / 22 warnings. Existing dependency-version and SQLite package security warnings remain; package versions were not changed in this corrective scope. Whitespace/diff check passed, with pre-existing line-ending notices.

All 18 automated checks passed:

1. Additive/idempotent migration preserves legacy rows and unknown provenance.
2. Debit-only monthly deletion retains its existing expense scope.
3. Credit-only deletion works and keeps identical-looking manual Income.
4. Mixed Debit/Credit deletion leaves no scoped imported rows and protects other months.
5. Import-specific deletion protects other imports and ordinary transactions.
6. Older Credit review deletes only individually approved IDs.
7. Income updates retain ID/provenance, change totals, and leave Expense untouched.
8. An injected Income SQL failure rolls back already-deleted Expense rows.
9. Changes after confirmation invalidate the plan and roll back.
10. Shop/range bill-list loading works without a date and excludes other shops/months.
11. New matching imports during confirmation abort the stale plan.
12. Selected-month backup round-trips provenance; older JSON remains compatible; Income is untouched.
13. Full backup round-trips imported Income and its edited value/provenance.
14. Older full backup restores successfully and allows subsequent tagged Debit/Credit insertion.
15. December/January bounds and edited Income dates/shops preserve deletion scope.
16. ASC/DESC retains shop identity, amounts, palette objects and route targets.
17. Category-local Shop ordering, date/ID ties and actual generated PDF content agree.
18. XAML/source integration retains shared editing, intended charts, period parameters, overlays and a common View/Share PDF path.

The harness runs actual repository/SQL, deletion, bill-list view-model, ordering, statement and PDF code. MAUI platform surfaces are stubbed. Device taps, chart rendering, native Edit/Back navigation, screen-lock appearance and native PDF viewing/sharing still need interactive verification. A connected Android device was detected but no build was installed on it. Real bank-statement PDF re-import was not exercised; parser, duplicate handling and Credit routing were preserved in source.

Preserve the working features: this change is scoped to the corrections above. Automated coverage and both builds pass, but they are not a guarantee of every existing runtime flow; complete device acceptance testing before treating all UI acceptance criteria as verified.
