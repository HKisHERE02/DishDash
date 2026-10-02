using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DishDash.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace DishDash.IntegrationTests;

public sealed partial class ApiTests
{
    [Fact]
    public async Task OpenApiIsAvailableOnlyInDevelopment()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/openapi/v1.json")).StatusCode);
        using var development = new AppFactory("Development");
        await development.Initialize();
        using var devClient = development.CreateClient();
        var document = await devClient.GetFromJsonAsync<JsonElement>("/api/openapi/v1.json");
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/dishes/{id}/comparison", out _));
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/orders", out _));
    }
    [Fact]
    public async Task DiscoveryCombinesFiltersAndSortsDeterministically()
    {
        using var client = Client();
        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/dishes?diet=Vegan&cuisine=Mexican&maxMinutes=30&budget=8");
        Assert.Single(filtered.EnumerateArray());
        Assert.Equal("Smoky Black Bean Tacos", filtered[0].GetProperty("dish").GetProperty("name").GetString());
        var ingredient = await client.GetFromJsonAsync<JsonElement>("/api/dishes?search=arborio");
        Assert.Single(ingredient.EnumerateArray());
        Assert.Equal("Mushroom Risotto", ingredient[0].GetProperty("dish").GetProperty("name").GetString());
        var prices = (await client.GetFromJsonAsync<JsonElement>("/api/dishes?sort=price")).EnumerateArray().Select(x => x.GetProperty("fromPrice").GetDecimal()).ToArray();
        Assert.Equal(prices.Order().ToArray(), prices);
        var times = (await client.GetFromJsonAsync<JsonElement>("/api/dishes?sort=time")).EnumerateArray().Select(x => x.GetProperty("dish").GetProperty("prepMinutes").GetInt32() + x.GetProperty("dish").GetProperty("cookMinutes").GetInt32()).ToArray();
        Assert.Equal(times.Order().ToArray(), times);
    }
    [Fact]
    public async Task ForgedSessionCannotAccessProfile()
    {
        using var client = Client(); client.DefaultRequestHeaders.Add("Cookie", "dishdash.session=forged-value");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
    }
    [Fact]
    public async Task ProfileRejectsInvalidInputWithoutChangingSavedValues()
    {
        using var client = await User();
        var original = await client.GetStringAsync("/api/profile");
        var response = await client.PutAsJsonAsync("/api/profile", new { displayName = "", diet = "unknown", allergens = new[] { "invalid" }, cuisines = Array.Empty<string>(), defaultServings = 0, budget = -1, maxMinutes = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(original, await client.GetStringAsync("/api/profile"));
    }
    [Fact]
    public async Task EmptyCheckoutAndInvalidSimulationAreRejected()
    {
        using var client = await User();
        foreach (var outcome in new[] { "success", "invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/orders", new { requestKey = Guid.NewGuid(), outcome, expectedTotal = 0 })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/orders")).EnumerateArray());
    }
    [Fact]
    public async Task NotificationsEnforceOwnership()
    {
        using var owner = await User(); using var stranger = await User(); await Checkout(owner);
        var notes = await owner.GetFromJsonAsync<JsonElement>("/api/notifications"); var id = notes[0].GetProperty("id").GetString();
        Assert.Empty((await stranger.GetFromJsonAsync<JsonElement>("/api/notifications")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsync($"/api/notifications/{id}/read", null)).StatusCode);
        Assert.False((await owner.GetFromJsonAsync<JsonElement>("/api/notifications"))[0].GetProperty("isRead").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync($"/api/notifications/{id}/read", null)).StatusCode);
        Assert.True((await owner.GetFromJsonAsync<JsonElement>("/api/notifications"))[0].GetProperty("isRead").GetBoolean());
    }
    [Fact]
    public async Task ChangedAllergiesInvalidateCartAndOwnerCanClearIt()
    {
        using var client = await User();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/cart", new { dishId = 1, servings = 2, kind = "Cook", providerId = "market" })).StatusCode);
        await client.PutAsJsonAsync("/api/profile", new { displayName = "Test", diet = "Any", allergens = new[] { "milk" }, cuisines = Array.Empty<string>(), defaultServings = 2, budget = 30, maxMinutes = 60 });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/cart")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/orders", new { requestKey = Guid.NewGuid(), outcome = "success", expectedTotal = 7.70m })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/cart")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/cart")).EnumerateArray());
    }
    [Fact]
    public async Task LoginLocksOutAfterFiveFailures()
    {
        using var client = Client(); await Csrf(client); var email = $"{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = "Test-Meal-2026!", displayName = "Test" });
        await Csrf(client); await client.PostAsync("/api/auth/logout", null); await Csrf(client);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-Meal-2026!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-Meal-2026!" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<DishDashDbContext>().Users.SingleAsync(x => x.Email == email);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
    }
    [Fact]
    public async Task DemoSeedIsRepeatableAndIncludesCompletedHistory()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DishDashDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
        await DemoSeed.Initialize(db, users, "Seed-Only-2026!"); await DemoSeed.Initialize(db, users, "Seed-Only-2026!");
        Assert.Equal(20, await db.Dishes.CountAsync()); Assert.Equal(3, await db.Orders.CountAsync()); Assert.Equal(3, await db.Ratings.CountAsync()); Assert.Equal(3, await db.Notifications.CountAsync());
    }
}
