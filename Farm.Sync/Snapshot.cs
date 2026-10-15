using System.Text.Json;
using System.Text.Json.Serialization;

namespace AffineSync;

public sealed partial record Snapshot
{
    [JsonPropertyName("capturedAtUtc")] public required DateTimeOffset CapturedAtUtc { get; init; }
    [JsonPropertyName("documents")] public required List<SourceDocument> Documents { get; init; }

    public static Snapshot Parse(string json) => ParseUnvalidated(json).Validated();

    // The runner validates per document so one bad document does not discard the rest.
    public static Snapshot ParseUnvalidated(string json) =>
        JsonSerializer.Deserialize<Snapshot>(json) ?? throw new InvalidDataException("Reader returned a null snapshot.");

    private Snapshot Validated() { Validate(); return this; }

    // Keeps every document that validates (including the cross-document uniqueness rules) and reports the rest,
    // so one bad document cannot block the others. Snapshot-level problems still throw.
    public Snapshot WithoutInvalidDocuments(Action<string, string> reportSkipped)
    {
        if (Documents is null) throw new InvalidDataException("documents must be an array.");
        (this with { Documents = [] }).Validate();
        var kept = new List<SourceDocument>();
        foreach (var document in Documents)
        {
            try { (this with { Documents = [.. kept, document] }).Validate(); kept.Add(document); }
            catch (InvalidDataException exception) { reportSkipped(document?.Id ?? "(no id)", exception.Message); }
        }
        return this with { Documents = kept };
    }

    public void Validate()
    {
        ValidateTimestamp(CapturedAtUtc, "capturedAtUtc");
        if (CapturedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidDataException("Snapshot capture timestamp is implausibly far in the future.");
        if (Documents is null) throw new InvalidDataException("documents must be an array.");
        var documentIds = new HashSet<string>(StringComparer.Ordinal);
        var metadataIds = new HashSet<int>();
        var bookmarkIds = new HashSet<int>();
        var toolIds = new HashSet<int>();
        var sourceDocIds = new HashSet<string>(StringComparer.Ordinal);
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var document in Documents)
        {
            if (document is null || string.IsNullOrWhiteSpace(document.Id) || !documentIds.Add(document.Id))
                throw new InvalidDataException("Document IDs must be nonempty and unique.");
            ValidateTimestamp(document.UpdatedAtUtc, $"document {document.Id} updatedAtUtc");
            if (document.ContentHash is not null && (document.ContentHash.Length == 0 || document.ContentHash.Length > 256))
                throw new InvalidDataException("Optional document contentHash must be a nonempty string of at most 256 characters.");
            if (document.UpdatedAtUtc > CapturedAtUtc)
                throw new InvalidDataException($"Document {document.Id} is newer than the capture boundary; retry a fresh snapshot.");
            if (document.Metadata is null || document.Bookmarks is null)
                throw new InvalidDataException($"Document {document.Id} metadata and bookmarks must be arrays.");
            foreach (var row in document.Metadata)
            {
                if (row is null) throw new InvalidDataException("Metadata rows cannot be null.");
                ValidateId(row.Id, metadataIds);
                ValidateString(row.DeveloperName, 128, "DeveloperName");
                ValidateString(row.Title, 256, "Title");
                ValidateString(row.Description, 1024, "Description");
                ValidateString(row.RepositoryUrl, 512, "RepositoryUrl");
                if (row.SpecifiedIssueUrl is not null)
                {
                    ValidateString(row.SpecifiedIssueUrl, 512, "SpecifiedIssueUrl");
                    if (!GitHubIssueUrl().IsMatch(row.SpecifiedIssueUrl))
                        throw new InvalidDataException("SpecifiedIssueUrl must be a GitHub issue link.");
                }
                ValidateTimestamp(row.CreatedAtUtc, "CreatedAtUtc");
                ValidateTimestamp(row.UpdatedAtUtc, "UpdatedAtUtc");
                if (row.UpdatedAtUtc < row.CreatedAtUtc || row.UpdatedAtUtc > document.UpdatedAtUtc)
                    throw new InvalidDataException("Metadata timestamps must be ordered and not newer than their document.");
            }
            foreach (var row in document.Bookmarks)
            {
                if (row is null) throw new InvalidDataException("Bookmark rows cannot be null.");
                ValidateId(row.Id, bookmarkIds);
                ValidateString(row.Platform, 64, "Platform");
                ValidateString(row.Url, 512, "Url");
                ValidateString(row.PlatformId, 128, "PlatformId");
                ValidateString(row.Description, 512, "Description");
                ValidateString(row.Color, 32, "Color");
                ValidateTimestamp(row.CreatedAtUtc, "CreatedAtUtc");
                if (row.CreatedAtUtc > document.UpdatedAtUtc)
                    throw new InvalidDataException("Bookmark CreatedAtUtc cannot be newer than its document.");
            }
            foreach (var row in document.Tools)
            {
                if (row is null) throw new InvalidDataException("Tool rows cannot be null.");
                ValidateId(row.Id, toolIds);
                ValidateString(row.Name, 128, "Tool.Name");
                ValidateString(row.Description, 1024, "Tool.Description");
                ValidateString(row.Category, 64, "Tool.Category");
                if (!ValidToolCategories.Contains(row.Category))
                    throw new InvalidDataException($"Tool.Category must be one of {string.Join(", ", ValidToolCategories)}: {row.Category}");
                ValidateString(row.DirectUrl, 512, "Tool.DirectUrl");
                if (row.RepositoryUrl is not null)
                    ValidateString(row.RepositoryUrl, 512, "Tool.RepositoryUrl");
                if (row.IssueUrl is not null)
                    ValidateString(row.IssueUrl, 512, "Tool.IssueUrl");
                ValidateTimestamp(row.CreatedAtUtc, "Tool.CreatedAtUtc");
                ValidateTimestamp(row.UpdatedAtUtc, "Tool.UpdatedAtUtc");
                if (row.CreatedAtUtc > document.UpdatedAtUtc)
                    throw new InvalidDataException("Tool CreatedAtUtc cannot be newer than its document.");
            }
            PortfolioValidation.Validate(document, sourceDocIds, slugs);
        }
    }

