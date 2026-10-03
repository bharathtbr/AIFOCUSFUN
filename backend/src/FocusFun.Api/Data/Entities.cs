using FocusFun.Core;

namespace FocusFun.Api.Data;

public sealed class Parent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }

    /// <summary>Admins manage categories and word lists.</summary>
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Child> Children { get; set; } = [];
}

/// <summary>First name only: no photos, school or location (see SPEC "Safety, privacy").</summary>
public sealed class Child
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentId { get; set; }
    public required string FirstName { get; set; }
    public int Age { get; set; }
    public List<string> Themes { get; set; } = ["vehicles"];
    public List<string> Scripts { get; set; } = ["en"];
    public ChildLevels Levels { get; set; } = new();
    public PaperSize PaperSize { get; set; } = PaperSize.Letter;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Parent? Parent { get; set; }
}

/// <summary>Current level per module and per script; the difficulty tuner moves these (Phase 4).</summary>
public sealed class ChildLevels
{
    public Level Puzzles { get; set; } = Level.Easy;
    public Level Coloring { get; set; } = Level.Easy;
    public Level TracingEn { get; set; } = Level.Easy;
    public Level TracingHi { get; set; } = Level.Easy;
    public Level TracingTe { get; set; } = Level.Easy;
    public Level Memory { get; set; } = Level.Easy;
    public Level PaperArts { get; set; } = Level.Easy;
}

/// <summary>A sheet made for a child. The spec fields regenerate it exactly; the rendered SVGs live in R2.</summary>
public sealed class Sheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public SheetType Type { get; set; }
    public Level Level { get; set; }
    public required string Theme { get; set; }
    public string? Letters { get; set; }

    /// <summary>The category words the sheet was made with, so a reprint matches even if the list changes later.</summary>
    public List<string>? Words { get; set; }
    public required string Seed { get; set; }
    public PaperSize PaperSize { get; set; }
    public required string Title { get; set; }
    public required string Instruction { get; set; }
    public required string SvgKey { get; set; }
    public string? AnswerKeySvgKey { get; set; }
    public bool IsFavourite { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Child? Child { get; set; }

    public SheetRequest ToRequest() => new(Type, Level, Seed, Theme, PaperSize, Letters, Words);
}

/// <summary>A topic sheets can be about (vehicles, animals, fruits…). Its key is the sheet's Theme.</summary>
public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Lowercase slug, e.g. "animals"; matches a picture set when one exists.</summary>
    public required string Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>When the topic has enough approved image pictures, picture sheets use them instead of the drawn set (if any).</summary>
    public bool UseImagePictures { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CategoryWord> Words { get; set; } = [];
}

/// <summary>An approved word in a category, with optional Hindi and Telugu forms (checked by a parent before saving).</summary>
public sealed class CategoryWord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public required string Word { get; set; }
    public string? Hindi { get; set; }
    public string? Telugu { get; set; }

    /// <summary>How to draw it, for the image model (e.g. "child standing on one leg, palms together above the head").</summary>
    public string? PictureHint { get; set; }

    /// <summary>"admin" when typed in, "ai" when accepted from an AI suggestion.</summary>
    public required string Source { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Category? Category { get; set; }
    public List<WordPicture> Pictures { get; set; } = [];
}

public enum PictureStatus
{
    /// <summary>Generated, waiting for an admin.</summary>
    Draft,
    /// <summary>Checked by an admin; sheets may use it (one per word).</summary>
    Approved,
    Rejected,
}

/// <summary>
/// An image-model picture of a category word: the cleaned black line-art PNG sheets use, the original as returned,
/// and how it was made. Nothing reaches a sheet until an admin approves it. Never contains anything about a child.
/// </summary>
public sealed class WordPicture
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WordId { get; set; }
    public PictureStatus Status { get; set; } = PictureStatus.Draft;

    public required string Provider { get; set; }
    public required string Model { get; set; }
    public required string Prompt { get; set; }

    /// <summary>The picture this one was edited from ("fix with words"), if any.</summary>
    public Guid? EditedFromId { get; set; }

    /// <summary>R2 key of the cleaned PNG that sheets embed.</summary>
    public required string ImageKey { get; set; }

    /// <summary>R2 key of the image exactly as the model returned it, so it can be cleaned again later.</summary>
    public required string OriginalKey { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>JSON <see cref="FocusFun.Core.Art.PictureShape"/>: outline and colourable areas, found when the image was cleaned.</summary>
    public string? ShapeJson { get; set; }

    /// <summary>An admin checked that a mirrored copy looks clearly different (odd one out may then use it mirrored).</summary>
    public bool MirrorSafe { get; set; }

    /// <summary>Estimated price of this generation, from the model's configured per-image price.</summary>
    public decimal CostUsd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public CategoryWord? Word { get; set; }
}

/// <summary>
/// One AI job (e.g. finding words) with everything needed to train our own model later: the prompt, the web searches
/// and results the model used, its raw answer, and — once an admin reviews it — the corrected answer they kept.
/// Never contains anything about a child.
/// </summary>
public sealed class AiCall
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>What the call was for, e.g. "word-suggestions".</summary>
    public required string Task { get; set; }
    public required string Provider { get; set; }
    public required string Model { get; set; }
    public required string SystemPrompt { get; set; }
    public required string Prompt { get; set; }

    /// <summary>JSON: the searches the model ran and the results it saw, in order.</summary>
    public string? SearchTrace { get; set; }
    public required string RawOutput { get; set; }

    /// <summary>JSON: what the app showed the admin after cleaning the raw output.</summary>
    public required string Suggested { get; set; }

    /// <summary>JSON: what the admin kept, including their edits. Null until reviewed — this is the training label.</summary>
    public string? Accepted { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
