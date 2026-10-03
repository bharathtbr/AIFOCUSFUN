using FocusFun.Api.Auth;
using FocusFun.Core.Art;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FocusFun.Api.Data;

public sealed class AdminOptions
{
    public const string Section = "Admin";

    /// <summary>Login name for the admin account (a username or an email).</summary>
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Startup data: the admin account and a category for every built-in picture set. Safe to run on every start.</summary>
public static class Seeder
{
    public static async Task RunAsync(IServiceProvider services, AdminOptions admin)
    {
        var db = services.GetRequiredService<FocusFunDb>();
        await SeedAdminAsync(db, services.GetRequiredService<IPasswordHasher<Parent>>(), admin);
        await SeedCategoriesAsync(db);
    }

    /// <summary>Creates the admin once; afterwards only makes sure it is still an admin (the password is never overwritten).</summary>
    private static async Task SeedAdminAsync(FocusFunDb db, IPasswordHasher<Parent> hasher, AdminOptions admin)
    {
        if (string.IsNullOrWhiteSpace(admin.Username) || string.IsNullOrWhiteSpace(admin.Password)) return;

        string login = AuthEndpoints.Normalise(admin.Username);
        var parent = await db.Parents.SingleOrDefaultAsync(p => p.Email == login);
        if (parent is null)
        {
            parent = new Parent { Email = login, PasswordHash = string.Empty, IsAdmin = true };
            parent.PasswordHash = hasher.HashPassword(parent, admin.Password);
            db.Parents.Add(parent);
        }
        else
        {
            parent.IsAdmin = true;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Picture sets (vehicles, animals…) become categories, pre-filled with their drawable words.</summary>
    private static async Task SeedCategoriesAsync(FocusFunDb db)
    {
        foreach (string theme in PictureSets.Themes)
        {
            if (await db.Categories.AnyAsync(c => c.Key == theme)) continue;
            var set = PictureSets.For(theme);
            var category = new Category { Key = theme, Name = char.ToUpperInvariant(theme[0]) + theme[1..], Description = $"Built-in {set.Plural} with pictures." };
            category.Words.AddRange(set.All.Select(p => new CategoryWord { Word = p.Word, Source = "admin" }));
            db.Categories.Add(category);
        }
        await db.SaveChangesAsync();
    }
}
