using Farm.Data;
using Farm.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffineSync;

// Upserts page-based portfolio rows by AFFiNE page id (SourceDocId). Entity rows are never hard
// deleted: when a changed page no longer has an entity's Kind, rows it produced under another Kind
// are soft-deleted (DeletedAt) and restored if the page gets that Kind back. Join rows and gig steps
// are replaced so they mirror the current page.
public static class PortfolioWriter
{
    public static async Task WriteAsync(PortfolioDbContext database, IReadOnlyList<SourceDocument> documents,
        CancellationToken cancellationToken)
    {
        var profiles = documents.SelectMany(d => d.Profiles).ToList();
        var projects = documents.SelectMany(d => d.Projects).ToList();
        var experiences = documents.SelectMany(d => d.Experiences).ToList();
        var skills = documents.SelectMany(d => d.Skills).ToList();
        var links = documents.SelectMany(d => d.SocialLinks).ToList();
        var gigs = documents.SelectMany(d => d.Gigs).ToList();
        var pages = documents.Where(d => d.Kind is not null).ToList();
        var hasRows = profiles.Count + projects.Count + experiences.Count + skills.Count + links.Count + gigs.Count > 0;
        if (!hasRows && pages.Count == 0) return;
        if (profiles.Count > 1) throw new InvalidDataException("Only one Profile page is supported.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync(
                """LOCK TABLE "PortfolioProfiles", "PortfolioSkills", "PortfolioProjects", "PortfolioExperiences", "GigProblems" IN SHARE ROW EXCLUSIVE MODE""",
                cancellationToken);

        await RetireOtherKindsAsync(database, pages, cancellationToken);
        if (!hasRows)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var profile = await UpsertProfileAsync(database, profiles.SingleOrDefault(), cancellationToken);
        var skillByName = await database.Skills.ToDictionaryAsync(s => s.Name, StringComparer.Ordinal, cancellationToken);
        foreach (var row in skills)
        {
            if (!skillByName.TryGetValue(row.Name, out var skill))
                skillByName[row.Name] = skill = database.Skills.Add(new PortfolioSkill { Name = row.Name }).Entity;
            skill.Category = row.Category;
        }
        await database.SaveChangesAsync(cancellationToken);

        foreach (var row in skills)
        {
            var link = await database.ProfileSkills.FindAsync([profile.Id, skillByName[row.Name].Id], cancellationToken);
            if (link is null)
                database.ProfileSkills.Add(link = new ProfileSkill { ProfileId = profile.Id, SkillId = skillByName[row.Name].Id });
            link.DisplayOrder = row.DisplayOrder;
        }
        foreach (var row in links)
        {
            var link = await database.SocialLinks.SingleOrDefaultAsync(l => l.ProfileId == profile.Id && l.Label == row.Label, cancellationToken);
            if (link is null) database.SocialLinks.Add(link = new PortfolioSocialLink { ProfileId = profile.Id, Label = row.Label });
            link.Url = row.Url;
            link.DisplayOrder = row.DisplayOrder;
        }
        foreach (var row in experiences)
        {
            var experience = await database.Experiences.IgnoreQueryFilters()
                .SingleOrDefaultAsync(e => e.SourceDocId == row.SourceDocId, cancellationToken);
            if (experience is null)
                database.Experiences.Add(experience = new PortfolioExperience { SourceDocId = row.SourceDocId });
            experience.DeletedAt = null;
            experience.ProfileId = profile.Id;
            experience.Company = row.Company;
            experience.JobTitle = row.JobTitle;
            experience.Description = row.Description;
            experience.EmploymentType = Enum.Parse<EmploymentType>(row.EmploymentType);
            experience.WorkMode = Enum.Parse<WorkMode>(row.WorkMode);
            experience.StartDate = row.StartDate;
            experience.EndDate = row.EndDate;
            experience.IsCurrent = row.IsCurrent;
            experience.DisplayOrder = row.DisplayOrder;
        }
        await database.SaveChangesAsync(cancellationToken);

        foreach (var row in projects)
        {
            var project = await database.Projects.IgnoreQueryFilters().Include(p => p.ProjectSkills)
                .FirstOrDefaultAsync(p => p.SourceDocId == row.SourceDocId, cancellationToken)
                ?? await database.Projects.Include(p => p.ProjectSkills)
                    .FirstOrDefaultAsync(p => p.SourceDocId == null && p.Slug == row.Slug, cancellationToken);
            if (project is null) database.Projects.Add(project = new PortfolioProject());
            project.SourceDocId = row.SourceDocId;
            project.DeletedAt = null;
            project.Title = row.Title;
            project.Slug = row.Slug;
            project.Summary = row.Summary;
            project.Description = row.Description;
            project.DemoUrl = row.DemoUrl;
            project.SourceUrl = row.SourceUrl;
            await database.SaveChangesAsync(cancellationToken);

            var link = await database.ProfileProjects.FindAsync([profile.Id, project.Id], cancellationToken);
            if (link is null) database.ProfileProjects.Add(link = new ProfileProject { ProfileId = profile.Id, ProjectId = project.Id });
            link.Role = row.Role;
            link.Organization = row.Organization;
            link.StartDate = row.StartDate;
            link.EndDate = row.EndDate;
            link.IsFeatured = row.IsFeatured;
            link.DisplayOrder = row.DisplayOrder;

            var wanted = await ResolveSkillsAsync(database, skillByName, row.Skills, cancellationToken);
            database.ProjectSkills.RemoveRange(project.ProjectSkills.Where(s => !wanted.Contains(s.SkillId)));
            foreach (var skillId in wanted.Where(id => project.ProjectSkills.All(s => s.SkillId != id)))
                database.ProjectSkills.Add(new ProjectSkill { ProjectId = project.Id, SkillId = skillId });
        }

        foreach (var row in gigs)
        {
            var gig = await database.GigProblems.IgnoreQueryFilters().Include(g => g.Steps).Include(g => g.Skills)
                .FirstOrDefaultAsync(g => g.SourceDocId == row.SourceDocId, cancellationToken)
                ?? await database.GigProblems.Include(g => g.Steps).Include(g => g.Skills)
                    .FirstOrDefaultAsync(g => g.SourceDocId == null && g.Slug == row.Slug, cancellationToken);
            if (gig is null) database.GigProblems.Add(gig = new GigProblem());
            gig.SourceDocId = row.SourceDocId;
            gig.DeletedAt = null;
            gig.Title = row.Title;
            gig.Slug = row.Slug;
            gig.Category = row.Category;
            gig.Summary = row.Summary;
            gig.BodyMarkdown = row.BodyMarkdown;
            gig.Problem = row.Problem;
            gig.Outcome = row.Outcome;
            gig.SolvedOn = row.SolvedOn;
            gig.DisplayOrder = row.DisplayOrder;
            await database.SaveChangesAsync(cancellationToken);

            var numbers = row.Steps.Select(s => s.StepNumber).ToHashSet();
            database.GigSteps.RemoveRange(gig.Steps.Where(s => !numbers.Contains(s.StepNumber)));
            foreach (var step in row.Steps)
            {
                var existing = gig.Steps.FirstOrDefault(s => s.StepNumber == step.StepNumber);
                if (existing is null)
                    database.GigSteps.Add(existing = new GigStep { ProblemId = gig.Id, StepNumber = step.StepNumber });
                existing.Title = step.Title;
                existing.Description = step.Description;
            }
            var wanted = await ResolveSkillsAsync(database, skillByName, row.Skills, cancellationToken);
            database.GigProblemSkills.RemoveRange(gig.Skills.Where(s => !wanted.Contains(s.SkillId)));
            foreach (var skillId in wanted.Where(id => gig.Skills.All(s => s.SkillId != id)))
                database.GigProblemSkills.Add(new GigProblemSkill { ProblemId = gig.Id, SkillId = skillId });
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<PortfolioProfile> UpsertProfileAsync(PortfolioDbContext database, ProfileRow? row,
        CancellationToken cancellationToken)
    {
        PortfolioProfile? profile = null;
        if (row is not null)
        {
            profile = await database.Profiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.SourceDocId == row.SourceDocId, cancellationToken)
                ?? await database.Profiles.OrderBy(p => p.Id).FirstOrDefaultAsync(p => p.SourceDocId == null, cancellationToken);
            if (profile is null) database.Profiles.Add(profile = new PortfolioProfile());
            profile.SourceDocId = row.SourceDocId;
            profile.DeletedAt = null;
            profile.DisplayName = row.DisplayName;
            profile.Headline = row.Headline;
            profile.Bio = row.Bio;
            profile.Location = row.Location;
            profile.ContactEmail = row.ContactEmail;
            profile.ProfileImageUrl = row.ProfileImageUrl;
            profile.ResumeUrl = row.ResumeUrl;
            await database.SaveChangesAsync(cancellationToken);
            return profile;
        }
        // Changed pages other than the profile attach to the single existing profile.
        return await database.Profiles.OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No portfolio profile exists yet; add a Profile page in AFFiNE.");
    }

    // Soft-deletes live rows a changed page produced under a Kind it no longer has. Rows of the page's
    // current Kind are left to the upsert, so an invalid (skipped) page keeps its last good row.
    private static async Task RetireOtherKindsAsync(PortfolioDbContext database, IReadOnlyList<SourceDocument> pages,
        CancellationToken cancellationToken)
    {
        string[] Except(string kind) => pages.Where(p => p.Kind != kind).Select(p => p.Id).ToArray();
        var now = DateTimeOffset.UtcNow;
        var profileIds = Except("Profile");
        foreach (var row in await database.Profiles.Where(p => profileIds.Contains(p.SourceDocId!)).ToListAsync(cancellationToken))
            row.DeletedAt = now;
        var experienceIds = Except("Experience");
        foreach (var row in await database.Experiences.Where(e => experienceIds.Contains(e.SourceDocId!)).ToListAsync(cancellationToken))
            row.DeletedAt = now;
        var projectIds = Except("Project");
        foreach (var row in await database.Projects.Include(p => p.Profiles).Include(p => p.ProjectSkills)
                     .Where(p => projectIds.Contains(p.SourceDocId!)).ToListAsync(cancellationToken))
        {
            // Join rows are rebuilt from the page if the Kind returns; removing them keeps flat endpoints consistent.
            row.DeletedAt = now;
            database.ProfileProjects.RemoveRange(row.Profiles);
            database.ProjectSkills.RemoveRange(row.ProjectSkills);
        }
        var gigIds = Except("Gig");
        foreach (var row in await database.GigProblems.Include(g => g.Steps).Include(g => g.Skills)
                     .Where(g => gigIds.Contains(g.SourceDocId!)).ToListAsync(cancellationToken))
        {
            row.DeletedAt = now;
            database.GigSteps.RemoveRange(row.Steps);
            database.GigProblemSkills.RemoveRange(row.Skills);
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task<HashSet<int>> ResolveSkillsAsync(PortfolioDbContext database,
        Dictionary<string, PortfolioSkill> skillByName, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        foreach (var name in names.Where(n => !skillByName.ContainsKey(n)))
            skillByName[name] = database.Skills.Add(new PortfolioSkill { Name = name, Category = "Uncategorized" }).Entity;
        await database.SaveChangesAsync(cancellationToken);
        return names.Select(n => skillByName[n].Id).ToHashSet();
    }
}
