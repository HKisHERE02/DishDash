using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DishDash.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
namespace DishDash.IntegrationTests;

public sealed partial class ApiTests : IAsyncLifetime
{
    private readonly AppFactory factory = new();
    public Task InitializeAsync() => factory.Initialize();
    public Task DisposeAsync() { factory.Dispose(); return Task.CompletedTask; }
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private static async Task Csrf(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("token").GetString());
    }
    private async Task<HttpClient> User()
    {
        var client = Client(); await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = $"{Guid.NewGuid():N}@example.test", password = "Test-Meal-2026!", displayName = "Tester" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); await Csrf(client); return client;
    }
    [Fact]
    public async Task HealthRevealsOnlyStatus()
    {
        using var client = Client(); var body = await client.GetStringAsync("/api/health"); Assert.Equal("{\"status\":\"ok\"}", body);
    }
    [Theory]
    [InlineData("/api/profile")]
    [InlineData("/api/orders")]
    [InlineData("/api/cart")]
    [InlineData("/api/notifications")]
    [InlineData("/api/favorites")]
    [InlineData("/api/ratings")]
    [InlineData("/api/recommendations")]
    public async Task ProtectedEndpointsRejectAnonymous(string path)
    {
        using var client = Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task CsrfIsRequiredForRegistration()
    {
        using var client = Client(); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email = "bad@example.test", password = "Test-Meal-2026!", displayName = "Test" })).StatusCode);
    }
    [Fact]
    public async Task RegistrationLoginLogoutAndHashing()
    {
        using var client = Client(); await Csrf(client);
        var email = $"{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email, password = "short", displayName = "Test" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register", new { email, password = "Test-Meal-2026!", displayName = "Test" })).StatusCode);
        await Csrf(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DishDashDbContext>();
        Assert.NotEqual("Test-Meal-2026!", (await db.Users.SingleAsync(x => x.Email == email)).PasswordHash);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Incorrect-2026!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-Meal-2026!" })).StatusCode);
    }
    [Fact]
    public async Task CatalogSupportsSearchAndServings()
    {
        using var client = Client(); var dishes = await client.GetFromJsonAsync<JsonElement>("/api/dishes"); Assert.Equal(20, dishes.GetArrayLength());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/dishes?search=impossibledish")).GetArrayLength());
        var two = await client.GetFromJsonAsync<JsonElement>("/api/dishes/1/comparison?servings=2");
        var four = await client.GetFromJsonAsync<JsonElement>("/api/dishes/1/comparison?servings=4");
        Assert.Equal(two.GetProperty("cook").GetProperty("total").GetDecimal() * 2, four.GetProperty("cook").GetProperty("total").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/dishes/1/comparison?servings=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/dishes/999")).StatusCode);
    }
    private static async Task<JsonElement> Checkout(HttpClient client, string kind = "Cook")
    {
        var line = await client.PostAsJsonAsync("/api/cart", new { dishId = 1, servings = 2, kind, providerId = kind == "Cook" ? "market" : "table" });
        Assert.Equal(HttpStatusCode.OK, line.StatusCode);
        var price = (await line.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal();
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/orders", new { requestKey = key, outcome = "decline", expectedTotal = price })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/orders", new { requestKey = key, outcome = "success", expectedTotal = price });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        var duplicate = await client.PostAsJsonAsync("/api/orders", new { requestKey = key, outcome = "success", expectedTotal = price });
        Assert.Equal(order.GetProperty("id").GetString(), (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/cart")).GetArrayLength());
        return order;
    }
    [Theory]
    [InlineData("Cook")]
    [InlineData("Restaurant")]
    public async Task CheckoutIsAtomicIdempotentAndSupportsBothPaths(string kind)
    {
        using var client = await User(); await Checkout(client, kind);
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/orders")).GetArrayLength());
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/notifications")).GetArrayLength());
    }
    [Fact]
    public async Task OrdersEnforceOwnershipAndRatingState()
    {
        using var owner = await User(); using var stranger = await User();
        var order = await Checkout(owner); var id = order.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/orders/{id}/advance-demo", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync("/api/ratings", new { orderId = id, stars = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/ratings", new { orderId = id, stars = 5 })).StatusCode);
        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/orders/{id}/advance-demo", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/api/orders/{id}/advance-demo", null)).StatusCode);
        var rated = await owner.PutAsJsonAsync("/api/ratings", new { orderId = id, stars = 5, comment = "Excellent" });
        Assert.Equal(HttpStatusCode.OK, rated.StatusCode);
        var ratingId = (await rated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/ratings/{ratingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync("/api/ratings", new { orderId = id, stars = 4 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/ratings/{ratingId}")).StatusCode);
    }
    [Fact]
    public async Task CartRefusesInvalidValuesAndOtherUsersCannotRemoveItems()
    {
        using var owner = await User(); using var stranger = await User();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/cart", new { dishId = 1, servings = 13, kind = "Cook", providerId = "market" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/cart", new { dishId = 1, servings = 2, kind = "Restaurant", providerId = "missing" })).StatusCode);
        var response = await owner.PostAsJsonAsync("/api/cart", new { dishId = 1, servings = 2, kind = "Cook", providerId = "market" });
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/cart/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/orders", new { requestKey = Guid.NewGuid(), outcome = "success", expectedTotal = 0 })).StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/cart")).EnumerateArray());
    }
    [Fact]
    public async Task PreferencesExcludeAllergensAndFavoritesRemainPrivate()
    {
        using var owner = await User(); using var stranger = await User();
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync("/api/profile", new { displayName = "Test", diet = "Any", allergens = new[] { "milk" }, cuisines = new[] { "Indian" }, defaultServings = 2, budget = 30, maxMinutes = 60 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync("/api/favorites/1", null)).StatusCode);
        Assert.Equal(0, (await stranger.GetFromJsonAsync<JsonElement>("/api/favorites")).GetArrayLength());
        var recommendations = await owner.GetFromJsonAsync<JsonElement>("/api/recommendations");
        Assert.DoesNotContain(recommendations.EnumerateArray(), r => r.GetProperty("dish").GetProperty("allergens").EnumerateArray().Any(a => a.GetString() == "milk"));
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/cart", new { dishId = 1, servings = 2, kind = "Cook", providerId = "market" })).StatusCode);
    }
}
