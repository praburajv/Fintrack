namespace FinTracker.Models;

public sealed record CategoryIconOption(string DisplayName, string IconKey, string IconFileName)
{
    public override string ToString() => DisplayName;
}

public static class CategoryIconCatalog
{
    public static IReadOnlyList<CategoryIconOption> All { get; } =
    [
        new("Default Shop", "default_shop", "category_default_shop.svg"),
        new("Shopping Cart", "shopping_cart", "category_shopping_cart.svg"),
        new("Travel", "travel", "category_travel.svg"),
        new("Internet", "internet", "category_internet.svg"),
        new("Fuel", "fuel", "category_fuel.svg"),
        new("Gas", "gas", "category_gas.svg"),
        new("Telephone", "telephone", "category_telephone.svg"),
        new("Stationery", "stationery", "category_stationery.svg"),
        new("Electricity", "electricity", "category_electricity.svg"),
        new("Bank", "bank", "category_bank.svg"),
        new("Home", "home", "category_home.svg"),
        new("Online Shopping", "online_shopping", "category_online_shopping.svg"),
        new("Restaurant", "restaurant", "category_restaurant.svg"),
        new("Medical", "medical", "category_medical.svg"),
        new("Receipt", "receipt", "category_receipt.svg"),
        new("Transport", "transport", "category_transport.svg"),
        new("Rent / Mortgage", "rent_mortgage", "category_rent_mortgage.svg"),
        new("Water", "water", "category_water.svg"),
        new("TV / Streaming", "tv_streaming", "category_tv_streaming.svg"),
        new("Vehicle", "vehicle", "category_vehicle.svg"),
        new("Car Workshop", "car_workshop", "category_car_workshop.svg"),
        new("Bike Workshop", "bike_workshop", "category_bike_workshop.svg"),
        new("Pharmacy", "pharmacy", "category_pharmacy.svg"),
        new("Insurance", "insurance", "category_insurance.svg"),
        new("Clothing", "clothing", "category_clothing.svg"),
        new("Personal Care", "personal_care", "category_personal_care.svg"),
        new("Barber / Hairdresser", "barber", "category_barber.svg"),
        new("Gym / Fitness", "gym", "category_gym.svg"),
        new("Education", "education", "category_education.svg"),
        new("Children", "children", "category_children.svg"),
        new("Pets", "pets", "category_pets.svg"),
        new("Entertainment", "entertainment", "category_entertainment.svg"),
        new("Subscriptions", "subscriptions", "category_subscriptions.svg"),
        new("Gifts", "gifts", "category_gifts.svg"),
        new("Taxes / Government", "taxes_government", "category_taxes_government.svg"),
        new("Home Maintenance", "home_maintenance", "category_home_maintenance.svg"),
        new("Electronics", "electronics", "category_electronics.svg")
    ];

    public static string FileNameFor(string? iconKey) => All.FirstOrDefault(x => string.Equals(x.IconKey, iconKey, StringComparison.OrdinalIgnoreCase))?.IconFileName ?? "category_default_shop.svg";
    public static string DisplayNameFor(string? iconKey) => All.FirstOrDefault(x => string.Equals(x.IconKey, iconKey, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? "Default Shop";
    public static CategoryIconOption Find(string? iconKey) => All.FirstOrDefault(x => string.Equals(x.IconKey, iconKey, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
