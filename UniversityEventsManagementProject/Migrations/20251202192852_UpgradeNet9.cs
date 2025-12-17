using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityEventsManagementProject.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeNet9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing index to allow altering column type
            migrationBuilder.DropIndex(
                name: "IX_Events_Secret",
                table: "Events");

            migrationBuilder.AlterColumn<string>(
                name: "Secret",
                table: "Events",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_Secret",
                table: "Events",
                column: "Secret",
                unique: true,
                filter: "[Secret] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_Secret",
                table: "Events");

            migrationBuilder.AlterColumn<string>(
                name: "Secret",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
