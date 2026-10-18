using FinTracker.PageModels;
using FinTracker.Utilities;
using System.Xml.Linq;

var passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"PASS: {description}"); passed++;
}

Check(NavigationRoutes.Leaf("//main/bill?type=Income") == "bill", "Leaf routes exclude query strings and ancestors");
foreach (var route in new[] { "welcome", "introduction", "login", "register", "passwordreset", "bill", "shop", "paymentmode", "category", "recurringbill" })
    Check(!NavigationRoutes.ShowShared(route), $"Shared navigation excluded on {route}");
Check(NavigationRoutes.SelectedItem("main") == "Home", "Existing main dashboard is Home");
Check(NavigationRoutes.SelectedItem("dashcool") == "More", "Dash cool stays a separate flyout destination");
var sampleDate = new DateTime(2026, 10, 9, 17, 30, 0);
var monthRange = FinancialDashboardDates.For(FinancialDashboardPeriod.Month, sampleDate);
Check(monthRange.Start == new DateTime(2026, 10, 1) && monthRange.EndExclusive == new DateTime(2026, 11, 1) && monthRange.PreviousStart == new DateTime(2026, 9, 1), "Month uses calendar boundaries and the previous month");
var threeMonthRange = FinancialDashboardDates.For(FinancialDashboardPeriod.LastThreeMonths, sampleDate);
Check(threeMonthRange.Start == new DateTime(2026, 8, 1) && threeMonthRange.EndExclusive == new DateTime(2026, 11, 1) && threeMonthRange.PreviousStart == new DateTime(2026, 5, 1), "Last 3 Months includes the current and two preceding calendar months");
var yearRange = FinancialDashboardDates.For(FinancialDashboardPeriod.ThisYear, sampleDate);
Check(yearRange.Start == new DateTime(2026, 1, 1) && yearRange.EndExclusive == new DateTime(2026, 10, 10) && yearRange.PreviousEndExclusive == new DateTime(2025, 10, 10), "This Year is year-to-date with a matching prior-year comparison");
Check(FinancialDashboardDates.Greeting(new DateTime(2026, 1, 1, 5, 0, 0)) == "Good morning" && FinancialDashboardDates.Greeting(new DateTime(2026, 1, 1, 12, 0, 0)) == "Good afternoon" && FinancialDashboardDates.Greeting(new DateTime(2026, 1, 1, 18, 0, 0)) == "Good night", "Greeting changes only at the agreed local-time boundaries");
Check(FinancialDashboardDates.Change(120, 100) == "↑ 20%" && FinancialDashboardDates.Change(80, 100) == "↓ 20%" && FinancialDashboardDates.Change(10, 0) == "No prior data", "Comparison never invents a percentage without prior data");
foreach (var route in new[] { "expensereport", "monthlybudget", "budgetreport", "incomeflow" })
    Check(NavigationRoutes.SelectedItem(route) == "Reports", $"Reports selection follows {route}");
