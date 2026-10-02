using System.Data;
using DishDash.Application;
using DishDash.Domain;
using Microsoft.EntityFrameworkCore;
namespace DishDash.Infrastructure;

public record CartLine(Guid Id, int DishId, string DishName, int Servings, Fulfillment Kind, string ProviderId, string ProviderName, decimal Total);
public sealed class CheckoutService(DishDashDbContext db, IGroceryProvider grocery, IRestaurantProvider restaurant)
{
    public async Task<CartLine> Quote(CartItem item)
    {
        MealRules.ValidateServings(item.Servings);
        var dish = await db.Dishes.Include(d => d.Ingredients).SingleOrDefaultAsync(d => d.Id == item.DishId) ?? throw new RuleException("Dish is unavailable.");
        var profile = await db.Preferences.SingleAsync(p => p.UserId == item.UserId);
        if (dish.Allergens.Intersect(profile.Allergens, StringComparer.OrdinalIgnoreCase).Any()) throw new RuleException("This dish contains an allergen excluded in your profile.");
        string name; decimal total;
        if (item.Kind == Fulfillment.Cook)
        {
            var option = grocery.GetOptions(dish, item.Servings).SingleOrDefault(o => o.Id == item.ProviderId) ?? throw new RuleException("Grocery option is unavailable.");
            name = option.Name; total = option.Total;
        }
        else if (item.Kind == Fulfillment.Restaurant)
        {
            var option = restaurant.GetOptions(dish, item.Servings).SingleOrDefault(o => o.Id == item.ProviderId) ?? throw new RuleException("Restaurant option is unavailable.");
            name = option.Name; total = option.Total;
        }
        else throw new RuleException("Choose Cook or Restaurant fulfillment.");
        return new(item.Id, dish.Id, dish.Name, item.Servings, item.Kind, item.ProviderId, name, total);
    }
    public async Task<IReadOnlyList<CartLine>> GetCart(string userId)
    {
        var items = await db.CartItems.Where(x => x.UserId == userId).ToListAsync();
        var lines = new List<CartLine>();
        foreach (var item in items) lines.Add(await Quote(item));
        return lines;
    }
    public async Task<Order> Checkout(string userId, Guid key, string outcome, decimal expectedTotal)
    {
        if (key == Guid.Empty) throw new RuleException("A checkout request key is required.");
        if (outcome is not ("success" or "decline")) throw new RuleException("Choose a demo payment outcome.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var prior = await db.Orders.Include(x => x.Items).Include(x => x.Events).SingleOrDefaultAsync(x => x.UserId == userId && x.CheckoutKey == key);
        if (prior is not null) return prior;
        var lines = await GetCart(userId);
        if (lines.Count == 0) throw new RuleException("Your basket is empty.");
        var total = lines.Sum(x => x.Total);
        if (expectedTotal != total) throw new RuleException("Your basket price changed. Refresh and review the total before checkout.");
        if (outcome == "decline") throw new RuleException("Demo payment declined. Your basket is saved; choose success to try again.");
        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            UserId = userId,
            CheckoutKey = key,
            CreatedAt = now,
            Total = total,
            Items = lines.Select(x => new OrderItem { DishId = x.DishId, DishName = x.DishName, Servings = x.Servings, Kind = x.Kind, ProviderName = x.ProviderName, Total = x.Total }).ToList(),
            Events = [new OrderStatusEvent { Status = OrderStatus.Confirmed, CreatedAt = now }]
        };
        db.Orders.Add(order);
        db.Notifications.Add(new Notification { UserId = userId, OrderId = order.Id, CreatedAt = now, Message = "Your demo order is confirmed. No payment was taken." });
        db.CartItems.RemoveRange(await db.CartItems.Where(x => x.UserId == userId).ToListAsync());
        await db.SaveChangesAsync(); await tx.CommitAsync(); return order;
    }
}
