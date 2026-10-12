using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm.Mind.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpUsageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpUsage",
                schema: "mind",
                columns: table => new
                {
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    ReadBytes = table.Column<long>(type: "bigint", nullable: false),
                    WriteBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpUsage", x => new { x.Subject, x.Day });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpUsage",
                schema: "mind");
        }
    }
}
