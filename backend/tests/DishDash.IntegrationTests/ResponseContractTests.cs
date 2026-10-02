using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DishDash.IntegrationTests;

public sealed partial class ApiTests
{
    private static void Fields(JsonElement value, params string[] names) =>
        Assert.Equal(names.Order(), value.EnumerateObject().Select(property => property.Name).Order());

    private static void OrderContract(JsonElement order)
    {
        Fields(order, "id", "createdAt", "total", "status", "items", "events");
        Assert.NotEqual(Guid.Empty, order.GetProperty("id").GetGuid());
        Assert.True(order.GetProperty("total").GetDecimal() > 0);
        var item = Assert.Single(order.GetProperty("items").EnumerateArray());
        Fields(item, "dishId", "dishName", "servings", "kind", "providerName", "total");
        Assert.Equal(1, item.GetProperty("dishId").GetInt32());
        Assert.Equal(2, item.GetProperty("servings").GetInt32());
        Assert.Equal(order.GetProperty("total").GetDecimal(), item.GetProperty("total").GetDecimal());
        Assert.NotEmpty(order.GetProperty("events").EnumerateArray());
        foreach (var statusEvent in order.GetProperty("events").EnumerateArray())
            Fields(statusEvent, "status", "createdAt");
    }

    [Fact]
    public async Task ProfileResponsesExposeOnlyPreferences()
    {
        using var client = await User();
        string[] fields = ["displayName", "diet", "allergens", "cuisines", "defaultServings", "budget", "maxMinutes"];
        Fields(await client.GetFromJsonAsync<JsonElement>("/api/profile"), fields);
        using var response = await client.PutAsJsonAsync("/api/profile", new { displayName = "Alex", diet = "Any", allergens = Array.Empty<string>(), cuisines = new[] { "Indian" }, defaultServings = 4, budget = 25, maxMinutes = 45 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        Fields(profile, fields);
        Assert.Equal(4, profile.GetProperty("defaultServings").GetInt32());
        Assert.Equal("Alex", profile.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task OrderResponsesExposeOnlyPublicSnapshotsIncludingIdempotentReplay()
    {
        using var client = await User();
        var line = await client.PostAsJsonAsync("/api/cart", new { dishId = 1, servings = 2, kind = "Cook", providerId = "market" });
        Assert.Equal(HttpStatusCode.OK, line.StatusCode);
        var total = (await line.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal();
        var checkout = new { requestKey = Guid.NewGuid(), outcome = "success", expectedTotal = total };
        Guid? id = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/orders", checkout);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var order = await response.Content.ReadFromJsonAsync<JsonElement>();
            OrderContract(order);
            id ??= order.GetProperty("id").GetGuid();
            Assert.Equal(id.Value, order.GetProperty("id").GetGuid());
        }
        OrderContract(Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/orders")).EnumerateArray()));
        OrderContract(await client.GetFromJsonAsync<JsonElement>($"/api/orders/{id}"));
        using var advance = await client.PostAsync($"/api/orders/{id}/advance-demo", null);
        Assert.Equal(HttpStatusCode.OK, advance.StatusCode);
        var advanced = await advance.Content.ReadFromJsonAsync<JsonElement>();
        OrderContract(advanced);
        Assert.Equal("Preparing", advanced.GetProperty("status").GetString());
        Assert.Equal(2, advanced.GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task NotificationAndRatingResponsesRetainLinksWithoutOwnershipFields()
    {
        using var client = await User();
        var order = await Checkout(client);
        var id = order.GetProperty("id").GetGuid();
        var notice = Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/notifications")).EnumerateArray());
        Fields(notice, "id", "orderId", "message", "createdAt", "isRead");
        Assert.Equal(id, notice.GetProperty("orderId").GetGuid());
        for (var step = 0; step < 3; step++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/orders/{id}/advance-demo", null)).StatusCode);
        foreach (var stars in new[] { 5, 4 })
        {
            using var response = await client.PutAsJsonAsync("/api/ratings", new { orderId = id, stars, comment = "Tasty" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var rating = await response.Content.ReadFromJsonAsync<JsonElement>();
            Fields(rating, "id", "orderId", "stars", "comment");
            Assert.Equal(id, rating.GetProperty("orderId").GetGuid());
            Assert.Equal(stars, rating.GetProperty("stars").GetInt32());
        }
        Fields(Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/ratings")).EnumerateArray()), "id", "orderId", "stars", "comment");
    }

    [Theory]
    [InlineData("/api/health", 200)]
    [InlineData("/api/profile", 401)]
    [InlineData("/api/dishes/999", 404)]
    [InlineData("/api/dishes/1/comparison?servings=0", 400)]
    public async Task ProductionResponsesIncludeSafeHeadersEvenOnErrors(string path, int status)
    {
        await using var production = new AppFactory("Production");
        await production.Initialize();
        using var client = production.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync(path);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task OpenApiDescribesPublicResponseContracts()
    {
        await using var development = new AppFactory("Development");
        await development.Initialize();
        using var client = development.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/api/openapi/v1.json");
        var schemas = document.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "ProfileResponse", "OrderResponse", "OrderItemResponse", "OrderEventResponse", "NotificationResponse", "RatingResponse" })
        {
            var properties = schemas.GetProperty(name).GetProperty("properties");
            Assert.False(properties.TryGetProperty("userId", out _));
            Assert.False(properties.TryGetProperty("checkoutKey", out _));
            Assert.False(properties.TryGetProperty("version", out _));
        }
        foreach (var (path, method, name) in new[] { ("/api/profile", "get", "ProfileResponse"), ("/api/profile", "put", "ProfileResponse"), ("/api/orders", "post", "OrderResponse"), ("/api/orders/{id}", "get", "OrderResponse"), ("/api/orders/{id}/advance-demo", "post", "OrderResponse"), ("/api/ratings", "put", "RatingResponse") })
        {
            var schema = document.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
            Assert.Equal($"#/components/schemas/{name}", schema.GetProperty("$ref").GetString());
        }
    }
}
