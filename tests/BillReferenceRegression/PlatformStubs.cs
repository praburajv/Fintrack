// Only platform services are substituted. Repositories, SQL, deletion, grouping and PDF code are linked unchanged.
global using Microsoft.Maui.Controls;
global using Microsoft.Maui.Graphics;
global using FinTracker.Data;
global using FinTracker.Services;
global using FinTracker.Utilities;
global using FinTracker.Models;


namespace Microsoft.Maui.Graphics
{
    public sealed record Color(string Value)
    {
        public static Color FromArgb(string value) => new(value);
    }
    public static class Colors
    {
        public static Color Green => new("Green");
        public static Color Red => new("Red");
        public static Color Orange => new("Orange");
        public static Color Black => new("Black");
        public static Color Gray => new("Gray");
        public static Color Transparent => new("Transparent");
    }
}
namespace Microsoft.Maui.Controls
{
    public class Brush
    {
        public static implicit operator Brush(Color color) => new SolidColorBrush(color);
    }
    public sealed class SolidColorBrush(Color color) : Brush { public Color Color { get; } = color; }
    public sealed class Application
    {
        public static Application? Current => null;
        public Dictionary<string, object> Resources { get; } = [];
    }
    public interface IQueryAttributable { void ApplyQueryAttributes(IDictionary<string, object> query); }
    public sealed class Shell
    {
        public static Shell Current { get; } = new();
        public List<string> Routes { get; } = [];
        public Queue<string> Choices { get; } = new();
        public Queue<bool> Confirmations { get; } = new();
        public Action<string>? OnNavigate { get; set; }
        public Task GoToAsync(string route) { OnNavigate?.Invoke(route); Routes.Add(route); return Task.CompletedTask; }
        public Task<string> DisplayActionSheetAsync(string title, string cancel, string? destruction, params string[] choices)
            => Task.FromResult(Choices.TryDequeue(out var value) ? value : cancel);
        public Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
            => Task.FromResult(Confirmations.TryDequeue(out var value) && value);
        public Task DisplayAlertAsync(string title, string message, string cancel) => Task.CompletedTask;
    }
}

public static class FileSystem { public static string AppDataDirectory {get;set;} = ""; public static string CacheDirectory {get;set;} = ""; }
public sealed class AppInfo {public static AppInfo Current {get;}=new(); public string VersionString=>"reference-regression";}
namespace FinTracker.Services { public sealed class ModalErrorHandler {public List<Exception> Errors {get;}=[];public void HandleError(Exception e)=>Errors.Add(e);} }
namespace FinTracker.Utilities {public static class TestTasks { public static async void FireAndForgetSafeAsync(this Task task, FinTracker.Services.ModalErrorHandler errors){try{await task;}catch(Exception ex){errors.HandleError(ex);}}} }
public sealed class TestCurrency : ICurrencyService
{
    public IReadOnlyList<DisplayCurrency> AvailableCurrencies { get; } = [
        new("EUR", "Euro", "€", "pt-PT", false, true), new("USD", "Dollar", "$", "en-US", true, false),
        new("JPY", "Yen", "¥", "ja-JP", true, false)];
    public DisplayCurrency CurrentCurrency { get; private set; } = new("EUR", "Euro", "€", "pt-PT", false, true);
    public event EventHandler? CurrencyChanged;
    public void LoadSavedPreference() => ApplyAccountCurrency("EUR");
    public void ApplyCurrency(string currencyCode) => ApplyAccountCurrency(currencyCode);
    public void ApplyAccountCurrency(string currencyCode)
    {
        CurrentCurrency = new DisplayCurrency(currencyCode, currencyCode, currencyCode, "en-US", true, false);
        CurrencyChanged?.Invoke(this, EventArgs.Empty);
    }
    public void ClearAccountCurrency() => LoadSavedPreference();
}
