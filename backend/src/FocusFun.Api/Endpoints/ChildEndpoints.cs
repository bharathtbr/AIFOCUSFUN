using FocusFun.Api.Auth;
using FocusFun.Api.Data;
using FocusFun.Core;
using Microsoft.EntityFrameworkCore;

namespace FocusFun.Api.Endpoints;

public static class ChildEndpoints
{
    private static readonly string[] KnownScripts = ["en", "hi", "te"];

    public static void MapChildEndpoints(this IEndpointRouteBuilder api)
    {
        var children = api.MapGroup("/children").RequireAuthorization();

        children.MapGet("/", async (HttpContext http, FocusFunDb db) =>
            await db.Children.Where(c => c.ParentId == http.User.ParentId())
                .OrderBy(c => c.CreatedAt)
                .Select(c => ToDto(c))
                .ToListAsync());

        children.MapPost("/", async (ChildInput body, HttpContext http, FocusFunDb db) =>
        {
            if (Validate(body) is { } problem) return problem;
            var child = new Child { ParentId = http.User.ParentId(), FirstName = body.FirstName.Trim() };
            Apply(child, body);
            db.Children.Add(child);
            await db.SaveChangesAsync();
            return Results.Created($"/api/children/{child.Id}", ToDto(child));
        });

        children.MapPut("/{id:guid}", async (Guid id, ChildInput body, HttpContext http, FocusFunDb db) =>
        {
            if (Validate(body) is { } problem) return problem;
            var child = await db.Children.SingleOrDefaultAsync(c => c.Id == id && c.ParentId == http.User.ParentId());
            if (child is null) return Results.NotFound();
            child.FirstName = body.FirstName.Trim();
            Apply(child, body);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(child));
        });
    }

    private static IResult? Validate(ChildInput body)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(body.FirstName) || body.FirstName.Trim().Length > 30)
            errors["firstName"] = ["First name is required (up to 30 characters)."];
        if (body.Age is < 3 or > 12)
            errors["age"] = ["Age should be between 3 and 12."];
        if (body.Scripts is { } scripts && scripts.Except(KnownScripts).Any())
            errors["scripts"] = ["Scripts can be en, hi and te."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static void Apply(Child child, ChildInput body)
    {
        child.Age = body.Age;
        if (body.Themes is { Count: > 0 } themes) child.Themes = themes;
        if (body.Scripts is { Count: > 0 } scripts) child.Scripts = scripts.Distinct().ToList();
        if (body.PaperSize is { } paper) child.PaperSize = paper;
        if (body.Levels is { } levels) child.Levels = levels;
    }

    private static ChildDto ToDto(Child c) => new(c.Id, c.FirstName, c.Age, c.Themes, c.Scripts, c.Levels, c.PaperSize);

    public sealed record ChildInput(string FirstName, int Age, List<string>? Themes, List<string>? Scripts, ChildLevels? Levels, PaperSize? PaperSize);

    public sealed record ChildDto(Guid Id, string FirstName, int Age, List<string> Themes, List<string> Scripts, ChildLevels Levels, PaperSize PaperSize);
}
