namespace MauiApp1.Models;

/// <summary>An explicit choice in the monthly deletion review, never a database command.</summary>
public sealed record MonthlyDeletionChoice(string Key, string Title, string Description = "", bool IsDestructive = false);
