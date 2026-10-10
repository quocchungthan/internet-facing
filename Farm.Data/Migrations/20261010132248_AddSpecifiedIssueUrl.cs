using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecifiedIssueUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecifiedIssueUrl",
                table: "WebsiteMetadata",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "WebsiteMetadata",
                keyColumn: "Id",
                keyValue: 1,
                column: "SpecifiedIssueUrl",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecifiedIssueUrl",
                table: "WebsiteMetadata");
        }
    }
}