    internal static void ValidateText(string? value, int maximum, string field, bool required)
    {
        if (value is null)
        {
            if (required) throw new InvalidDataException($"{field} is required.");
            return;
        }
        if (value.Length > maximum || (required && string.IsNullOrWhiteSpace(value)))
            throw new InvalidDataException($"{field} must be a{(required ? " nonempty" : "")} string of at most {maximum} characters.");
    }

    private static readonly HashSet<string> ValidToolCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "SearchCollect", "ConvertTransformCompose", "ObserveWatching", "CalculatingAnalysing", "WriteExecute"
    };

    private static void ValidateId(int id, HashSet<int> ids)
    {
        if (id <= 0 || !ids.Add(id))
            throw new InvalidDataException($"Entity IDs must be positive and unique per entity type across the snapshot: {id}.");
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^https://github\.com/[\w.-]+/[\w.-]+/issues/\d+$")]
    private static partial System.Text.RegularExpressions.Regex GitHubIssueUrl();

    private static void ValidateString(string value, int maximum, string field)
    {
        if (value is null || value.Length > maximum)
            throw new InvalidDataException($"{field} must be a non-null string of at most {maximum} characters.");
    }

    private static void ValidateTimestamp(DateTimeOffset value, string field)
    {
        if (value.Offset != TimeSpan.Zero || value < CheckpointStore.Baseline)
            throw new InvalidDataException($"{field} must be a UTC timestamp on or after 1990-01-01.");
    }
}

public sealed record SourceDocument
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("updatedAtUtc")] public required DateTimeOffset UpdatedAtUtc { get; init; }
    [JsonPropertyName("contentHash")] public string? ContentHash { get; init; }
    [JsonPropertyName("metadata")] public required List<MetadataRow> Metadata { get; init; }
    [JsonPropertyName("bookmarks")] public required List<BookmarkRow> Bookmarks { get; init; }
    [JsonPropertyName("profiles")] public List<ProfileRow> Profiles { get; init; } = [];
    [JsonPropertyName("projects")] public List<ProjectRow> Projects { get; init; } = [];
    [JsonPropertyName("experiences")] public List<ExperienceRow> Experiences { get; init; } = [];
    [JsonPropertyName("skills")] public List<SkillRow> Skills { get; init; } = [];
    [JsonPropertyName("socialLinks")] public List<SocialLinkRow> SocialLinks { get; init; } = [];
    [JsonPropertyName("gigs")] public List<GigRow> Gigs { get; init; } = [];
    [JsonPropertyName("tools")] public List<ToolRow> Tools { get; init; } = [];

    public bool HasPortfolioRows =>
        Profiles.Count + Projects.Count + Experiences.Count + Skills.Count + SocialLinks.Count + Gigs.Count > 0;
}

