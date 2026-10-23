using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShopFinder.Api.Data;
using ShopFinder.Api.Models;
using ShopFinder.Api.Services;

namespace ShopFinder.Api.Endpoints;

/// <summary>Endpoints for shop owners: sign up, log in, open/close, manage products and discounts.</summary>
public static class OwnerEndpoints
{
    public static void MapOwnerEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/owner").WithTags("Shop owners");

        auth.MapPost("/register", async (RegisterRequest r, AppDbContext db, Clock clock, TokenService tokens) =>
        {
            var phone = ShopMapper.NormalizePhone(r.Phone);
            if (string.IsNullOrEmpty(r.Password) || r.Password.Length < 6)
                throw new ApiError(400, "Password must be at least 6 characters.");
            if (await db.Shops.AnyAsync(s => s.Phone == phone))
                throw new ApiError(409, "A shop with this phone number already exists. Log in instead.");

            var (lat, lng) = ShopMapper.CoordsOrThrow(r.Latitude, r.Longitude);
            var shop = new Shop
            {
                Name = ShopMapper.Required(r.Name, "Shop name", 120),
                Category = ShopMapper.CategoryOrThrow(r.Category),
                Description = ShopMapper.Optional(r.Description, "Description", 1000),
                Address = ShopMapper.Required(r.Address, "Address", 300),
                Phone = phone,
                Latitude = lat,
                Longitude = lng,
                OpenTime = ShopMapper.TimeOrThrow(r.OpenTime, "Opening time"),
                CloseTime = ShopMapper.TimeOrThrow(r.CloseTime, "Closing time"),
                OpenDays = ShopMapper.DaysOrThrow(r.OpenDays),
                PasswordHash = Passwords.Hash(r.Password)
            };
            db.Shops.Add(shop);
            await db.SaveChangesAsync();

            return Results.Created($"/api/shops/{shop.Id}",
                new AuthResponse(tokens.CreateToken(shop.Id, shop.Name), shop.ToDetail(clock)));
        }).RequireRateLimiting("auth");

        auth.MapPost("/login", async (LoginRequest r, AppDbContext db, Clock clock, TokenService tokens) =>
        {
            var phone = ShopMapper.NormalizePhone(r.Phone);
            var shop = await db.Shops.Include(s => s.Products).FirstOrDefaultAsync(s => s.Phone == phone);
            if (shop is null || string.IsNullOrEmpty(r.Password) || !Passwords.Verify(r.Password, shop.PasswordHash))
                throw new ApiError(401, "Wrong phone number or password.");
            return new AuthResponse(tokens.CreateToken(shop.Id, shop.Name), shop.ToDetail(clock));
        }).RequireRateLimiting("auth");

        // Everything below requires the owner's token.
        var owner = app.MapGroup("/api/owner").WithTags("Shop owners").RequireAuthorization();

        owner.MapGet("/me", async (ClaimsPrincipal user, AppDbContext db, Clock clock) =>
            (await LoadOwnShop(user, db)).ToDetail(clock));

        owner.MapPut("/shop", async (UpdateShopRequest r, ClaimsPrincipal user, AppDbContext db, Clock clock) =>
        {
            var shop = await LoadOwnShop(user, db);
            var (lat, lng) = ShopMapper.CoordsOrThrow(r.Latitude, r.Longitude);
            shop.Name = ShopMapper.Required(r.Name, "Shop name", 120);
            shop.Category = ShopMapper.CategoryOrThrow(r.Category);
            shop.Description = ShopMapper.Optional(r.Description, "Description", 1000);
            shop.Address = ShopMapper.Required(r.Address, "Address", 300);
            shop.Latitude = lat;
            shop.Longitude = lng;
            shop.OpenTime = ShopMapper.TimeOrThrow(r.OpenTime, "Opening time");
            shop.CloseTime = ShopMapper.TimeOrThrow(r.CloseTime, "Closing time");
            shop.OpenDays = ShopMapper.DaysOrThrow(r.OpenDays);
            await db.SaveChangesAsync();
            return shop.ToDetail(clock);
        });

