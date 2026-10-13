using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Farm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalhostTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalhostTools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DirectUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RepositoryUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IssueUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalhostTools", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LocalhostTools",
                columns: new[] { "Id", "Category", "CreatedAtUtc", "Description", "DirectUrl", "DisplayOrder", "IsActive", "IssueUrl", "Name", "RepositoryUrl", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "SearchCollect", new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Locally collect web documentation and repo patterns for AI context retrieval", "http://localhost:5120", 1, true, "https://github.com/quocchungthan/internet-facing/issues", "Web & Repo Collector", "https://github.com/quocchungthan/internet-facing", new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "ConvertTransformCompose", new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Transforms raw note AST and Yjs payloads into structured LLM prompts and markdown", "http://localhost:5121", 2, true, "https://github.com/quocchungthan/internet-facing/issues", "Prompt & Markdown Transformer", "https://github.com/quocchungthan/internet-facing", new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "ObserveWatching", new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Observes SSE streams, worker jobs and container health telemetry", "http://localhost:5122", 3, true, "https://github.com/quocchungthan/internet-facing/issues", "Realtime Stream Watcher", "https://github.com/quocchungthan/internet-facing", new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "CalculatingAnalysing", new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Calculates activity distributions and aggregates habit metrics across platforms", "http://localhost:5123", 4, true, "https://github.com/quocchungthan/internet-facing/issues", "Habit & Click Analyzer", "https://github.com/quocchungthan/internet-facing", new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalhostTools");
        }
    }
}
