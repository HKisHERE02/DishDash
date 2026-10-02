using DishDash.Application;
using DishDash.Domain;
namespace DishDash.Infrastructure;

public sealed class DemoGroceryProvider : IGroceryProvider
{
    public IReadOnlyList<GroceryOption> GetOptions(Dish dish, int servings)
    {
        MealRules.ValidateServings(servings);
        return new[] { ("market", "Green Basket · Demo", 1m), ("pantry", "Neighbourhood Pantry · Demo", 1.12m) }.Select(p =>
        {
            var ingredients = dish.Ingredients.Select(i => new IngredientEstimate(i.Name, MealRules.Scale(i.Quantity, dish.BaseServings, servings), i.Unit, MealRules.Money(i.BaseCost * servings / dish.BaseServings * p.Item3))).ToArray();
            return new GroceryOption(p.Item1, p.Item2, ingredients.Sum(x => x.Cost), ingredients);
        }).ToArray();
    }
}
public sealed class DemoRestaurantProvider : IRestaurantProvider
{
    public IReadOnlyList<RestaurantOption> GetOptions(Dish dish, int servings)
    {
        MealRules.ValidateServings(servings);
        return new[] { ("table", $"The {dish.Cuisine} Table · Demo", 0m, 3.49m, 25 + dish.Id % 15, 4.7m), ("kitchen", "Little Kitchen · Demo", 1.50m, 1.99m, 20 + dish.Id % 12, 4.5m) }.Select(p =>
        {
            var meal = MealRules.Money((9.50m + dish.Id % 6 + p.Item3) * servings);
            return new RestaurantOption(p.Item1, p.Item2, meal, p.Item4, meal + p.Item4, p.Item5, p.Item6);
        }).ToArray();
    }
}
