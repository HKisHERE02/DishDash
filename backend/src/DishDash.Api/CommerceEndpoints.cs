using System.Security.Claims;
using DishDash.Domain;
using DishDash.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace DishDash.Api;

public record CartInput(int DishId, int Servings, Fulfillment Kind, string ProviderId);
public record CheckoutInput(Guid RequestKey, string Outcome, decimal ExpectedTotal);
public record RatingInput(Guid OrderId, int Stars, string? Comment);
public static class CommerceEndpoints
{
    public static void MapCommerce(this WebApplication app)
    {
        var cart = app.MapGroup("/api/cart").RequireAuthorization();
        cart.MapGet("/", async (ClaimsPrincipal user, CheckoutService checkout) => Results.Ok(await checkout.GetCart(user.UserId())));
        cart.MapPost("/", async (CartInput input, ClaimsPrincipal user, DishDashDbContext db, CheckoutService checkout) =>
        {
            if (await db.CartItems.CountAsync(x => x.UserId == user.UserId()) >= 20) return Results.BadRequest(new { title = "Your basket can hold up to 20 items." });
            var item = new CartItem { UserId = user.UserId(), DishId = input.DishId, Servings = input.Servings, Kind = input.Kind, ProviderId = input.ProviderId };
            var line = await checkout.Quote(item); db.CartItems.Add(item); await db.SaveChangesAsync(); return Results.Ok(line);
        });
        cart.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, DishDashDbContext db) => await db.CartItems.Where(x => x.Id == id && x.UserId == user.UserId()).ExecuteDeleteAsync() > 0 ? Results.NoContent() : Results.NotFound());
        cart.MapDelete("/", async (ClaimsPrincipal user, DishDashDbContext db) => { await db.CartItems.Where(x => x.UserId == user.UserId()).ExecuteDeleteAsync(); return Results.NoContent(); });
        var orders = app.MapGroup("/api/orders").RequireAuthorization();
        orders.MapPost("/", async (CheckoutInput input, ClaimsPrincipal user, CheckoutService checkout) => TypedResults.Ok(OrderResponse.From(await checkout.Checkout(user.UserId(), input.RequestKey, input.Outcome, input.ExpectedTotal))));
        orders.MapGet("/", async (ClaimsPrincipal user, DishDashDbContext db) => TypedResults.Ok((await db.Orders.Include(x => x.Items).Include(x => x.Events).Where(x => x.UserId == user.UserId()).AsSplitQuery().AsNoTracking().ToListAsync()).OrderByDescending(x => x.CreatedAt).Select(OrderResponse.From).ToArray()));
        orders.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, DishDashDbContext db) => await db.Orders.Include(x => x.Items).Include(x => x.Events).AsSplitQuery().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == user.UserId()) is { } order ? Results.Ok(OrderResponse.From(order)) : Results.NotFound()).Produces<OrderResponse>();
        orders.MapPost("/{id:guid}/advance-demo", async (Guid id, ClaimsPrincipal user, DishDashDbContext db) =>
        {
            var order = await db.Orders.Include(x => x.Items).Include(x => x.Events).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id && x.UserId == user.UserId());
            if (order is null) return Results.NotFound();
            order.Advance(DateTimeOffset.UtcNow);
            db.Notifications.Add(new Notification { UserId = user.UserId(), OrderId = id, CreatedAt = DateTimeOffset.UtcNow, Message = $"Demo order status: {order.Status}." });
            await db.SaveChangesAsync(); return Results.Ok(OrderResponse.From(order));
        }).Produces<OrderResponse>();
        var notifications = app.MapGroup("/api/notifications").RequireAuthorization();
        notifications.MapGet("/", async (ClaimsPrincipal user, DishDashDbContext db) => TypedResults.Ok((await db.Notifications.Where(x => x.UserId == user.UserId()).AsNoTracking().ToListAsync()).OrderByDescending(x => x.CreatedAt).Select(NotificationResponse.From).ToArray()));
        notifications.MapPut("/{id:guid}/read", async (Guid id, ClaimsPrincipal user, DishDashDbContext db) => await db.Notifications.Where(x => x.Id == id && x.UserId == user.UserId()).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true)) > 0 ? Results.NoContent() : Results.NotFound());
        var ratings = app.MapGroup("/api/ratings").RequireAuthorization();
        ratings.MapGet("/", async (ClaimsPrincipal user, DishDashDbContext db) => TypedResults.Ok((await db.Ratings.Where(x => x.UserId == user.UserId()).AsNoTracking().ToListAsync()).Select(RatingResponse.From).ToArray()));
        ratings.MapPut("/", async (RatingInput input, ClaimsPrincipal user, DishDashDbContext db) =>
        {
            var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == input.OrderId && x.UserId == user.UserId());
            if (order is null) return Results.NotFound();
            MealRules.ValidateRating(order, user.UserId(), input.Stars, input.Comment ?? "");
            var rating = await db.Ratings.SingleOrDefaultAsync(x => x.OrderId == input.OrderId && x.UserId == user.UserId());
            if (rating is null) { rating = new Rating { UserId = user.UserId(), OrderId = input.OrderId }; db.Ratings.Add(rating); }
            rating.Stars = input.Stars; rating.Comment = input.Comment?.Trim() ?? "";
            await db.SaveChangesAsync(); return Results.Ok(RatingResponse.From(rating));
        }).Produces<RatingResponse>();
        ratings.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, DishDashDbContext db) => await db.Ratings.Where(x => x.Id == id && x.UserId == user.UserId()).ExecuteDeleteAsync() > 0 ? Results.NoContent() : Results.NotFound());
    }
}
