using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Farm.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuraFarming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformBookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PlatformId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformBookmarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebsiteMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeveloperName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RepositoryUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebsiteMetadata", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PlatformBookmarks",
                columns: new[] { "Id", "Color", "CreatedAtUtc", "Description", "DisplayOrder", "IsActive", "Platform", "PlatformId", "Url" },
                values: new object[,]
                {
                    { 1, "#238636", new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Utc), "GitHub repositories, PRs & code contributions", 1, true, "GitHub", "quocchungthan", "https://github.com/quocchungthan" },
                    { 2, "#ff0050", new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Short-form tech demos and vibe coding feeds", 2, true, "TikTok", "quocchungthan", "https://www.tiktok.com/@quocchungthan" },
                    { 3, "#1877f2", new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Community updates and social networking", 3, true, "Facebook", "quocchungthan", "https://www.facebook.com/quocchungthan" },
                    { 4, "#e4405f", new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Dev stories, behind the scenes, and life feeds", 4, true, "Instagram", "quocchungthan", "https://www.instagram.com/quocchungthan" },
                    { 5, "#0078d4", new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Pipelines, work items, and backlog gigs", 5, true, "Azure DevOps", "quocchungthan", "https://dev.azure.com/quocchungthan" },
                    { 6, "#ea4335", new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Direct client inquiries & software gig proposals", 6, true, "Gmail", "chung@eldervibe.dev", "mailto:chung@eldervibe.dev" }
                });

            migrationBuilder.InsertData(
                table: "WebsiteMetadata",
                columns: new[] { "Id", "CreatedAtUtc", "Description", "DeveloperName", "RepositoryUrl", "Title", "UpdatedAtUtc" },
                values: new object[] { 1, new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Utc), "if you are seeking for a software developer to talk to, it's me here", "quocchungthan", "https://github.com/quocchungthan/internet-facing", "a Elder engineer learn to vibe code", new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformBookmarks");

            migrationBuilder.DropTable(
                name: "WebsiteMetadata");
        }
    }
}
