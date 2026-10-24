using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm.Data.Migrations.Portfolio
{
    /// <inheritdoc />
    public partial class SoftDeleteRetiredPortfolioPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioProjects_Slug",
                table: "PortfolioProjects");

            migrationBuilder.DropIndex(
                name: "IX_GigProblems_Slug",
                table: "GigProblems");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "PortfolioProjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "PortfolioProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "PortfolioExperiences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "GigProblems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProjects_Slug",
                table: "PortfolioProjects",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GigProblems_Slug",
                table: "GigProblems",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioProjects_Slug",
                table: "PortfolioProjects");

            migrationBuilder.DropIndex(
                name: "IX_GigProblems_Slug",
                table: "GigProblems");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PortfolioProjects");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PortfolioProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PortfolioExperiences");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "GigProblems");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProjects_Slug",
                table: "PortfolioProjects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigProblems_Slug",
                table: "GigProblems",
                column: "Slug",
                unique: true);
        }
    }
}
