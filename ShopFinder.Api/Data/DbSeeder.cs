using ShopFinder.Api.Models;
using ShopFinder.Api.Services;

namespace ShopFinder.Api.Data;

/// <summary>
/// Creates a handful of demo shops around App:SeedCenterLatitude/Longitude the first time the app runs.
/// Every demo shop logs in with its phone number and the password "demo1234".
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "demo1234";

    public static void Seed(AppDbContext db, IConfiguration config, Clock clock)
    {
        if (db.Shops.Any()) return;

        var lat0 = config.GetValue("App:SeedCenterLatitude", 40.7306);
        var lng0 = config.GetValue("App:SeedCenterLongitude", -73.9866);
        var hash = Passwords.Hash(DemoPassword);
        var now = clock.UtcNow;

        Shop NewShop(string name, string category, string desc, string address, string phone,
            double dLat, double dLng, string open, string close, string days) => new()
        {
            Name = name, Category = category, Description = desc, Address = address, Phone = phone,
            Latitude = lat0 + dLat, Longitude = lng0 + dLng,
            OpenTime = TimeOnly.Parse(open), CloseTime = TimeOnly.Parse(close), OpenDays = days,
            PasswordHash = hash
        };

        Product P(string name, decimal price, int discount = 0, int? endsInHours = null, bool inStock = true, string desc = "") => new()
        {
            Name = name, Price = price, DiscountPercent = discount, InStock = inStock, Description = desc,
            DealEndsUtc = endsInHours is null ? null : now.AddHours(endsInHours.Value)
        };

        var shops = new List<Shop>
        {
            NewShop("Green Basket Grocery", "Grocery", "Fresh produce, dairy and daily essentials.",
                "12 Market Street", "5550101", 0.004, -0.003, "07:00", "22:00", "0,1,2,3,4,5,6"),
            NewShop("Sunrise Bakery", "Bakery", "Bread baked every morning. End-of-day discounts after 6 PM.",
                "48 Baker Lane", "5550102", -0.006, 0.005, "06:00", "19:00", "1,2,3,4,5,6"),
            NewShop("CityCare Pharmacy", "Pharmacy", "Prescriptions, first aid and health products. Open 24 hours.",
                "3 Health Avenue", "5550103", 0.009, 0.008, "00:00", "00:00", "0,1,2,3,4,5,6"),
            NewShop("Volt Electronics", "Electronics", "Phones, chargers, cables and repairs.",
                "77 Circuit Road", "5550104", -0.011, -0.009, "10:00", "20:00", "1,2,3,4,5,6"),
            NewShop("Thread & Co.", "Clothing", "Everyday clothing for the whole family.",
                "21 Fashion Row", "5550105", 0.013, -0.012, "11:00", "21:00", "0,1,2,3,4,5,6"),
            NewShop("Night Owl Diner", "Restaurant", "Late-night meals and takeaway.",
                "9 Moon Street", "5550106", -0.002, 0.014, "18:00", "03:00", "3,4,5,6,0"),
            NewShop("FixIt Hardware", "Hardware", "Tools, paint and home repair supplies.",
                "150 Builder Avenue", "5550107", 0.017, 0.003, "08:00", "18:00", "1,2,3,4,5"),
            NewShop("Page Turner Books", "Books", "New and used books, stationery.",
                "5 Library Square", "5550108", -0.015, 0.002, "09:30", "20:30", "1,2,3,4,5,6"),
        };

        shops[0].Products.AddRange(new[]
        {
            P("Whole milk, 1 gallon", 4.29m, 10, 48), P("Bananas, per lb", 0.69m),
            P("Free-range eggs, dozen", 5.49m, 15, 24), P("Basmati rice, 5 lb", 8.99m),
            P("Avocados, bag of 4", 5.99m, 25, 6), P("Orange juice, 52 oz", 4.79m, inStock: false)
        });
        shops[1].Products.AddRange(new[]
        {
            P("Sourdough loaf", 6.50m, 30, 10, desc: "Baked this morning"), P("Croissant", 2.75m),
            P("Blueberry muffins, 4-pack", 7.00m, 20), P("Birthday cake (8\")", 32.00m)
        });
        shops[2].Products.AddRange(new[]
        {
            P("Vitamin C, 100 tablets", 12.99m, 20, 72), P("Hand sanitizer, 8 oz", 3.49m),
            P("Digital thermometer", 14.99m), P("First aid kit", 24.99m, 15)
        });
        shops[3].Products.AddRange(new[]
        {
            P("USB-C fast charger 30W", 24.99m, 35, 36), P("Wireless earbuds", 59.99m, 20),
            P("Phone screen protector", 9.99m), P("Power bank 10,000 mAh", 29.99m, 25, 12),
            P("HDMI cable, 6 ft", 11.99m)
        });
        shops[4].Products.AddRange(new[]
        {
            P("Cotton T-shirt", 14.99m, 40), P("Denim jeans", 49.99m, 20, 96),
            P("Rain jacket", 69.99m), P("Wool socks, 3-pack", 12.99m, 10)
        });
        shops[5].Products.AddRange(new[]
        {
            P("Classic burger & fries", 13.50m, 15), P("Late-night pancake stack", 9.00m),
            P("Chicken wrap", 10.50m)
        });
        shops[6].Products.AddRange(new[]
        {
            P("Cordless drill", 89.99m, 20, 120), P("Interior paint, 1 gal", 34.99m),
            P("LED bulbs, 4-pack", 11.99m, 30), P("Duct tape", 6.49m)
        });
        shops[7].Products.AddRange(new[]
        {
            P("Hardcover notebook", 8.99m, 25), P("Used paperbacks (any)", 3.00m),
            P("Fountain pen", 19.99m)
        });

        db.Shops.AddRange(shops);
        db.SaveChanges();
    }
}
