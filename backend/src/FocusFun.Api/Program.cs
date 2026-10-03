using System.Text.Json.Serialization;
using FocusFun.Api.Ai;
using FocusFun.Api.Auth;
using FocusFun.Api.Data;
using FocusFun.Api.Endpoints;
using FocusFun.Api.Storage;
using FocusFun.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Sheet generation
builder.Services.AddSingleton(SheetRenderer.CreateDefault());

// MySQL
string connectionString = config.GetConnectionString("FocusFun")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:FocusFun in appsettings.json.");
string serverVersion = config["Database:ServerVersion"] ?? "8.0.36-mysql";
builder.Services.AddDbContext<FocusFunDb>(o => FocusFunDb.Configure(o, connectionString, serverVersion));

// Cloudflare R2
builder.Services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
builder.Services.AddSingleton<IFileStorage, R2FileStorage>();

// Parent login (JWT)
var authOptions = config.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
if (authOptions.JwtKey.Length < 32 || authOptions.JwtKey.StartsWith('<'))
    throw new InvalidOperationException("Auth:JwtKey in appsettings.json must be a random string of at least 32 characters.");
builder.Services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));
builder.Services.AddSingleton<IPasswordHasher<Parent>, PasswordHasher<Parent>>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = authOptions.Issuer,
        ValidAudience = authOptions.Issuer,
        IssuerSigningKey = authOptions.SigningKey(),
    };
    // A token can outlive its account (database reset, account deleted); treat that as signed out rather than failing later on a foreign key.
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<FocusFunDb>();
            if (!Guid.TryParse(ctx.Principal?.FindFirst("sub")?.Value, out var parentId) || !await db.Parents.AnyAsync(p => p.Id == parentId))
                ctx.Fail("This account no longer exists.");
        },
    };
});
builder.Services.AddAuthorization(o => o.AddPolicy(AuthEndpoints.AdminPolicy, p => p.RequireClaim(AuthEndpoints.AdminClaim, "true")));

// AI jobs (admin only); the API key never leaves the server. Ai:Provider picks the model behind ITextModel.
builder.Services.Configure<AiOptions>(config.GetSection(AiOptions.Section));
string aiProvider = config["Ai:Provider"] ?? "claude";
if (aiProvider != "claude")
    throw new InvalidOperationException($"Ai:Provider \"{aiProvider}\" is not available yet; only \"claude\" is implemented.");
builder.Services.AddSingleton<ITextModel, ClaudeTextModel>();
builder.Services.AddScoped<WordFinder>();

// AI pictures (optional): drawing is off until Images:Google:ApiKey is set; approved pictures work either way.
builder.Services.Configure<ImageOptions>(config.GetSection(ImageOptions.Section));
builder.Services.AddHttpClient<IImageModel, GoogleImageModel>(c => c.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddScoped<PictureMaker>();
builder.Services.AddScoped<PictureLibrary>();
builder.Services.AddMemoryCache();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (config.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<FocusFunDb>().Database.Migrate();
}

using (var scope = app.Services.CreateScope())
    await Seeder.RunAsync(scope.ServiceProvider, config.GetSection(AdminOptions.Section).Get<AdminOptions>() ?? new AdminOptions());

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
api.MapAuthEndpoints();
api.MapChildEndpoints();
api.MapSheetEndpoints();
api.MapCategoryEndpoints();
api.MapPictureEndpoints();
api.MapTrainingEndpoints();
api.MapReferenceEndpoints(app.Environment.IsDevelopment());

app.Run();
