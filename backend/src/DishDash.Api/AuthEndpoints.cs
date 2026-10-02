using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DishDash.Domain;
using DishDash.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace DishDash.Api;

public record Credentials(string Email, string Password, string? DisplayName);
public record ProfileInput(string DisplayName, string Diet, string[] Allergens, string[] Cuisines, int DefaultServings, decimal Budget, int MaxMinutes);
public static class AuthEndpoints
{
    public static string UserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new RuleException("Please sign in.");
    public static void MapAuth(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (Credentials input, UserManager<AppUser> users, SignInManager<AppUser> signIn, DishDashDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || !new EmailAddressAttribute().IsValid(input.Email) || string.IsNullOrEmpty(input.Password) || input.Password.Length > 128 || string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Length > 60)
                return Results.BadRequest(new { title = "Enter a valid email, a name up to 60 characters, and a password of 12–128 characters." });
            var user = new AppUser { Email = input.Email.Trim(), UserName = input.Email.Trim() };
            await using var transaction = await db.Database.BeginTransactionAsync();
            var result = await users.CreateAsync(user, input.Password);
            if (!result.Succeeded) return Results.BadRequest(new { title = "Registration could not be completed. Use an available email and a password with 12+ characters, uppercase, lowercase, a number and a symbol." });
            db.Preferences.Add(new UserPreference { UserId = user.Id, DisplayName = input.DisplayName.Trim() });
            await db.SaveChangesAsync(); await transaction.CommitAsync();
            await signIn.SignInAsync(user, isPersistent: true);
            return Results.Ok(new { message = "Account created." });
        }).RequireRateLimiting("auth");
        app.MapPost("/api/auth/login", async (Credentials input, UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrEmpty(input.Password) || input.Email.Length > 254 || input.Password.Length > 128) return Results.BadRequest(new { title = "Enter your email and password." });
            var user = await users.FindByEmailAsync(input.Email.Trim());
            var result = user is null ? SignInResult.Failed : await signIn.PasswordSignInAsync(user, input.Password, isPersistent: true, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok(new { message = "Signed in." }) : Results.Json(new { title = "Unable to sign in with those credentials. If you recently tried several times, wait 15 minutes." }, statusCode: 401);
        }).RequireRateLimiting("auth");
        app.MapPost("/api/auth/logout", async (SignInManager<AppUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        app.MapGet("/api/profile", async (ClaimsPrincipal user, DishDashDbContext db) => TypedResults.Ok(ProfileResponse.From(await db.Preferences.SingleAsync(x => x.UserId == user.UserId())))).RequireAuthorization();
        app.MapPut("/api/profile", async (ProfileInput input, ClaimsPrincipal user, DishDashDbContext db) =>
        {
            MealRules.ValidateServings(input.DefaultServings);
            string[] diets = ["Any", "Omnivore", "Vegetarian", "Vegan", "Pescatarian"];
            string[] allergens = ["milk", "egg", "fish", "shellfish", "peanut", "tree nuts", "soy", "wheat", "sesame"];
            if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Length > 60 || !diets.Contains(input.Diet) || input.Budget is < 1 or > 500 || input.MaxMinutes is < 5 or > 240 || input.Allergens is null || input.Cuisines is null || input.Allergens.Any(x => !allergens.Contains(x)) || input.Cuisines.Length > 8 || input.Cuisines.Any(x => x is null || x.Length > 40))
                return Results.BadRequest(new { title = "Check your profile values. Budget must be $1–500 and cooking time 5–240 minutes." });
            var profile = await db.Preferences.SingleAsync(x => x.UserId == user.UserId());
            profile.DisplayName = input.DisplayName.Trim(); profile.Diet = input.Diet; profile.Allergens = input.Allergens.Distinct().ToArray(); profile.Cuisines = input.Cuisines.Distinct().ToArray(); profile.DefaultServings = input.DefaultServings; profile.Budget = input.Budget; profile.MaxMinutes = input.MaxMinutes;
            await db.SaveChangesAsync(); return Results.Ok(ProfileResponse.From(profile));
        }).Produces<ProfileResponse>().RequireAuthorization();
    }
}
