using DishDash.Domain;
namespace DishDash.Application;

public record IngredientEstimate(string Name, decimal Quantity, string Unit, decimal Cost);
public record GroceryOption(string Id, string Name, decimal Total, IReadOnlyList<IngredientEstimate> Ingredients);
public record RestaurantOption(string Id, string Name, decimal MealPrice, decimal DeliveryFee, decimal Total, int EtaMinutes, decimal Rating);
public interface IGroceryProvider { IReadOnlyList<GroceryOption> GetOptions(Dish dish, int servings); }
public interface IRestaurantProvider { IReadOnlyList<RestaurantOption> GetOptions(Dish dish, int servings); }
public record Comparison(int Servings, GroceryOption Cook, RestaurantOption Order, int CookMinutes, string Effort, decimal MoneySavedCooking, int MinutesSavedOrdering, string Explanation);
public sealed class MealComparison(IGroceryProvider groceries, IRestaurantProvider restaurants)
{
    public Comparison Compare(Dish dish, int servings)
    {
        MealRules.ValidateServings(servings);
        var cook = groceries.GetOptions(dish, servings).OrderBy(x => x.Total).FirstOrDefault();
        var order = restaurants.GetOptions(dish, servings).OrderBy(x => x.Total).FirstOrDefault();
        if (cook is null || order is null) throw new RuleException("Comparison is unavailable because a demo provider has no options.");
        var saving = order.Total - cook.Total;
        var minutes = dish.PrepMinutes + dish.CookMinutes - order.EtaMinutes;
        var priceText = saving >= 0 ? $"Cooking is estimated to save ${saving:F2}" : $"Ordering is estimated to save ${-saving:F2}";
        var timeText = minutes >= 0 ? $"ordering may save {minutes} minutes" : $"cooking may save {-minutes} minutes";
        return new(servings, cook, order, dish.PrepMinutes + dish.CookMinutes, dish.Effort, saving, minutes, $"{priceText}, while {timeText}. Cooking requires {dish.Effort.ToLowerInvariant()} effort.");
    }
}
public record Recommendation(Dish Dish, string[] Reasons, int Score);
public static class Recommendations
{
    public static IReadOnlyList<Recommendation> Rank(IEnumerable<Dish> dishes, UserPreference preference, IReadOnlySet<int> favorites, IGroceryProvider groceries) =>
        dishes.Where(d => MealRules.IsCompatible(d, preference)).Select(d =>
        {
            var reasons = new List<string> { "Matches your dietary and allergen exclusions" };
            var score = 1;
            if (preference.Cuisines.Contains(d.Cuisine)) { score += 3; reasons.Add("One of your favorite cuisines"); }
            if (favorites.Contains(d.Id)) { score += 2; reasons.Add("A dish you saved"); }
            if (d.PrepMinutes + d.CookMinutes <= preference.MaxMinutes) { score++; reasons.Add("Fits your cooking time preference"); }
            if (groceries.GetOptions(d, preference.DefaultServings).Any(x => x.Total <= preference.Budget)) { score++; reasons.Add("Fits your grocery budget"); }
            return new Recommendation(d, reasons.ToArray(), score);
        }).OrderByDescending(x => x.Score).ThenBy(x => x.Dish.Id).Take(8).ToArray();
}