Check(NavigationRoutes.SelectedItem("calendar") == "Calendar", "Calendar selected on calendar route");
Check(NavigationRoutes.SelectedItem("setup") == "More", "Setup is part of More");
Check(!WelcomeFlow.HasCompletedSwipe(79, 100) && WelcomeFlow.HasCompletedSwipe(80, 100), "Welcome slider completes only at 80 percent travel");
Check(!WelcomeFlow.HasCompletedSwipe(80, 0), "Welcome slider does not complete before layout gives it a width");
Check(WelcomeFlow.CompletionRoute(false, false) == "//login" && WelcomeFlow.CompletionRoute(true, true) == "..", "First launch goes to login and replay pops to the prior screen");
Check(WelcomeFlow.CompletionRoute(true, false) == "//login", "Expired replay session returns to login");
Check(WelcomeFlow.ResolveLanguage("pt-PT", "en-US") == "pt" && WelcomeFlow.ResolveLanguage("en", "pt-PT") == "en", "Saved Welcome language takes precedence over device language");
Check(WelcomeFlow.ResolveLanguage(null, "pt-BR") == "pt" && WelcomeFlow.ResolveLanguage(null, "ta-IN") == "en", "Portuguese device language and English fallback resolve correctly");
var draft = new GlobalDisplaySettingsPageModel();
Check(draft.Validate() is null, "Default display draft validates");
var original = draft.CreateSnapshot();
draft.PrimaryColor = "#BAD";
Check(draft.Validate() is not null, "Incomplete color rejected without applying it");
draft.PrimaryColor = "#GGGGGG";
Check(draft.Validate() is not null, "Non-hex color rejected");
draft.ResetDraftCommand.Execute(null);
Check(draft.CreateSnapshot() == original, "Reset restores draft defaults");
draft.CurrencyCode = "USD";
Check(draft.FormatAmount(1234.56m) == "USD 1,234.56", "Currency label formatting preserves amount");
draft.CurrencyFormat = "1.234,56";
Check(draft.FormatAmount(1234.56m) == "USD 1.234,56", "Decimal and grouping formatting is local to preview");
draft.DateFormat = "yyyy-MM-dd";
Check(draft.FormatDate(new DateTime(2026, 4, 25)) == "2026-04-25", "Preview date obeys selected format");
draft.Localization = "pt-PT"; draft.DateFormat = "dd MMM yyyy";
Check(draft.FormatDate(new DateTime(2026, 4, 25)).Contains("abr"), "Preview month uses selected locale");
draft.FontSize = double.NaN;
Check(draft.Validate() is not null, "Non-finite layout input rejected");
draft.ResetDraftCommand.Execute(null); draft.PreviewDraftCommand.Execute(null);
Check(draft.Status.Contains("connected later"), "Validation does not claim app-wide Apply");
Check(original.CurrencyCode == "EUR", "Captured draft snapshot remains immutable");

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var app = Path.Combine(root, "FinTrack");
XNamespace maui = "http://schemas.microsoft.com/dotnet/2021/maui";
var shell = XDocument.Load(Path.Combine(app, "AppShell.xaml"));
Check(shell.Descendants(maui + "ShellContent").Any(e => (string?)e.Attribute("Route") == "main" && ((string?)e.Attribute("ContentTemplate"))?.Contains("FinancialDashboardPage") == true), "Financial Dashboard is the main Shell root");
Check(shell.Descendants(maui + "ShellContent").Any(e => (string?)e.Attribute("Route") == "dashcool" && (string?)e.Attribute("Title") == "Dash cool" && ((string?)e.Attribute("ContentTemplate"))?.Contains("LandingDashboardPage") == true), "Former tile dashboard is Dash cool in the flyout");
var financialMarkup = XDocument.Load(Path.Combine(app, "Pages/FinancialDashboardPage.xaml"));
var periodPicker = financialMarkup.Descendants(maui + "Picker").Single(e => ((string?)e.Attribute("ItemsSource"))?.Contains("PeriodOptions") == true);
Check(((string?)periodPicker.Attribute("SelectedItem"))?.Contains("SelectedPeriod") == true && ((string?)periodPicker.Attribute("IsEnabled"))?.Contains("IsProcessing") == true, "Greeting uses the native period picker and preserves selection and busy-state bindings");
Check(!financialMarkup.ToString().Contains("SelectPeriodCommand") && !financialMarkup.ToString().Contains("IsPeriodMenuOpen"), "The custom period menu is removed");
Check(!financialMarkup.ToString().Contains("All Accounts") && financialMarkup.ToString().Contains("PreviousCompaniesCommand") && financialMarkup.ToString().Contains("NextCompaniesCommand") && financialMarkup.ToString().Contains("SelectFirstCompanyCommand"), "Company carousel replaces the old account-scope tabs");
Check(financialMarkup.Descendants(maui + "Entry").Any(e => ((string?)e.Attribute("Text"))?.Contains("Search.SearchText") == true), "Financial Dashboard reuses shop search state");
var greetingImage = financialMarkup.Descendants(maui + "Image").Single(e => (string?)e.Attribute("Source") == "financial_greeting_mountains_rounded.png");
var greetingCard = greetingImage.Ancestors(maui + "Border").First();
Check(greetingCard.Attribute("HeightRequest") is null && (string?)greetingCard.Attribute("MinimumHeightRequest") == "176"
    && (string?)greetingCard.Attribute("HorizontalOptions") == "Fill"
    && (string?)greetingCard.Attribute("Padding") == "0", "Greeting artwork shares the company-selector edges without inherited border padding and can grow beyond its minimum height");
Check(greetingImage.Parent?.Name == maui + "AbsoluteLayout" && (string?)greetingImage.Attribute("AbsoluteLayout.LayoutFlags") == "All", "Greeting background follows the measured text container without dictating its size");
var greetingRows = greetingCard.Descendants(maui + "Grid").Single(e => e.Attribute("RowDefinitions") is not null);
Check((string?)greetingRows.Attribute("RowDefinitions") == "Auto,Auto,Auto" && !greetingRows.Descendants().Any(e => e.Attribute("HeightRequest") is not null), "Greeting content and period selector use separate growing rows");
foreach (var dashboard in new[] { financialMarkup, XDocument.Load(Path.Combine(app, "Pages/LandingDashboardPage.xaml")) })
    Check(dashboard.Descendants(maui + "Entry").Any(e => (string?)e.Attribute("Placeholder") == "Search Shop or Ref"), "Dashboard uses the requested shop/reference search prompt");
