namespace MauiApp1.Models;

public class Shop
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public override string ToString() => Name;
}
