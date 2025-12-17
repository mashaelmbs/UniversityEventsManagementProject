using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityEventsManagementProject.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusToClubMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ClubMembers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ClubMembers");
        }
    }
}