var incomeMarkup = XDocument.Load(Path.Combine(app, "Pages/IncomeAnalysisPage.xaml"));
var categoryToggle = incomeMarkup.Descendants(maui + "TapGestureRecognizer").Single();
Check(((string?)categoryToggle.Attribute("Command"))?.Contains("ToggleCommand") == true
    && ((string?)categoryToggle.Attribute("Command"))?.Contains("IncomeAnalysisPageModel") == true
    && (string?)categoryToggle.Attribute("CommandParameter") == "{Binding .}", "Income category header connects to the owning model and passes the selected category");
var budgetMarkup = XDocument.Load(Path.Combine(app, "Pages/MonthlyBudgetReportPage.xaml"));
var budgetPeriod = budgetMarkup.Descendants().Single(e => e.Name.LocalName == "PeriodNavigationControl");
Check((string?)budgetPeriod.Attribute("PeriodText") == "{Binding MonthText}"
    && (string?)budgetPeriod.Attribute("PreviousCommand") == "{Binding PreviousMonthCommand}"
    && (string?)budgetPeriod.Attribute("NextCommand") == "{Binding NextMonthCommand}", "Budget month navigation reuses the bordered control with existing commands");
Check(financialMarkup.Descendants().Any(e => e.Name.LocalName == "ProcessingOverlay" && ((string?)e.Attribute("IsBusy"))?.Contains("IsProcessing") == true), "Dashboard processing uses the full-screen spinner overlay");
Check(financialMarkup.Descendants(maui + "Image").Any(e => (string?)e.Attribute("Source") == "financial_greeting_mountains_rounded.png"), "Greeting uses the rounded text-free mountain artwork");
Check(financialMarkup.Descendants(maui + "Grid").Any(e => (string?)e.Attribute("SizeChanged") == "OnGreetingArtworkSizeChanged"), "Greeting artwork clip updates with card size");
Check(!financialMarkup.ToString().Contains("Total Balance") && !financialMarkup.ToString().Contains("Currency Accounts"), "Excluded balance and currency cards are absent");
Check(File.Exists(Path.Combine(app, "Resources/Images/financial_greeting_mountains_rounded.png")) && File.Exists(Path.Combine(app, "Resources/Images/financial_greeting_mountains.png")), "Rounded Greeting background is packaged and the original is preserved");
Check(File.ReadAllText(Path.Combine(app, "Pages/FinancialDashboardPage.xaml.cs")).Contains("new RoundRectangleGeometry("), "Greeting image and overlay share a rounded clip");
Check(shell.Descendants(maui + "ShellContent").Count(e => (string?)e.Attribute("Route") == "login") == 1, "Exactly one hidden Login Shell root");
Check(shell.Descendants(maui + "ShellContent").Count(e => (string?)e.Attribute("Route") == "welcome") == 1, "First-launch Welcome remains a Shell root");
var startup = File.ReadAllText(Path.Combine(app, "FinTrackerProgram.cs"));
var backupServiceSource = File.ReadAllText(Path.Combine(app, "Services/BackupService.cs"));
Check(startup.Contains("AddSingleton<IBackupService, BackupService>()")
    && startup.Contains("AddSingleton<MonthlyBudgetRepository>()")
    && backupServiceSource.Contains("MonthlyBudgetRepository budgets"),
    "Backup and Restore routes can resolve the budget repository required by BackupService");
