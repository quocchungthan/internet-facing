using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm.Data.Migrations.Portfolio
{
    /// <inheritdoc />
    public partial class AddGigBodyMarkdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyMarkdown",
                table: "GigProblems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyMarkdown",
                table: "GigProblems");
        }
    }
}
