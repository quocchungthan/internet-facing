using Farm.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Farm.Data;

public class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : DbContext(options)
{
    public DbSet<PortfolioProfile> Profiles => Set<PortfolioProfile>();
    public DbSet<PortfolioProject> Projects => Set<PortfolioProject>();
    public DbSet<PortfolioExperience> Experiences => Set<PortfolioExperience>();
    public DbSet<PortfolioSkill> Skills => Set<PortfolioSkill>();
    public DbSet<PortfolioSocialLink> SocialLinks => Set<PortfolioSocialLink>();
    public DbSet<ProfileProject> ProfileProjects => Set<ProfileProject>();
    public DbSet<ProfileSkill> ProfileSkills => Set<ProfileSkill>();
    public DbSet<ProjectSkill> ProjectSkills => Set<ProjectSkill>();
    public DbSet<GigProblem> GigProblems => Set<GigProblem>();
    public DbSet<GigStep> GigSteps => Set<GigStep>();
    public DbSet<GigProblemSkill> GigProblemSkills => Set<GigProblemSkill>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PortfolioProfile>(entity =>
        {
            entity.ToTable("PortfolioProfiles");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.SourceDocId).HasMaxLength(64);
            entity.HasIndex(p => p.SourceDocId).IsUnique();
            entity.Property(p => p.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(p => p.Headline).HasMaxLength(240).IsRequired();
            entity.Property(p => p.Bio).HasMaxLength(4000);
            entity.Property(p => p.Location).HasMaxLength(160);
            entity.Property(p => p.ContactEmail).HasMaxLength(320);
            entity.Property(p => p.ProfileImageUrl).HasMaxLength(2048);
            entity.Property(p => p.AvatarSvg).HasColumnType("text");
            entity.Property(p => p.ResumeUrl).HasMaxLength(2048);
            entity.HasMany(p => p.Projects).WithOne(p => p.Profile)
                .HasForeignKey(p => p.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Experiences).WithOne(p => p.Profile)
                .HasForeignKey(p => p.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Skills).WithOne(p => p.Profile)
                .HasForeignKey(p => p.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.SocialLinks).WithOne(p => p.Profile)
                .HasForeignKey(p => p.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PortfolioProject>(entity =>
        {
            entity.ToTable("PortfolioProjects");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.SourceDocId).HasMaxLength(64);
            entity.HasIndex(p => p.SourceDocId).IsUnique();
            entity.Property(p => p.Title).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Slug).HasMaxLength(220).IsRequired();
            entity.Property(p => p.Summary).HasMaxLength(500).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(8000);
            entity.Property(p => p.DemoUrl).HasMaxLength(2048);
            entity.Property(p => p.SourceUrl).HasMaxLength(2048);
            entity.HasIndex(p => p.Slug).IsUnique();
            entity.HasMany(p => p.ProjectSkills).WithOne(p => p.Project)
                .HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProfileProject>(entity =>
        {
            entity.ToTable("ProfileProjects", table =>
            {
                table.HasCheckConstraint("CK_ProfileProjects_DateRange",
                    "\"EndDate\" IS NULL OR \"StartDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
                table.HasCheckConstraint("CK_ProfileProjects_DisplayOrder", "\"DisplayOrder\" >= 0");
            });
            entity.HasKey(p => new { p.ProfileId, p.ProjectId });
            entity.Property(p => p.Role).HasMaxLength(160);
            entity.Property(p => p.Organization).HasMaxLength(200);
            entity.Property(p => p.StartDate).HasColumnType("date");
            entity.Property(p => p.EndDate).HasColumnType("date");
            entity.HasIndex(p => new { p.ProfileId, p.IsFeatured, p.DisplayOrder });
            entity.HasOne(p => p.Project).WithMany(p => p.Profiles)
                .HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PortfolioExperience>(entity =>
        {
            entity.ToTable("PortfolioExperiences", table =>
            {
                table.HasCheckConstraint("CK_PortfolioExperiences_DateRange",
                    "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
                table.HasCheckConstraint("CK_PortfolioExperiences_CurrentHasNoEndDate",
                    "NOT \"IsCurrent\" OR \"EndDate\" IS NULL");
                table.HasCheckConstraint("CK_PortfolioExperiences_DisplayOrder", "\"DisplayOrder\" >= 0");
            });
            entity.HasKey(p => p.Id);
            entity.Property(p => p.SourceDocId).HasMaxLength(64);
            entity.HasIndex(p => p.SourceDocId).IsUnique();
            entity.Property(p => p.Company).HasMaxLength(200).IsRequired();
            entity.Property(p => p.JobTitle).HasMaxLength(160).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(4000);
            entity.Property(p => p.EmploymentType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(p => p.WorkMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(p => p.StartDate).HasColumnType("date").IsRequired();
            entity.Property(p => p.EndDate).HasColumnType("date");
            entity.HasIndex(p => new { p.ProfileId, p.DisplayOrder });
        });

        modelBuilder.Entity<PortfolioSkill>(entity =>
        {
            entity.ToTable("PortfolioSkills");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Category).HasMaxLength(80).IsRequired();
            entity.HasIndex(p => p.Name).IsUnique();
            entity.HasMany(p => p.Profiles).WithOne(p => p.Skill)
                .HasForeignKey(p => p.SkillId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProfileSkill>(entity =>
        {
            entity.ToTable("ProfileSkills", table =>
                table.HasCheckConstraint("CK_ProfileSkills_DisplayOrder", "\"DisplayOrder\" >= 0"));
            entity.HasKey(p => new { p.ProfileId, p.SkillId });
            entity.HasIndex(p => new { p.ProfileId, p.DisplayOrder });
        });

        modelBuilder.Entity<ProjectSkill>(entity =>
        {
            entity.ToTable("ProjectSkills");
            entity.HasKey(p => new { p.ProjectId, p.SkillId });
            entity.HasOne(p => p.Skill).WithMany(p => p.ProjectSkills)
                .HasForeignKey(p => p.SkillId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PortfolioSocialLink>(entity =>
        {
            entity.ToTable("PortfolioSocialLinks", table =>
                table.HasCheckConstraint("CK_PortfolioSocialLinks_DisplayOrder", "\"DisplayOrder\" >= 0"));
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Label).HasMaxLength(80).IsRequired();
            entity.Property(p => p.Url).HasMaxLength(2048).IsRequired();
            entity.HasIndex(p => new { p.ProfileId, p.Label }).IsUnique();
            entity.HasIndex(p => new { p.ProfileId, p.DisplayOrder });
        });

        modelBuilder.Entity<GigProblem>(entity =>
        {
            entity.ToTable("GigProblems", table =>
                table.HasCheckConstraint("CK_GigProblems_DisplayOrder", "\"DisplayOrder\" >= 0"));
            entity.HasKey(p => p.Id);
            entity.Property(p => p.SourceDocId).HasMaxLength(64);
            entity.HasIndex(p => p.SourceDocId).IsUnique();
            entity.Property(p => p.Title).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Slug).HasMaxLength(220).IsRequired();
            entity.Property(p => p.Category).HasMaxLength(80).IsRequired();
            entity.Property(p => p.Summary).HasMaxLength(500).IsRequired();
            entity.Property(p => p.Problem).HasMaxLength(8000);
            entity.Property(p => p.Outcome).HasMaxLength(4000);
            entity.Property(p => p.SolvedOn).HasColumnType("date");
            entity.HasIndex(p => p.Slug).IsUnique();
            entity.HasMany(p => p.Steps).WithOne(p => p.Problem)
                .HasForeignKey(p => p.ProblemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Skills).WithOne(p => p.Problem)
                .HasForeignKey(p => p.ProblemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GigStep>(entity =>
        {
            entity.ToTable("GigSteps", table =>
                table.HasCheckConstraint("CK_GigSteps_StepNumber", "\"StepNumber\" >= 1"));
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(4000);
            entity.HasIndex(p => new { p.ProblemId, p.StepNumber }).IsUnique();
        });

        modelBuilder.Entity<GigProblemSkill>(entity =>
        {
            entity.ToTable("GigProblemSkills");
            entity.HasKey(p => new { p.ProblemId, p.SkillId });
            entity.HasOne(p => p.Skill).WithMany(p => p.GigProblems)
                .HasForeignKey(p => p.SkillId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
