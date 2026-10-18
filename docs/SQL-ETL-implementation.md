# SQL table structure and ETL import changes

## SQL Query Analyzer

The Shop Schema and Bill Schema buttons are replaced by one selector. It discovers all actual tables in the current SQLite database, including SQLite-maintained tables, and displays entries such as `PRAGMA table_info(Bill_transcation)` and `PRAGMA table_info(Shop)`.

Selection fills the existing editor; Execute Query displays the structure. Names requiring quoting are escaped safely. Table names refresh whenever the analyzer appears, including after database restore. The existing query builder, Tables/Counts shortcuts and read-only database connection are retained.

## ETL processing

After PDF selection, the existing overlay blocks the page and bottom task bar and displays live stage messages. The page model owns UI updates and dialogs; PDF reading, parsing, duplicate lookup and inserts execute on a worker with their own resources. Optional ETL setup also runs off the UI thread. Queued progress cannot overwrite the final status.

A shared import gate serializes imports, including calls through different importer instances. Critical-operation leases continue protecting company switching/navigation during processing. Cancellation and failures dispose PDF/database resources and release the gate and busy state.

Duplicate identity is active company + transaction type + calendar date + two-decimal amount + description with collapsed whitespace and case ignored. Only the PDF's relevant date range and identity columns are read from existing transactions. Date bounds support ISO timestamps, legacy space-separated timestamps and date-only values.

Existing matches are skipped. Each successful insert adds its identity to the lookup, so subsequent identical rows in the same PDF are also skipped. Failed saves remain errors and are not counted as duplicates. Existing records, references, notes and provenance remain untouched. Shop remapping or payment-mode changes do not make an existing transaction new.

This implements the approved strict rule: genuinely separate source rows with identical date, amount, type and normalized description also collapse to a single inserted transaction. Existing repeated database rows are not removed. Similar but different descriptions are not fuzzily matched.

The existing per-row failure policy is retained: successful rows remain saved when another row fails. Reimport safely skips already saved matches. Debit/credit column parsing, localized amounts, mappings, selected payment modes and the final import summary are preserved.

Tests exposed an existing initialization defect where the bill repository could create ETL_TEXT_MAP without FinancialAccountId. New tables now include the column; older affected mapping tables recover ownership from their referenced shops rather than assigning mappings to the current company.

## Automated verification

```powershell
dotnet run --project tests/SqlEtlRegression/SqlEtlRegression.csproj
dotnet run --project tests/BillReferenceRegression/BillReferenceRegression.csproj
dotnet run --project tests/FinancialAccountRegression/FinancialAccountRegression.csproj
dotnet run --project tests/SharedNavigationRegression/SharedNavigationRegression.csproj
dotnet build FinTrack/FinTracker.csproj -f net10.0-android --no-restore
dotnet build FinTrack/FinTracker.csproj -f net10.0-windows10.0.19041.0 --no-restore
```

The SQL/ETL harness links the real production services/repositories/page models and imports generated multi-page PDFs into an isolated temporary database. Platform/UI infrastructure is substituted. Its 58 checks include schema selection/execution, unusual table names, read-only protection, schema refresh, mapping repair, same-PDF/reimport/overlapping duplicates, legacy dates, company isolation, concurrent imports, cancellation, failed saves, 300-row PDF processing, UI-thread progress, picker cancellation and invalid-PDF recovery.

The 84 reference, 23 account and 79 navigation checks also passed. Android and Windows builds completed with zero errors and existing dependency warnings. Automated UI synchronization checks do not substitute for visually verifying animation on a device.

An Android emulator launch was verified using a debug APK built with `-p:EmbedAssembliesIntoApk=true` (the normal debug package requires Fast Deployment assembly transfer). Installation retained existing application data. The app reached Login and remained running; no credentials were supplied, accounts reset or test transactions inserted into the emulator database. Full SQL/ETL screen interaction on the emulator remains unverified.
