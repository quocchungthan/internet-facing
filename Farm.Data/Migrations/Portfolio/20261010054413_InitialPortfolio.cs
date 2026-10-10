using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Farm.Data.Migrations.Portfolio
{
    /// <inheritdoc />
    public partial class InitialPortfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Headline = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Bio = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ProfileImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AvatarSvg = table.Column<string>(type: "text", nullable: true),
                    ResumeUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    DemoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSkills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioExperiences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<int>(type: "integer", nullable: false),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WorkMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioExperiences", x => x.Id);
                    table.CheckConstraint("CK_PortfolioExperiences_CurrentHasNoEndDate", "NOT \"IsCurrent\" OR \"EndDate\" IS NULL");
                    table.CheckConstraint("CK_PortfolioExperiences_DateRange", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("CK_PortfolioExperiences_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_PortfolioExperiences_PortfolioProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PortfolioProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSocialLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSocialLinks", x => x.Id);
                    table.CheckConstraint("CK_PortfolioSocialLinks_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_PortfolioSocialLinks_PortfolioProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PortfolioProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileProjects",
                columns: table => new
                {
                    ProfileId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileProjects", x => new { x.ProfileId, x.ProjectId });
                    table.CheckConstraint("CK_ProfileProjects_DateRange", "\"EndDate\" IS NULL OR \"StartDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("CK_ProfileProjects_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProfileProjects_PortfolioProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PortfolioProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileProjects_PortfolioProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "PortfolioProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileSkills",
                columns: table => new
                {
                    ProfileId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileSkills", x => new { x.ProfileId, x.SkillId });
                    table.CheckConstraint("CK_ProfileSkills_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProfileSkills_PortfolioProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PortfolioProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileSkills_PortfolioSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "PortfolioSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSkills",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSkills", x => new { x.ProjectId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_ProjectSkills_PortfolioProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "PortfolioProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSkills_PortfolioSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "PortfolioSkills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioExperiences_ProfileId_DisplayOrder",
                table: "PortfolioExperiences",
                columns: new[] { "ProfileId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProjects_Slug",
                table: "PortfolioProjects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSkills_Name",
                table: "PortfolioSkills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSocialLinks_ProfileId_DisplayOrder",
                table: "PortfolioSocialLinks",
                columns: new[] { "ProfileId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSocialLinks_ProfileId_Label",
                table: "PortfolioSocialLinks",
                columns: new[] { "ProfileId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileProjects_ProfileId_IsFeatured_DisplayOrder",
                table: "ProfileProjects",
                columns: new[] { "ProfileId", "IsFeatured", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileProjects_ProjectId",
                table: "ProfileProjects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileSkills_ProfileId_DisplayOrder",
                table: "ProfileSkills",
                columns: new[] { "ProfileId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileSkills_SkillId",
                table: "ProfileSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSkills_SkillId",
                table: "ProjectSkills",
                column: "SkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioExperiences");

            migrationBuilder.DropTable(
                name: "PortfolioSocialLinks");

            migrationBuilder.DropTable(
                name: "ProfileProjects");

            migrationBuilder.DropTable(
                name: "ProfileSkills");

            migrationBuilder.DropTable(
                name: "ProjectSkills");

            migrationBuilder.DropTable(
                name: "PortfolioProfiles");

            migrationBuilder.DropTable(
                name: "PortfolioProjects");

            migrationBuilder.DropTable(
                name: "PortfolioSkills");
        }
    }
}
