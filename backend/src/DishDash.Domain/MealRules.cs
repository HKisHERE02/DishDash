namespace DishDash.Domain;

public static class MealRules
{
    public static void ValidateServings(int servings)
    {
        if (servings is < 1 or > 12) throw new RuleException("Choose between 1 and 12 servings.");
    }
    public static decimal Scale(decimal quantity, int baseServings, int servings)
    {
        ValidateServings(servings);
        if (baseServings < 1) throw new RuleException("The recipe has an invalid serving size.");
        return decimal.Round(quantity * servings / baseServings, 2, MidpointRounding.AwayFromZero);
    }
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static bool DietMatches(string requested, string actual) => requested == "Any" || requested == actual || requested == "Vegetarian" && actual == "Vegan";
    public static bool IsCompatible(Dish dish, UserPreference preference) =>
        !dish.Allergens.Intersect(preference.Allergens, StringComparer.OrdinalIgnoreCase).Any() && DietMatches(preference.Diet, dish.Diet);
    public static void ValidateRating(Order order, string userId, int stars, string comment)
    {
        if (order.UserId != userId) throw new RuleException("Order not found.");
        if (order.Status != OrderStatus.Delivered) throw new RuleException("Only delivered orders can be rated.");
        if (stars is < 1 or > 5 || comment.Length > 500) throw new RuleException("Choose 1–5 stars and a comment of up to 500 characters.");
    }
}