        // The big Open / Closed button. Manual choices last until midnight, then regular hours take over.
        owner.MapPost("/status", async (StatusRequest r, ClaimsPrincipal user, AppDbContext db, Clock clock) =>
        {
            var shop = await LoadOwnShop(user, db);
            switch (r.Mode?.Trim().ToLowerInvariant())
            {
                case "open":
                    shop.StatusOverride = ShopStatus.Open;
                    shop.OverrideDate = clock.LocalToday;
                    break;
                case "closed":
                    shop.StatusOverride = ShopStatus.Closed;
                    shop.OverrideDate = clock.LocalToday;
                    break;
                case "auto":
                    shop.StatusOverride = null;
                    shop.OverrideDate = null;
                    break;
                default:
                    throw new ApiError(400, "Mode must be 'open', 'closed' or 'auto'.");
            }
            // The owner just confirmed the real status, so earlier "it's closed" reports are stale.
            shop.ClosedReportsCount = 0;
            shop.LastStatusChangeUtc = clock.UtcNow;
            await db.SaveChangesAsync();
            return shop.ToDetail(clock);
        });

        owner.MapPost("/products", async (ProductRequest r, ClaimsPrincipal user, AppDbContext db, Clock clock) =>
        {
            var shop = await LoadOwnShop(user, db);
            var product = new Product { ShopId = shop.Id };
            Apply(product, r, clock);
            shop.Products.Add(product);
            await db.SaveChangesAsync();
            return Results.Created($"/api/owner/products/{product.Id}", product.ToDto(clock.UtcNow));
        });

        owner.MapPut("/products/{id:int}", async (int id, ProductRequest r, ClaimsPrincipal user, AppDbContext db, Clock clock) =>
        {
            var shop = await LoadOwnShop(user, db);
            var product = shop.Products.FirstOrDefault(p => p.Id == id)
                ?? throw new ApiError(404, "Product not found.");
            Apply(product, r, clock);
            await db.SaveChangesAsync();
            return product.ToDto(clock.UtcNow);
        });

        owner.MapDelete("/products/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var shop = await LoadOwnShop(user, db);
            var product = shop.Products.FirstOrDefault(p => p.Id == id)
                ?? throw new ApiError(404, "Product not found.");
            db.Products.Remove(product);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static async Task<Shop> LoadOwnShop(ClaimsPrincipal user, AppDbContext db)
    {
        var shopId = TokenService.GetShopId(user) ?? throw new ApiError(401, "Please log in again.");
        return await db.Shops.Include(s => s.Products).FirstOrDefaultAsync(s => s.Id == shopId)
            ?? throw new ApiError(401, "This shop no longer exists. Please log in again.");
    }

    /// <summary>Full update of a product from the request (PUT semantics).</summary>
    private static void Apply(Product p, ProductRequest r, Clock clock)
    {
        p.Name = ShopMapper.Required(r.Name, "Product name", 120);
        p.Description = ShopMapper.Optional(r.Description, "Description", 1000);

        if (r.Price is null || r.Price <= 0) throw new ApiError(400, "Price must be greater than 0.");
        if (r.Price > 1_000_000) throw new ApiError(400, "Price is too high.");
        p.Price = Math.Round(r.Price.Value, 2, MidpointRounding.AwayFromZero);

        var discount = r.DiscountPercent ?? 0;
        if (discount is < 0 or > 90) throw new ApiError(400, "Discount must be between 0 and 90 percent.");
        p.DiscountPercent = discount;

        DateTime? ends = null;
        if (discount > 0 && r.DealEndsUtc is not null)
        {
            var value = r.DealEndsUtc.Value;
            ends = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            if (ends <= clock.UtcNow) throw new ApiError(400, "The deal end time must be in the future.");
        }
        p.DealEndsUtc = ends;
        p.InStock = r.InStock ?? true;
    }
}
