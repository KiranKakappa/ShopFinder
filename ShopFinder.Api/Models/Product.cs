namespace ShopFinder.Api.Models;

public class Product
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop? Shop { get; set; }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Regular price in USD.</summary>
    public decimal Price { get; set; }

    /// <summary>0-90. 0 means no discount.</summary>
    public int DiscountPercent { get; set; }

    /// <summary>When the discount ends (UTC). Null = runs until the owner removes it.</summary>
    public DateTime? DealEndsUtc { get; set; }

    public bool InStock { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public bool IsDealActive(DateTime nowUtc) =>
        DiscountPercent > 0 && (DealEndsUtc is null || DealEndsUtc > nowUtc);

    public decimal FinalPrice(DateTime nowUtc) =>
        IsDealActive(nowUtc)
            ? Math.Round(Price * (100 - DiscountPercent) / 100m, 2, MidpointRounding.AwayFromZero)
            : Price;
}
