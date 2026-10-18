# Search History, selectors and income category chart

Implemented after approval on 2026-10-18. Existing working-tree edits were preserved; a pre-change source copy was saved to the task's temporary directory for comparison.

## Behavior

- The Expense Analytics dashboard's **Expense Analysis** action now has a compact local font/padding/alignment configuration while retaining automatic scaling and a usable touch target.
- Financial Dashboard includes a separate **Income by Category** pie and category amount/percentage rows. It uses the already-loaded income rows for the selected company and dashboard period. Slice/list selection opens the existing daily bill drilldown with `type=Income` and the exact dashboard date range. Expense chart files and behavior were retained.
- Reports hub has **Search History**, with red **Expense by** and green **Income by** columns, each offering **Category** and **Shop**.
- Category history groups by shop ID and shows shop totals with expandable bill details. Shop history groups by year, then month, with totals at both levels and expandable details. Group headings remain visible when collapsed. Bills have date, shop, reference, payment mode and an income/expense-colored amount.
- History supports selecting any combination of the current year and previous four years. Only the selected years are queried; nonconsecutive selections do not include intervening years. Default is the current year. At least one year and a valid selected category/shop are required.
- Four bill orders are offered: date newest/oldest and amount highest/lowest. Sorting stays within each group and uses deterministic ID ties. Year/month groups remain newest first. Changing filters invalidates earlier results and disables export until a fresh Search.
- Preview/view/print/share operates on an immutable filtered snapshot. The PDF includes all matching bills regardless of collapsed UI groups, selected years, ordering, group subtotals, overall total, company/currency, references and notes. Income and expense filenames/headings are distinct. Native PDF opening/printing and sharing follow the app's existing launcher/share-sheet approach; a compatible viewer is needed for printing.

## Searchable selectors

Transaction Shop, Shop Entry Category, and history filters use a new selector. Touch opens the complete company-scoped list with a search input and a scrollable CollectionView. Empty/cleared text shows all items with no fixed result cap. Typing applies case-insensitive name-prefix filtering; shop choices also show category names. Only choosing a listed object establishes the saved ID; free text cannot become a shop/category.

Typing clears the active selection during the search. Cancel restores the prior selection if still valid for the same company, including when master objects were refreshed. Selection and cancellation compare persisted ID plus company, rather than object reference. Company changes close the selector and clear the selection. The selector does not issue overlapping asynchronous database searches, so local keystroke filtering cannot publish an older query result.

Transaction category continues to come from the selected shop and is shown by the selector. Shop Entry remains responsible for category assignment. Existing shop-management actions are retained. Transaction entry refreshes available masters on return, preserves selected IDs, and rejects saving into a different company if the company changed while the form was open. Shop Entry has the equivalent save guard.

## Compatibility and implementation

History queries use the existing company-scoped repositories with half-open year ranges and parameterized SQL. No database schema, transaction table, backup format, existing note/reference rule or category-at-entry model was changed. Category reporting continues to use a shop's current category assignment.

New files include `Models/HistoryReport.cs`, `Utilities/HistoryGrouping.cs`, `Utilities/SelectionSearch.cs`, `Services/HistoryReportService.cs`, `Services/HistoryDocumentService.cs`, `Pages/HistoryReportPage.cs`, `Pages/HistoryPreviewPage.cs`, `Pages/Controls/SearchableSelector.cs`, and the two `IncomeCategoryChart` files. The existing document service was made partial to reuse its low-level PDF writer without changing existing statement/budget generation methods. History navigation is classified under Reports.

The prior backup, ETL, reference-schema/validation and dashboard-search service files were hash-checked unchanged against this turn's pre-change source copy. Existing expense chart files/global styles remain protected by the typography regression's baseline hash checks. The typography-only rollback preflight remains available and was rechecked during implementation.

## Verification

All 414 automated checks passed:

| Suite | Passed |
| --- | ---: |
| Bill reference, backup/restore, history and selectors | 216 |
| Shared navigation | 87 |
| SQL and ETL | 58 |
| Typography | 30 |
| Financial accounts | 23 |

The normal Android build completed with zero errors and 22 existing warnings; the Windows build completed with zero errors and 11 existing warnings. The typography rollback CheckOnly preflight passed. Existing unrelated whitespace issues in `Installations.txt` and `ReadMe.txt` were left intact.

Android emulator verification used a separate disposable package, `com.companyname.fintracker.historycheck`, with fictional data. It verified the populated income chart and income-only drilldown, Reports hub links, case-insensitive filtering, multi-year totals, expandable year/month groups, amount ordering, PDF preview, native share sheet and native printer selection. A new expense entry was saved through the UI, and a read-only database check confirmed its selected shop ID and amount. Cancelling a no-match search retained the previous shop. The full shop list also rendered correctly at system font scale 2.0. All five year choices wrap, report buttons occupy their own rows, and group headings wrap without clipping.

The emulator font scale was restored to 1.0 and the disposable package was uninstalled after testing. The normal application was built but was not deployed or given test data. Windows visual/runtime checks and physical printer output remain unverified. No file was sent to another person or service during share-sheet testing.

Representative screenshots are saved in [verification/search-history-20261018](verification/search-history-20261018/), including the income chart, drilldown, report groups, complete shop list, native share sheet and enlarged-text selector.

The expanded reference regression harness links the actual history service, repositories, grouping/filter helpers and document generator. Tests include all four report modes/orders, exact nonconsecutive years, duplicate dates/amounts, company isolation, nested total reconciliation, selector lists exceeding 100 items, cancellation, invalid filters, export reference preservation, separate income/expense names, and large-report pagination. Previous reference/backup/restore tests run in the same suite.

PDF QA renders generated documents with Poppler and checks extracted text and page geometry. A long-name/1,000-character-note stress fixture produced a 53-page report with no text outside page margins after wrapping was corrected. The initial overflow was fixed before completion. Existing PDF font encoding supports the same Western character range as the current app's other reports; full Unicode font embedding is outside this change.
