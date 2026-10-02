using DishDash.Application;
using DishDash.Domain;
namespace DishDash.UnitTests;

public sealed class MealRulesTests
{
    [Theory]
    [InlineData(1, 125)]
    [InlineData(2, 250)]
    [InlineData(4, 500)]
    [InlineData(12, 1500)]
    public void ScalesIngredients(int servings, decimal expected) => Assert.Equal(expected, MealRules.Scale(250, 2, servings));
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    public void RefusesInvalidServings(int servings) => Assert.Throws<RuleException>(() => MealRules.Scale(250, 2, servings));
    [Fact]
    public void AllergensAreHardExclusions() => Assert.False(MealRules.IsCompatible(new Dish { Diet = "Vegan", Allergens = ["Peanut"] }, new UserPreference { Diet = "Vegan", Allergens = ["peanut"] }));
    [Fact]
    public void VegetarianIncludesVegan() => Assert.True(MealRules.DietMatches("Vegetarian", "Vegan"));
    [Fact]
    public void TransitionsAreSequentialAndTerminal()
    {
        var order = new Order();
        order.Advance(DateTimeOffset.UtcNow); Assert.Equal(OrderStatus.Preparing, order.Status);
        order.Advance(DateTimeOffset.UtcNow); order.Advance(DateTimeOffset.UtcNow);
        Assert.Equal(OrderStatus.Delivered, order.Status); Assert.Equal(3, order.Events.Count);
        Assert.Throws<RuleException>(() => order.Advance(DateTimeOffset.UtcNow));
    }
    [Theory]
    [InlineData("other", OrderStatus.Delivered, 5)]
    [InlineData("owner", OrderStatus.Preparing, 5)]
    [InlineData("owner", OrderStatus.Delivered, 0)]
    [InlineData("owner", OrderStatus.Delivered, 6)]
    public void RatingsRejectInvalidOwnershipStateOrStars(string user, OrderStatus status, int stars) => Assert.Throws<RuleException>(() => MealRules.ValidateRating(new Order { UserId = "owner", Status = status }, user, stars, ""));
    [Fact]
    public void CompletedOwnerCanRate() => MealRules.ValidateRating(new Order { UserId = "owner", Status = OrderStatus.Delivered }, "owner", 5, "Great");
    [Fact]
    public void ComparisonExplainsMoneyTimeAndEffort()
    {
        var result = new MealComparison(new GroceryStub(), new RestaurantStub()).Compare(new Dish { PrepMinutes = 15, CookMinutes = 40, Effort = "Medium" }, 4);
        Assert.Equal(15.50m, result.MoneySavedCooking); Assert.Equal(24, result.MinutesSavedOrdering);
        Assert.Contains("15.50", result.Explanation); Assert.Contains("24 minutes", result.Explanation);
    }
    [Fact]
    public void RecommendationsExcludeAllergensEvenForFavorites()
    {
        var result = Recommendations.Rank([new Dish { Id = 1, Allergens = ["milk"] }, new Dish { Id = 2 }], new UserPreference { Allergens = ["milk"] }, new HashSet<int> { 1 }, new GroceryStub());
        Assert.Single(result); Assert.Equal(2, result[0].Dish.Id);
    }
    private sealed class GroceryStub : IGroceryProvider { public IReadOnlyList<GroceryOption> GetOptions(Dish d, int s) => [new("g", "Grocery", 21.40m, [])]; }
    [Fact]
    public void MissingProviderDataRefusesComparison() => Assert.Throws<RuleException>(() => new MealComparison(new EmptyGrocery(), new RestaurantStub()).Compare(new Dish(), 2));
    [Fact]
    public void MoneyRoundsHalfCentsAwayFromZero() => Assert.Equal(1.01m, MealRules.Money(1.005m));
    [Fact]
    public void InvalidRecipeBaseIsRejected() => Assert.Throws<RuleException>(() => MealRules.Scale(100, 0, 2));
    [Fact]
    public void LongRatingCommentsAreRejected() => Assert.Throws<RuleException>(() => MealRules.ValidateRating(new Order { UserId = "owner", Status = OrderStatus.Delivered }, "owner", 4, new string('x', 501)));
    private sealed class EmptyGrocery : IGroceryProvider { public IReadOnlyList<GroceryOption> GetOptions(Dish d, int s) => []; }
    private sealed class RestaurantStub : IRestaurantProvider { public IReadOnlyList<RestaurantOption> GetOptions(Dish d, int s) => [new("r", "Restaurant", 33.90m, 3m, 36.90m, 31, 4.7m)]; }
}
