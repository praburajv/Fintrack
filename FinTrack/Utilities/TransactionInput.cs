namespace FinTracker.Utilities;

/// <summary>Field rules shared by manual entry, persistence and backup validation.</summary>
public static class TransactionInput
{
    public static bool IsReference(string? value) => value is null ||
        value.Length is >= 1 and <= 15 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '!' or '@' or '#' or '&' or '-' or '_');

    public static string? NormalizeReference(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim(' ');
        if (!IsReference(normalized))
            throw new ArgumentException("Bill Reference may contain only letters, numbers and ! @ # & - _, with a maximum of 15 characters.");
        return normalized;
    }

    public static string? ValidateManualNote(string? value, string? original = null)
    {
        if (value?.Length > 1000) return "Note cannot exceed 1000 characters.";
        // Existing bank descriptions and historical notes must survive unrelated edits.
        if (original is not null && string.Equals(value?.Trim(), original.Trim(), StringComparison.Ordinal)) return null;
        return value is null || value.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '\r' or '\n')
            ? null : "Note may contain only letters, numbers, spaces and line breaks.";
    }
}
