# Phase 12 corrective regression checks

Run from the repository root:

```powershell
dotnet run --project tests/Phase12Regression/Phase12Regression.csproj
```

The console harness links the actual repositories, PDF bank importer, additive schema migration, transactional deletion service, monthly-maintenance/financial-list/bill-list view models, ordering helpers, statement builder and PDF writer. Only MAUI platform surfaces (including Shell navigation/dialogs) are replaced by small stubs. Each run uses a fresh OS-temporary SQLite database and cache, not application/user data.

The 33 checks cover debit-only, credit-only and mixed deletion; month/import isolation; manual and legacy Income protection/review; rollback and stale confirmations; Income update ID/provenance; year boundaries; shop-range drilldown; sort identity; category/shop ordering; generated PDF content; new and older full/selected-month backup compatibility; and idempotent Bank Trnsfer payment-mode creation/reuse and ETL Credit wiring.

The importer tests generate real coordinate-based bank PDFs and pass them through PdfPig and the production importer. Cases include the supplied September Credit example, Debit with Income-like description, blank/zero Debit and Credit with a populated Balance, negative balances, missing balances, missing SALDO headers, comma decimals, space-grouped thousands, numeric descriptions, both monetary columns populated (existing Credit precedence), repeated legitimate source rows and duplicate reimports. Monthly command tests cover the correct absolute Shell route, passed year/month, screen locking, awaited selected-month loading (including a concurrent refresh), cancellation, confirmed deletion, count refresh and manual-Income protection. XAML checks verify parent-model command sources, shared button sizing and whole-page overlay coverage.

These checks do not replace device testing of taps, chart rendering, Edit/Back navigation or native PDF viewing/sharing.

The explicit monthly review-panel tests exercise the production view model's actual choice commands: 70 imported Income rows with zero expenses, Expense-only confirmation, one-import selection, individual older-Credit approval, protected manual Income, cancellation at scope/import/final stages, leaving during preparation, stale/foreign choices, repeated taps, busy year-navigation guards and a real SQLite failure after an expense deletion (atomic rollback and released UI/operation locks). XAML checks cover the reusable review panel, explicit choice/Cancel buttons, final Delete pinned outside scrolling details, application icon, full-page input blocking, full-width month details and normalized progress shown only during confirmed deletion. Native action-sheet stubs are not used to approve monthly deletion.
