using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm.Mind.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNoteConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoteConnections",
                schema: "mind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoteConnections",
                schema: "mind",
                columns: table => new
                {
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkspaceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteConnections", x => new { x.Subject, x.WorkspaceId });
                });
        }
    }
}
