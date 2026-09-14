namespace MauiApp1.Models;

public enum ShopCategory { DefaultShop, ShoppingCart, Travel, Internet, Fuel, Gas, Telephone, Stationery, Electricity, Bank, Home, OnlineShopping, Restaurant, Medical, Receipt, Transport }

public sealed record ShopCategoryOption(ShopCategory Value, string DisplayName, string IconFileName)
{
    public override string ToString() => DisplayName;
}

public static class ShopCategoryCatalog
{
    public static IReadOnlyList<ShopCategoryOption> All { get; } =
    [
        new(ShopCategory.DefaultShop, "Default Shop", "category_default_shop.svg"), new(ShopCategory.ShoppingCart, "Shopping Cart", "category_shopping_cart.svg"), new(ShopCategory.Travel, "Travel", "category_travel.svg"), new(ShopCategory.Internet, "Internet", "category_internet.svg"), new(ShopCategory.Fuel, "Fuel", "category_fuel.svg"), new(ShopCategory.Gas, "Gas", "category_gas.svg"), new(ShopCategory.Telephone, "Telephone", "category_telephone.svg"), new(ShopCategory.Stationery, "Stationery", "category_stationery.svg"), new(ShopCategory.Electricity, "Electricity", "category_electricity.svg"), new(ShopCategory.Bank, "Bank", "category_bank.svg"), new(ShopCategory.Home, "Home", "category_home.svg"), new(ShopCategory.OnlineShopping, "Online Shopping", "category_online_shopping.svg"), new(ShopCategory.Restaurant, "Restaurant", "category_restaurant.svg"), new(ShopCategory.Medical, "Medical", "category_medical.svg"), new(ShopCategory.Receipt, "Receipt", "category_receipt.svg"), new(ShopCategory.Transport, "Transport", "category_transport.svg")
    ];
    public static ShopCategory Normalize(string? value) => Enum.TryParse<ShopCategory>(value, true, out var category) && All.Any(x => x.Value == category) ? category : ShopCategory.DefaultShop;
    public static string IconFor(ShopCategory category) => All.FirstOrDefault(x => x.Value == category)?.IconFileName ?? "category_default_shop.svg";
    public static ShopCategory FromIconKey(string? key) => key?.ToLowerInvariant() switch
    {
        "shopping_cart" => ShopCategory.ShoppingCart, "travel" => ShopCategory.Travel, "internet" => ShopCategory.Internet,
        "fuel" => ShopCategory.Fuel, "gas" => ShopCategory.Gas, "telephone" => ShopCategory.Telephone,
        "stationery" => ShopCategory.Stationery, "electricity" => ShopCategory.Electricity, "bank" => ShopCategory.Bank,
        "home" => ShopCategory.Home, "online_shopping" => ShopCategory.OnlineShopping, "restaurant" => ShopCategory.Restaurant,
        "medical" => ShopCategory.Medical, "receipt" => ShopCategory.Receipt, "transport" => ShopCategory.Transport,
        _ => ShopCategory.DefaultShop
    };
}
