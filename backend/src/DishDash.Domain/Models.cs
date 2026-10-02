namespace DishDash.Domain;

public enum Fulfillment { Cook, Restaurant }
public enum OrderStatus { Confirmed, Preparing, OutForDelivery, Delivered }

public sealed class Dish
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Cuisine { get; set; } = "";
    public string Description { get; set; } = "";
    public string Image { get; set; } = "";
    public string Diet { get; set; } = "Omnivore";
    public string[] Allergens { get; set; } = [];
    public int BaseServings { get; set; } = 2;
    public int PrepMinutes { get; set; }
    public int CookMinutes { get; set; }
    public string Effort { get; set; } = "Easy";
    public List<DishIngredient> Ingredients { get; set; } = [];
    public List<CookingStep> Steps { get; set; } = [];
}
public sealed class DishIngredient
{
    public int Id { get; set; }
    public int DishId { get; set; }
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "g";
    public decimal BaseCost { get; set; }
}
public sealed class CookingStep
{
    public int Id { get; set; }
    public int DishId { get; set; }
    public int Position { get; set; }
    public string Instruction { get; set; } = "";
    public int TimerSeconds { get; set; }
}
public sealed class UserPreference
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Diet { get; set; } = "Any";
    public string[] Allergens { get; set; } = [];
    public string[] Cuisines { get; set; } = [];
    public int DefaultServings { get; set; } = 2;
    public decimal Budget { get; set; } = 30;
    public int MaxMinutes { get; set; } = 60;
}
public sealed class Favorite
{
    public string UserId { get; set; } = "";
    public int DishId { get; set; }
}
public sealed class CartItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public int DishId { get; set; }
    public int Servings { get; set; }
    public Fulfillment Kind { get; set; }
    public string ProviderId { get; set; } = "";
}
public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public Guid CheckoutKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
    public int Version { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public List<OrderStatusEvent> Events { get; set; } = [];
    public void Advance(DateTimeOffset now)
    {
        if (Status == OrderStatus.Delivered) throw new RuleException("This order is already delivered.");
        Status++;
        Version++;
        Events.Add(new OrderStatusEvent { Status = Status, CreatedAt = now });
    }
}
public sealed class OrderItem
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int DishId { get; set; }
    public string DishName { get; set; } = "";
    public int Servings { get; set; }
    public Fulfillment Kind { get; set; }
    public string ProviderName { get; set; } = "";
    public decimal Total { get; set; }
}
public sealed class OrderStatusEvent
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public Guid OrderId { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
public sealed class Rating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public Guid OrderId { get; set; }
    public int Stars { get; set; }
    public string Comment { get; set; } = "";
}
public sealed class RuleException(string message) : Exception(message);
