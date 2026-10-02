using System.Security.Claims;
using DishDash.Application;
using DishDash.Domain;
using DishDash.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace DishDash.Api;

public static class CatalogEndpoints
{
    public static IQueryable<Dish> CompleteDishes(this DishDashDbContext db) => db.Dishes.Include(x => x.Ingredients).Include(x => x.Steps).AsSplitQuery();
    public static void MapCatalog(this WebApplication app)
    {
        app.MapGet("/api/dishes", async (string? search, string? cuisine, string? diet, int? maxMinutes, decimal? budget, string? sort, DishDashDbContext db, IGroceryProvider grocery) =>
        {
            if (search?.Length > 100 || maxMinutes is < 1 or > 240 || budget is < 0 or > 500) return Results.BadRequest(new { title = "The search filters are invalid." });
            IEnumerable<Dish> dishes = await db.CompleteDishes().AsNoTracking().ToListAsync();
            if (!string.IsNullOrWhiteSpace(search)) dishes = dishes.Where(d => d.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || d.Description.Contains(search, StringComparison.OrdinalIgnoreCase) || d.Ingredients.Any(i => i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrEmpty(cuisine)) dishes = dishes.Where(d => d.Cuisine == cuisine);
            if (!string.IsNullOrEmpty(diet)) dishes = dishes.Where(d => MealRules.DietMatches(diet, d.Diet));
            if (maxMinutes.HasValue) dishes = dishes.Where(d => d.PrepMinutes + d.CookMinutes <= maxMinutes);
            if (budget.HasValue) dishes = dishes.Where(d => grocery.GetOptions(d, 2).Any(x => x.Total <= budget));
            dishes = sort switch { "time" => dishes.OrderBy(d => d.PrepMinutes + d.CookMinutes), "price" => dishes.OrderBy(d => grocery.GetOptions(d, 2).Min(x => x.Total)), _ => dishes.OrderBy(d => d.Id) };
            return Results.Ok(dishes.Select(d => new { dish = d, fromPrice = grocery.GetOptions(d, 2).Min(x => x.Total) }));
        });
        app.MapGet("/api/dishes/{id:int}", async (int id, DishDashDbContext db) => await db.CompleteDishes().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) is { } dish ? Results.Ok(dish) : Results.NotFound(new { title = "Dish not found." }));
        app.MapGet("/api/dishes/{id:int}/comparison", async (int id, int servings, DishDashDbContext db, MealComparison comparison) => await db.CompleteDishes().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) is { } dish ? Results.Ok(comparison.Compare(dish, servings)) : Results.NotFound());
        app.MapGet("/api/dishes/{id:int}/grocery-options", async (int id, int servings, DishDashDbContext db, IGroceryProvider provider) => await db.CompleteDishes().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) is { } dish ? Results.Ok(provider.GetOptions(dish, servings)) : Results.NotFound());
        app.MapGet("/api/dishes/{id:int}/restaurant-options", async (int id, int servings, DishDashDbContext db, IRestaurantProvider provider) => await db.CompleteDishes().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) is { } dish ? Results.Ok(provider.GetOptions(dish, servings)) : Results.NotFound());
        var favorites = app.MapGroup("/api/favorites").RequireAuthorization();
        favorites.MapGet("/", async (ClaimsPrincipal user, DishDashDbContext db) => Results.Ok(await db.Favorites.Where(x => x.UserId == user.UserId()).Select(x => x.DishId).ToArrayAsync()));
        favorites.MapPut("/{dishId:int}", async (int dishId, ClaimsPrincipal user, DishDashDbContext db) =>
        {
            if (!await db.Dishes.AnyAsync(x => x.Id == dishId)) return Results.NotFound();
            if (!await db.Favorites.AnyAsync(x => x.DishId == dishId && x.UserId == user.UserId())) { db.Favorites.Add(new Favorite { DishId = dishId, UserId = user.UserId() }); await db.SaveChangesAsync(); }
            return Results.NoContent();
        });
        favorites.MapDelete("/{dishId:int}", async (int dishId, ClaimsPrincipal user, DishDashDbContext db) => { await db.Favorites.Where(x => x.UserId == user.UserId() && x.DishId == dishId).ExecuteDeleteAsync(); return Results.NoContent(); });
        app.MapGet("/api/recommendations", async (ClaimsPrincipal user, DishDashDbContext db, IGroceryProvider provider) =>
        {
            var profile = await db.Preferences.SingleAsync(x => x.UserId == user.UserId());
            var ids = await db.Favorites.Where(x => x.UserId == user.UserId()).Select(x => x.DishId).ToListAsync();
            return Results.Ok(Recommendations.Rank(await db.CompleteDishes().AsNoTracking().ToListAsync(), profile, ids.ToHashSet(), provider));
        }).RequireAuthorization();
    }
}