Check(!startup.Contains("WithShellRoute<LoginPage"), "Login is not also registered as a global pushed route");
Check(startup.Contains("Routing.RegisterRoute(\"introduction\", typeof(WelcomePage))"), "Replay introduction is a pushed route");
Check(!startup.Contains("GlobalDisplaySettingsPage"), "Draft settings page remains unwired");
var source = File.ReadAllText(Path.Combine(app, "PageModels/GlobalDisplaySettingsPageModel.cs"));
Check(!source.Contains("Preferences.") && !source.Contains("ApplyCurrency") && !source.Contains("ApplyAppearance"), "Draft has no global preference/service writes");
var shellSource = File.ReadAllText(Path.Combine(app, "AppShell.xaml.cs"));
Check(shellSource.Contains("? \"//companies\"") && shellSource.Contains("_accountContext.Clear()") && shell.Descendants(maui + "ShellContent").Any(e => (string?)e.Attribute("Route") == "companies"), "Restored sessions return to Company Cards after a fresh app launch");
Check(shellSource.Contains("dashboard.ToggleSearch()") && shellSource.Contains("page is LandingDashboardPage or FinancialDashboardPage"), "Header search is available on both dashboards");
var introductionHandler = shellSource.Split("private async void OnShowIntroductionClicked", 2)[1].Split("private async void OnLogoutClicked", 2)[0];
Check(!introductionHandler.Contains("_authentication.Logout()") && introductionHandler.Contains("[\"entry\"] = \"replay\""), "Show introduction preserves authentication and passes replay entry");
var welcomeMarkup = XDocument.Load(Path.Combine(app, "Pages/WelcomePage.xaml"));
Check(welcomeMarkup.Descendants(maui + "Image").Any(e => (string?)e.Attribute("Source") == "fintracker_en__w_s_no_slider_n1.png"), "Welcome starts with the button-free English artwork");
Check(welcomeMarkup.Descendants(maui + "PanGestureRecognizer").Count() == 1 && welcomeMarkup.Descendants(maui + "Button").Count() >= 2, "Welcome has an interactive swipe and visible tap alternative");
Check(welcomeMarkup.Descendants(maui + "Grid").Any(e => (string?)e.Attribute("SafeAreaEdges") == "Container"), "Welcome controls respect the bottom safe area");
var welcomeSource = File.ReadAllText(Path.Combine(app, "Pages/WelcomePage.xaml.cs"));
Check(welcomeSource.Contains("Aspect.AspectFit") && welcomeSource.Contains("height / width < 2.0"), "Short-screen Welcome shows the complete artwork");
Check(File.Exists(Path.Combine(app, "Resources/Images/fintracker_pt__w_s_no_slider_n1.png")), "Portuguese Welcome artwork is packaged");
var reportsHub = File.ReadAllText(Path.Combine(app, "Pages/ReportsHubPage.cs"));
Check(reportsHub.Contains("monthlybudget?reference=") && !reportsHub.Contains("budgetreport?reference="), "Budget reports go through existing report builder, not an empty preview");
Check(shell.Descendants(maui + "ShellContent").Any(e => (string?)e.Attribute("Route") == "reportshub") && shell.Descendants(maui + "ShellContent").Any(e => (string?)e.Attribute("Route") == "addhub"), "Reports and Add hubs are shared navigation destinations");
var billSource = File.ReadAllText(Path.Combine(app, "PageModels/BillEntryPageModel.cs"));
Check(billSource.Contains("TransactionType = query.TryGetValue(\"type\""), "Add initializes new Income/Expense from the route");
Check(billSource.Contains("await _income.SaveAsync") && billSource.Contains("await _bills.SaveItemAsync"), "Income and Expense retain separate repositories");
var chrome = File.ReadAllText(Path.Combine(app, "Platforms/Android/AndroidTopBarTheme.cs"));
Check(chrome.Contains("TryGetValue(\"PrimaryDark\"") && chrome.Contains("view is AppBarLayout"), "Native status backing uses the same PrimaryDark resource as the toolbar");
Check(chrome.Contains("AppearanceLightStatusBars = false"), "Current dark top-bar palettes retain readable white status icons");
Check(!chrome.Contains("SetPadding(") && !chrome.Contains("SetOnApplyWindowInsetsListener(") && !chrome.Contains("SetDecorFitsSystemWindows("), "Top-bar color correction preserves existing insets and layout");
Check(chrome.Contains("ThemeChanged -=") && chrome.Contains("GlobalLayout -=") && chrome.Contains("RequestedThemeChanged -="), "Native theme subscriptions are cleaned up with the activity");
Check(chrome.Contains("ConditionalWeakTable<AppBarLayout, AppliedColor>") && chrome.Contains("applied.Color = color;"), "Material background wrappers cannot cause repainting on every layout");
var activity = File.ReadAllText(Path.Combine(app, "Platforms/Android/MainActivity.cs"));
Check(activity.Contains("AndroidBackupFileExporter.Complete"), "Native backup activity result handling remains intact");
Check(shellSource.Contains("_initializingThemeSelection || e.OldIndex < 0") && shellSource.Contains("e.OldIndex == e.NewIndex"), "Initial and unchanged flyout selections do not overwrite saved appearance");
Check(NavigationRoutes.SelectedItem("history") == "Reports" && NavigationRoutes.ShowShared("history"), "History keeps Reports selected in shared navigation");
Console.WriteLine($"{passed} checks passed.");
