using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinTracker.Services;
using Microsoft.Maui.Handlers;

var passed = 0;
Application.Current = new Application();
InterfaceTypography.Configure(); InterfaceTypography.Configure();
Check(LabelHandler.Mapper.Actions.Count == 2 && ButtonHandler.Mapper.Actions.Count == 2, "Handler registration is idempotent");
var body = new Label { Text = "Body text", FontSize = 17 };
LabelHandler.Mapper.Create(body);
Check(body.FontFamily is null, "Font is not changed before its parent hierarchy is loaded");
body.Load();
Check(body.FontFamily == InterfaceTypography.Regular && body.FontSize == 17 && body.FontAutoScalingEnabled, "Body uses Regular with unchanged size and OS scaling");
var button = new Button { FontAttributes = FontAttributes.Bold, FontSize = 18 };
ButtonHandler.Mapper.Create(button); button.Load();
Check(button.FontFamily == InterfaceTypography.Medium && button.FontAttributes == FontAttributes.Bold && InterfaceTypography.WeightForAlias(button.FontFamily) == 500, "Buttons use Medium while preserving formatting state");
var heading = new Label { FontAttributes = FontAttributes.Bold };
LabelHandler.Mapper.Create(heading); heading.Load();
Check(heading.FontFamily == InterfaceTypography.Semibold && heading.FontAttributes == FontAttributes.Bold && InterfaceTypography.WeightForAlias(heading.FontFamily) == 600, "Bold headings use Semibold while preserving formatting state");
var titleStyle = new Style(); Application.Current.Resources["Title1"] = titleStyle;
var windowsTitle = new Label { Style = titleStyle };
InterfaceTypography.Attach(windowsTitle); windowsTitle.Load();
Check(windowsTitle.FontFamily == InterfaceTypography.Semibold, "Windows title style uses Semibold even without a Bold attribute");
var inheritedTitle = new Label { Style = new Style { BasedOn = titleStyle } };
InterfaceTypography.Attach(inheritedTitle); inheritedTitle.Load();
Check(inheritedTitle.FontFamily == InterfaceTypography.Semibold, "Heading styles inherited through BasedOn retain their weight");
var italic = new Label { FontAttributes = FontAttributes.Italic };
InterfaceTypography.Attach(italic); italic.Load();
Check(italic.FontFamily == InterfaceTypography.Regular && italic.FontAttributes == FontAttributes.Italic, "Italic emphasis is retained");
body.FontAttributes = FontAttributes.Bold;
Check(body.FontFamily == InterfaceTypography.Semibold, "Later emphasis changes update the font face");
body.FontAttributes = FontAttributes.None;
Check(body.FontFamily == InterfaceTypography.Regular, "Removing emphasis restores the body face");
foreach (var input in new TextView[] { new Entry(), new Editor(), new Picker(), new DatePicker(), new TimePicker(), new SearchBar() })
{
    InterfaceTypography.Attach(input); input.Load();
    Check(input.FontFamily == InterfaceTypography.Regular && input.FontAutoScalingEnabled, $"{input.GetType().Name} uses Regular with scaling");
}
var radio = new RadioButton(); InterfaceTypography.Attach(radio); radio.Load();
Check(radio.FontFamily == InterfaceTypography.Medium, "Radio-button action labels use Medium");
var truncated = new Label { Text = "Long company and amount", MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
InterfaceTypography.Attach(truncated); truncated.Load();
Check(truncated.MaxLines == -1 && truncated.LineBreakMode == LineBreakMode.WordWrap, "App-owned truncated text can wrap instead of shrinking");
foreach (var chart in new View[] { new CategoryExpenseChart(), new Syncfusion.Maui.Toolkit.Charts.PlotSurface() })
{
    var label = new Label { Text = "Chart label", Parent = new View { Parent = chart }, FontFamily = "OpenSansRegular", FontSize = 12, FontAttributes = FontAttributes.Bold, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation, FontAutoScalingEnabled = false };
    InterfaceTypography.Attach(label); label.Load();
    Check(label.FontFamily == "OpenSansRegular" && label.FontSize == 12 && label.FontAttributes == FontAttributes.Bold && label.MaxLines == 1 && label.LineBreakMode == LineBreakMode.TailTruncation && !label.FontAutoScalingEnabled, "Chart subtree keeps every original typography/layout value");
}
var reparented = new Label { FontFamily = "OpenSansRegular", MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
InterfaceTypography.Attach(reparented); reparented.Load(); reparented.Parent = new CategoryExpenseChart();
Check(reparented.FontFamily == "OpenSansRegular" && reparented.MaxLines == 1 && reparented.LineBreakMode == LineBreakMode.TailTruncation, "Moving a text control into a chart restores its original values");
var glyph = new Label { FontFamily = "FluentUI", Text = "\uF101" };
InterfaceTypography.Attach(glyph); glyph.Load();
Check(glyph.FontFamily == "FluentUI", "Fluent icon fonts remain intact");
var privateGlyph = new Label { Text = "\uE101" };
InterfaceTypography.Attach(privateGlyph); privateGlyph.Load();
Check(privateGlyph.FontFamily is null, "Private-use glyph labels are excluded");
var specialist = new Label { FontFamily = "monospace" };
InterfaceTypography.Attach(specialist); specialist.Load();
Check(specialist.FontFamily == "monospace", "Deliberate specialist fonts remain intact");
var preview = new Label { Parent = new GlobalDisplaySettingsPage(), FontFamily = "OpenSansRegular" };
InterfaceTypography.Attach(preview); preview.Load();
Check(preview.FontFamily == "OpenSansRegular", "Independent font-settings draft preview is preserved");

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
foreach (var (weight, expected) in new[] { ("Regular", 400), ("Medium", 500), ("SemiBold", 600) })
{
    var bytes = File.ReadAllBytes(Path.Combine(root, $"FinTrack/Resources/Fonts/Inter-{weight}.ttf"));
    var count = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4, 2));
    var actual = -1;
    for (var i = 0; i < count; i++)
    {
        var record = 12 + i * 16;
        if (Encoding.ASCII.GetString(bytes, record, 4) != "OS/2") continue;
        var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(record + 8, 4));
        actual = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 4, 2));
    }
    Check(actual == expected, $"Bundled Inter {weight} file contains actual weight {expected}");
}
var baseline = JsonSerializer.Deserialize<SourceHash[]>(File.ReadAllText(Path.Combine(root, "docs/rollback/typography-20261016/source-baseline.json")))!;
var protectedFiles = baseline.Where(x => x.Path.Contains("Chart", StringComparison.OrdinalIgnoreCase) || x.Path.StartsWith("FinTrack\\Resources\\Styles\\", StringComparison.OrdinalIgnoreCase) || x.Path.StartsWith("FinTrack/Resources/Styles/", StringComparison.OrdinalIgnoreCase)).ToArray();
Check(protectedFiles.Length > 10 && protectedFiles.All(x => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, x.Path)))) == x.Sha256), "All chart files and global style dictionaries are byte-for-byte unchanged");
Check(File.ReadAllText(Path.Combine(root, "FinTrack/Resources/Raw/inter-license.txt")).Contains("SIL OPEN FONT LICENSE"), "Inter licence is bundled with the application");
Console.WriteLine($"{passed} typography checks passed.");

void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); passed++; Console.WriteLine("PASS: " + message); }
sealed record SourceHash(string Path, string Sha256);