public sealed record MetadataRow
{
    public required int Id { get; init; }
    public required string DeveloperName { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string RepositoryUrl { get; init; }
    public string? SpecifiedIssueUrl { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record BookmarkRow
{
    public required int Id { get; init; }
    public required string Platform { get; init; }
    public required string Url { get; init; }
    public required string PlatformId { get; init; }
    public required string Description { get; init; }
    public required string Color { get; init; }
    public required int DisplayOrder { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record ToolRow
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string DirectUrl { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? IssueUrl { get; init; }
    public required int DisplayOrder { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record ProfileRow
{
    public required string SourceDocId { get; init; }
    public required string DisplayName { get; init; }
    public required string Headline { get; init; }
    public string? Bio { get; init; }
    public string? Location { get; init; }
    public string? ContactEmail { get; init; }
    public string? ProfileImageUrl { get; init; }
    public string? ResumeUrl { get; init; }
}

public sealed record ProjectRow
{
    public required string SourceDocId { get; init; }
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required string Summary { get; init; }
    public string? Description { get; init; }
    public string? DemoUrl { get; init; }
    public string? SourceUrl { get; init; }
    public string? Role { get; init; }
    public string? Organization { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsFeatured { get; init; }
    public int DisplayOrder { get; init; }
    public List<string> Skills { get; init; } = [];
}

public sealed record ExperienceRow
{
    public required string SourceDocId { get; init; }
    public required string Company { get; init; }
    public required string JobTitle { get; init; }
    public string? Description { get; init; }
    public required string EmploymentType { get; init; }
    public required string WorkMode { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsCurrent { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record SkillRow
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record SocialLinkRow
{
    public required string Label { get; init; }
    public required string Url { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record GigRow
{
    public required string SourceDocId { get; init; }
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public string? BodyMarkdown { get; init; }
    public string? Problem { get; init; }
    public string? Outcome { get; init; }
    public DateOnly? SolvedOn { get; init; }
    public int DisplayOrder { get; init; }
    public List<string> Skills { get; init; } = [];
    public List<GigStepRow> Steps { get; init; } = [];
}

public sealed record GigStepRow
{
    public required int StepNumber { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
}

internal static class PortfolioValidation
{
    public static void Validate(SourceDocument document, HashSet<string> sourceDocIds, HashSet<string> slugs)
    {
        void Source(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !sourceDocIds.Add(id))
                throw new InvalidDataException($"SourceDocId must be unique and at most 64 characters: {id}.");
        }
        static void Order(int value, string field)
        {
            if (value < 0) throw new InvalidDataException($"{field} must not be negative.");
        }
        static void Dates(DateOnly? start, DateOnly? end, string name)
        {
            if (start is not null && end is not null && end < start)
                throw new InvalidDataException($"{name} EndDate precedes StartDate.");
        }
        static void Skills(List<string> skills, string name)
        {
            if (skills is null || skills.Any(skill => string.IsNullOrWhiteSpace(skill) || skill.Length > 100))
                throw new InvalidDataException($"{name} skill names must be nonempty and at most 100 characters.");
        }
        void Slug(string slug, int maximum, string name)
        {
            Snapshot.ValidateText(slug, maximum, $"{name}.Slug", true);
            if (!slugs.Add($"{name}:{slug}")) throw new InvalidDataException($"Duplicate slug {slug}.");
        }

        foreach (var row in document.Profiles)
        {
            Source(row.SourceDocId);
            Snapshot.ValidateText(row.DisplayName, 160, "Profile.DisplayName", true);
            Snapshot.ValidateText(row.Headline, 240, "Profile.Headline", true);
            Snapshot.ValidateText(row.Bio, 4000, "Profile.Bio", false);
            Snapshot.ValidateText(row.Location, 160, "Profile.Location", false);
            Snapshot.ValidateText(row.ContactEmail, 320, "Profile.ContactEmail", false);
            Snapshot.ValidateText(row.ProfileImageUrl, 2048, "Profile.ProfileImageUrl", false);
            Snapshot.ValidateText(row.ResumeUrl, 2048, "Profile.ResumeUrl", false);
        }
        foreach (var row in document.Projects)
        {
            Source(row.SourceDocId);
            Snapshot.ValidateText(row.Title, 200, "Project.Title", true);
            Slug(row.Slug, 220, "Project");
            Snapshot.ValidateText(row.Summary, 500, "Project.Summary", true);
            Snapshot.ValidateText(row.Description, 8000, "Project.Description", false);
            Snapshot.ValidateText(row.DemoUrl, 2048, "Project.DemoUrl", false);
            Snapshot.ValidateText(row.SourceUrl, 2048, "Project.SourceUrl", false);
            Snapshot.ValidateText(row.Role, 160, "Project.Role", false);
            Snapshot.ValidateText(row.Organization, 200, "Project.Organization", false);
            Order(row.DisplayOrder, "Project.DisplayOrder");
            Dates(row.StartDate, row.EndDate, $"Project {row.Slug}");
            Skills(row.Skills, $"Project {row.Slug}");
        }
        foreach (var row in document.Experiences)
        {
            Source(row.SourceDocId);
            Snapshot.ValidateText(row.Company, 200, "Experience.Company", true);
            Snapshot.ValidateText(row.JobTitle, 160, "Experience.JobTitle", true);
            Snapshot.ValidateText(row.Description, 4000, "Experience.Description", false);
            if (!Enum.TryParse<Farm.Data.Entities.EmploymentType>(row.EmploymentType, out _) ||
                !Enum.TryParse<Farm.Data.Entities.WorkMode>(row.WorkMode, out _))
                throw new InvalidDataException($"Experience {row.SourceDocId} has an invalid EmploymentType or WorkMode.");
            Order(row.DisplayOrder, "Experience.DisplayOrder");
            Dates(row.StartDate, row.EndDate, $"Experience {row.SourceDocId}");
            if (row.IsCurrent && row.EndDate is not null)
                throw new InvalidDataException($"Experience {row.SourceDocId}: a current role cannot have an EndDate.");
        }
        var skillNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in document.Skills)
        {
            Snapshot.ValidateText(row.Name, 100, "Skill.Name", true);
            Snapshot.ValidateText(row.Category, 80, "Skill.Category", true);
            Order(row.DisplayOrder, "Skill.DisplayOrder");
            if (!skillNames.Add(row.Name)) throw new InvalidDataException($"Duplicate skill {row.Name}.");
        }
        var labels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in document.SocialLinks)
        {
            Snapshot.ValidateText(row.Label, 80, "SocialLink.Label", true);
            Snapshot.ValidateText(row.Url, 2048, "SocialLink.Url", true);
            Order(row.DisplayOrder, "SocialLink.DisplayOrder");
            if (!labels.Add(row.Label)) throw new InvalidDataException($"Duplicate social link {row.Label}.");
        }
        foreach (var row in document.Gigs)
        {
            Source(row.SourceDocId);
            Snapshot.ValidateText(row.Title, 200, "Gig.Title", true);
            Slug(row.Slug, 220, "Gig");
            Snapshot.ValidateText(row.Category, 80, "Gig.Category", true);
            Snapshot.ValidateText(row.Summary, 500, "Gig.Summary", true);
            Snapshot.ValidateText(row.Problem, 8000, "Gig.Problem", false);
            Snapshot.ValidateText(row.Outcome, 4000, "Gig.Outcome", false);
            Order(row.DisplayOrder, "Gig.DisplayOrder");
            Skills(row.Skills, $"Gig {row.Slug}");
            var numbers = new HashSet<int>();
            foreach (var step in row.Steps)
            {
                Snapshot.ValidateText(step.Title, 200, "GigStep.Title", true);
                Snapshot.ValidateText(step.Description, 4000, "GigStep.Description", false);
                if (step.StepNumber < 1 || !numbers.Add(step.StepNumber))
                    throw new InvalidDataException($"Gig {row.Slug} step numbers must be unique and start at 1.");
            }
        }
    }
}
