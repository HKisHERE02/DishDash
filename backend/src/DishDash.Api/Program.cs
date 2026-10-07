using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DishDash.Api;
using DishDash.Application;
using DishDash.Domain;
using DishDash.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<DishDashDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DishDash") ?? "Host=localhost;Database=dishdash;Username=dishdash"));
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 12;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<DishDashDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "dishdash.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "dishdash.csrf"; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest; });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<IGroceryProvider, DemoGroceryProvider>();
builder.Services.AddScoped<IRestaurantProvider, DemoRestaurantProvider>();
builder.Services.AddScoped<MealComparison>();
builder.Services.AddScoped<CheckoutService>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    await next();
});
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var status = error is RuleException or BadHttpRequestException ? 400 : error is DbUpdateException ? 409 : 500;
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new { title = status == 400 && error is RuleException ? error.Message : status == 409 ? "The data changed. Refresh and try again." : "We could not complete this request. Please try again." });
}));
if (app.Environment.IsProduction()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { title = "Your session verification expired. Refresh and try again." }); return; }
    }
    await next();
});
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/auth/csrf", (HttpContext c, IAntiforgery csrf) => Results.Ok(new { token = csrf.GetAndStoreTokens(c).RequestToken }));
app.MapAuth();
app.MapCatalog();
app.MapCommerce();
app.MapFallbackToFile("index.html");
if (app.Environment.IsDevelopment()) app.MapOpenApi("/api/openapi/{documentName}.json");
if (!app.Environment.IsEnvironment("Testing") && builder.Configuration.GetValue<bool>("Seed:Demo"))
{
    using var scope = app.Services.CreateScope();
    await DemoSeed.Initialize(scope.ServiceProvider.GetRequiredService<DishDashDbContext>(), scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(), builder.Configuration["Seed:DemoPassword"]);
}
app.Run();
public partial class Program;
