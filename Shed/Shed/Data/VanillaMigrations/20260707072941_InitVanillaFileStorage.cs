using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shed.Data.VanillaMigrations
{
    /// <inheritdoc />
    public partial class InitVanillaFileStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessKeys",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FilePathWildCards = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    CanRead = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CanWrite = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false, defaultValue: "full access"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessKeys", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Acls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ActualPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserFeatureFlags",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Feature = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFeatureFlags", x => new { x.UserId, x.Feature });
                });

            migrationBuilder.CreateTable(
                name: "UserHiddenSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TranslatedText = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ThumbnailLogoUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    FontStyle = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserHiddenSettings", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessKeys_UserId",
                table: "AccessKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Acls_FileItemId",
                table: "Acls",
                column: "FileItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FileItems_Path",
                table: "FileItems",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessKeys");

            migrationBuilder.DropTable(
                name: "Acls");

            migrationBuilder.DropTable(
                name: "FileItems");

            migrationBuilder.DropTable(
                name: "UserFeatureFlags");

            migrationBuilder.DropTable(
                name: "UserHiddenSettings");
        }
    }
}


