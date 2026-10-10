using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Farm.Data.Migrations.Portfolio
{
    /// <inheritdoc />
    public partial class AddGigsAndSourceDocId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceDocId",
                table: "PortfolioProjects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDocId",
                table: "PortfolioProfiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDocId",
                table: "PortfolioExperiences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GigProblems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceDocId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Problem = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SolvedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigProblems", x => x.Id);
                    table.CheckConstraint("CK_GigProblems_DisplayOrder", "\"DisplayOrder\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "GigProblemSkills",
                columns: table => new
                {
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigProblemSkills", x => new { x.ProblemId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_GigProblemSkills_GigProblems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "GigProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GigProblemSkills_PortfolioSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "PortfolioSkills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GigSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigSteps", x => x.Id);
                    table.CheckConstraint("CK_GigSteps_StepNumber", "\"StepNumber\" >= 1");
                    table.ForeignKey(
                        name: "FK_GigSteps_GigProblems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "GigProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProjects_SourceDocId",
                table: "PortfolioProjects",
                column: "SourceDocId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProfiles_SourceDocId",
                table: "PortfolioProfiles",
                column: "SourceDocId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioExperiences_SourceDocId",
                table: "PortfolioExperiences",
                column: "SourceDocId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigProblems_Slug",
                table: "GigProblems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigProblems_SourceDocId",
                table: "GigProblems",
                column: "SourceDocId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigProblemSkills_SkillId",
                table: "GigProblemSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_GigSteps_ProblemId_StepNumber",
                table: "GigSteps",
                columns: new[] { "ProblemId", "StepNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GigProblemSkills");

            migrationBuilder.DropTable(
                name: "GigSteps");

            migrationBuilder.DropTable(
                name: "GigProblems");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioProjects_SourceDocId",
                table: "PortfolioProjects");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioProfiles_SourceDocId",
                table: "PortfolioProfiles");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioExperiences_SourceDocId",
                table: "PortfolioExperiences");

            migrationBuilder.DropColumn(
                name: "SourceDocId",
                table: "PortfolioProjects");

            migrationBuilder.DropColumn(
                name: "SourceDocId",
                table: "PortfolioProfiles");

            migrationBuilder.DropColumn(
                name: "SourceDocId",
                table: "PortfolioExperiences");
        }
    }
}
