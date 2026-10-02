using DishDash.Domain;

namespace DishDash.Api;

public sealed record ProfileResponse(string DisplayName, string Diet, string[] Allergens, string[] Cuisines, int DefaultServings, decimal Budget, int MaxMinutes)
{
    public static ProfileResponse From(UserPreference profile) => new(profile.DisplayName, profile.Diet, profile.Allergens, profile.Cuisines, profile.DefaultServings, profile.Budget, profile.MaxMinutes);
}

public sealed record OrderItemResponse(int DishId, string DishName, int Servings, Fulfillment Kind, string ProviderName, decimal Total);
public sealed record OrderEventResponse(OrderStatus Status, DateTimeOffset CreatedAt);
public sealed record OrderResponse(Guid Id, DateTimeOffset CreatedAt, decimal Total, OrderStatus Status, OrderItemResponse[] Items, OrderEventResponse[] Events)
{
    public static OrderResponse From(Order order) => new(order.Id, order.CreatedAt, order.Total, order.Status,
        order.Items.Select(item => new OrderItemResponse(item.DishId, item.DishName, item.Servings, item.Kind, item.ProviderName, item.Total)).ToArray(),
        order.Events.OrderBy(e => e.Status).Select(e => new OrderEventResponse(e.Status, e.CreatedAt)).ToArray());
}

public sealed record NotificationResponse(Guid Id, Guid OrderId, string Message, DateTimeOffset CreatedAt, bool IsRead)
{
    public static NotificationResponse From(Notification notification) => new(notification.Id, notification.OrderId, notification.Message, notification.CreatedAt, notification.IsRead);
}

public sealed record RatingResponse(Guid Id, Guid OrderId, int Stars, string Comment)
{
    public static RatingResponse From(Rating rating) => new(rating.Id, rating.OrderId, rating.Stars, rating.Comment);
}
