// Only platform services are substituted. Repositories, SQL, deletion, grouping and PDF code are linked unchanged.
global using Microsoft.Maui.Controls;
global using Microsoft.Maui.Graphics;
global using MauiApp1.Data;
global using MauiApp1.Services;
global using MauiApp1.Utilities;
global using Phase12Regression;

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
namespace MauiApp1.Data
{
    public static class Constants
    {
        public const string DatabaseFilename = "test.db";
        public static string DatabasePath { get; set; } = string.Empty;
    }
}
namespace MauiApp1.Services
{
    public sealed class ModalErrorHandler
    {
        public void HandleError(Exception error) => throw new InvalidOperationException("Page model failed", error);
    }
}
namespace MauiApp1
{
    public static class AppShell { public static bool IsClosing => false; }
}
namespace Phase12Regression
{
    public sealed class FileResult(string path)
    {
        public string FileName => Path.GetFileName(path);
        public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(File.OpenRead(path));
    }
    public static class FileSystem
    {
        // An isolated directory is assigned by the test program, never the application's cache.
        public static string CacheDirectory { get; set; } = string.Empty;
        public static string AppDataDirectory { get; set; } = string.Empty;
    }
    public sealed class AppInfo
    {
        public static AppInfo Current { get; } = new();
        public string VersionString => "phase12-regression";
    }
}
