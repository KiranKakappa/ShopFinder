using Microsoft.EntityFrameworkCore;
using ShopFinder.Api.Data;
using ShopFinder.Api.Models;
using ShopFinder.Api.Services;

namespace ShopFinder.Api.Endpoints;

/// <summary>Endpoints for customers. No login needed.</summary>
public static class PublicEndpoints
{
    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Customers");

        api.MapGet("/categories", () => ShopMapper.Categories);

        // List shops. ?q=milk searches shop names, descriptions and product names.
        api.MapGet("/shops", async (AppDbContext db, Clock clock,
            string? q, string? category, bool? openNow, double? lat, double? lng) =>
        {
            var shops = await db.Shops.AsNoTracking().Include(s => s.Products).ToListAsync();

            var result = shops
                .Where(s => MatchesCategory(s, category) && MatchesQuery(s, q))
                .Select(s => s.ToSummary(clock, lat, lng))
                .Where(s => openNow != true || s.IsOpen);

            result = lat is not null && lng is not null
                ? result.OrderBy(s => s.DistanceKm)
                : result.OrderByDescending(s => s.IsOpen).ThenBy(s => s.Name);

            return result.ToList();
        });

        api.MapGet("/shops/{id:int}", async (int id, AppDbContext db, Clock clock) =>
        {
            var shop = await db.Shops.AsNoTracking().Include(s => s.Products)
                .FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new ApiError(404, "Shop not found.");
            return shop.ToDetail(clock);
        });

        // A customer arrived and found the shop closed although the app said open.
        api.MapPost("/shops/{id:int}/report-closed", async (int id, AppDbContext db, Clock clock) =>
        {
            var shop = await db.Shops.FindAsync(id) ?? throw new ApiError(404, "Shop not found.");
            var today = clock.LocalToday;
            if (shop.ClosedReportsDate != today)
            {
                shop.ClosedReportsDate = today;
                shop.ClosedReportsCount = 0;
            }
            shop.ClosedReportsCount++;
            await db.SaveChangesAsync();
            return new { closedReportsToday = shop.ClosedReportsCount };
        }).RequireRateLimiting("reports");

        // All currently active discounts across the city.
        api.MapGet("/deals", async (AppDbContext db, Clock clock,
            string? q, string? category, bool? openNow, double? lat, double? lng) =>
        {
            var shops = await db.Shops.AsNoTracking().Include(s => s.Products).ToListAsync();
            var now = clock.UtcNow;
            var nowLocal = clock.LocalNow;
            var term = q?.Trim();

            var deals = new List<DealDto>();
            foreach (var shop in shops.Where(s => MatchesCategory(s, category)))
            {
                var status = ShopStatus.Compute(shop, nowLocal);
                if (openNow == true && !status.IsOpen) continue;
                var distance = Geo.DistanceKm(lat, lng, shop.Latitude, shop.Longitude);

                foreach (var p in shop.Products.Where(p => p.InStock && p.IsDealActive(now)))
                {
                    if (!string.IsNullOrEmpty(term) &&
                        !Contains(p.Name, term) && !Contains(p.Description, term) && !Contains(shop.Name, term))
                        continue;
                    deals.Add(new DealDto(p.ToDto(now), shop.Id, shop.Name, shop.Category,
                        status.IsOpen, status.Message, distance));
                }
            }

            IEnumerable<DealDto> sorted = lat is not null && lng is not null
                ? deals.OrderByDescending(d => d.ShopIsOpen).ThenBy(d => d.DistanceKm)
                : deals.OrderByDescending(d => d.ShopIsOpen).ThenByDescending(d => d.Product.DiscountPercent);
            return sorted.ToList();
        });
    }

    private static bool MatchesCategory(Shop s, string? category) =>
        string.IsNullOrWhiteSpace(category) || s.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool MatchesQuery(Shop s, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return true;
        var term = q.Trim();
        return Contains(s.Name, term) || Contains(s.Description, term) || Contains(s.Category, term)
            || s.Products.Any(p => Contains(p.Name, term));
    }

    private static bool Contains(string? text, string term) =>
        text is not null && text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
