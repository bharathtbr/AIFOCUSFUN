using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FocusFun.Api.Data;

public sealed class FocusFunDb(DbContextOptions<FocusFunDb> options) : DbContext(options)
{
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<Sheet> Sheets => Set<Sheet>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryWord> CategoryWords => Set<CategoryWord>();
    public DbSet<AiCall> AiCalls => Set<AiCall>();
    public DbSet<WordPicture> WordPictures => Set<WordPicture>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Parent>(e =>
        {
            e.HasIndex(p => p.Email).IsUnique();
            e.Property(p => p.Email).HasMaxLength(200);
            e.Property(p => p.PasswordHash).HasMaxLength(200);
        });

        b.Entity<Child>(e =>
        {
            e.Property(c => c.FirstName).HasMaxLength(30);
            e.Property(c => c.PaperSize).HasConversion<string>().HasMaxLength(10);
            e.PrimitiveCollection(c => c.Themes);
            e.PrimitiveCollection(c => c.Scripts);
            e.OwnsOne(c => c.Levels, l =>
            {
                foreach (var p in typeof(ChildLevels).GetProperties())
                    l.Property(p.Name).HasConversion<string>().HasMaxLength(10).HasColumnName($"Level{p.Name}");
            });
            e.HasOne(c => c.Parent).WithMany(p => p.Children).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Sheet>(e =>
        {
            e.Property(s => s.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(s => s.Level).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.PaperSize).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.Theme).HasMaxLength(30);
            e.Property(s => s.Letters).HasMaxLength(40);
            e.PrimitiveCollection(s => s.Words);
            e.Property(s => s.Seed).HasMaxLength(40);
            e.Property(s => s.Title).HasMaxLength(100);
            e.Property(s => s.Instruction).HasMaxLength(200);
            e.Property(s => s.SvgKey).HasMaxLength(200);
            e.Property(s => s.AnswerKeySvgKey).HasMaxLength(200);
            e.HasIndex(s => new { s.ChildId, s.CreatedAt });
            e.HasOne(s => s.Child).WithMany().HasForeignKey(s => s.ChildId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Key).IsUnique();
            e.Property(c => c.Key).HasMaxLength(40);
            e.Property(c => c.Name).HasMaxLength(60);
            e.Property(c => c.Description).HasMaxLength(300);
        });

        b.Entity<CategoryWord>(e =>
        {
            e.HasIndex(w => new { w.CategoryId, w.Word }).IsUnique();
            // Exact comparison, like the C# duplicate check: MySQL's default (utf8mb4_0900_ai_ci) ignores Telugu/Hindi
            // signs such as ం and ः, so అ, అం and అః would count as one word under the unique index.
            e.Property(w => w.Word).HasMaxLength(40).UseCollation("utf8mb4_bin");
            e.Property(w => w.Hindi).HasMaxLength(60);
            e.Property(w => w.Telugu).HasMaxLength(60);
            e.Property(w => w.Source).HasMaxLength(10);
            e.Property(w => w.PictureHint).HasMaxLength(300);
            e.HasOne(w => w.Category).WithMany(c => c.Words).HasForeignKey(w => w.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WordPicture>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(10);
            e.Property(p => p.Provider).HasMaxLength(20);
            e.Property(p => p.Model).HasMaxLength(80);
            e.Property(p => p.ImageKey).HasMaxLength(200);
            e.Property(p => p.OriginalKey).HasMaxLength(200);
            e.Property(p => p.CostUsd).HasPrecision(8, 4);
            e.HasIndex(p => new { p.WordId, p.Status });
            e.HasOne(p => p.Word).WithMany(w => w.Pictures).HasForeignKey(p => p.WordId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AiCall>(e =>
        {
            e.Property(a => a.Task).HasMaxLength(40);
            e.Property(a => a.Provider).HasMaxLength(20);
            e.Property(a => a.Model).HasMaxLength(80);
            e.HasIndex(a => new { a.Task, a.CreatedAt });
        });
    }

    /// <summary>Server version is configured rather than auto-detected so migrations can be created without a live database.</summary>
    public static void Configure(DbContextOptionsBuilder options, string connectionString, string serverVersion) =>
        options.UseMySql(connectionString, ServerVersion.Parse(serverVersion));
}

/// <summary>Used by `dotnet ef migrations add`; the connection string is never opened.</summary>
public sealed class FocusFunDbDesignFactory : IDesignTimeDbContextFactory<FocusFunDb>
{
    public FocusFunDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FocusFunDb>();
        FocusFunDb.Configure(options, "Server=localhost;Database=focusfun", "8.0.36-mysql");
        return new FocusFunDb(options.Options);
    }
}
