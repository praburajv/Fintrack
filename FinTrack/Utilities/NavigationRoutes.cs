namespace FinTracker.Utilities;

/// <summary>Route classification is independent of page lifetime and UI bindings.</summary>
public static class NavigationRoutes
{
    public static string Leaf(string? location) => (location ?? string.Empty).Split('?', 2)[0].Trim('/').Split('/').LastOrDefault() ?? string.Empty;
    public static bool IsAuthentication(string route) => route is "welcome" or "introduction" or "login" or "register" or "passwordreset" or "companies" or "companymanagement";
    public static bool ShowShared(string route) => !IsAuthentication(route) && route is not ("bill" or "shop" or "category" or "paymentmode" or "recurringbill" or "globaldisplaysettings");
    public static string SelectedItem(string route) => route switch
    {
        "main" => "Home",
        "calendar" or "monthlycalendar" or "dailybills" => "Calendar",
        "reportshub" or "history" or "expensereport" or "expensestatement" or "monthlybudget" or "budgetreport" or "incomeflow" or "expenseanalysis" or "incomeanalysis" or "dashboard" => "Reports",
        "addhub" => "Add",
        _ => "More"
    };
}
