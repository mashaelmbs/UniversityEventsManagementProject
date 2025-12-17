using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityEventsManagementProject.Migrations
{
    public partial class AddEventSecret : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Secret",
                table: "Events",
                type: "nvarchar(450)",
                nullable: true);

            // Generate secrets for existing events first
            migrationBuilder.Sql(@"
                UPDATE Events 
                SET Secret = REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', '')
                WHERE Secret IS NULL
            ");

            // Create unique index on Secret
            migrationBuilder.CreateIndex(
                name: "IX_Events_Secret",
                table: "Events",
                column: "Secret",
                unique: true,
                filter: "[Secret] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_Secret",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Secret",
                table: "Events");
        }
    }
}
