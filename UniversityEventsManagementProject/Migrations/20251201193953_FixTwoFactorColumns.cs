using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityEventsManagementProject.Migrations
{
    public partial class FixTwoFactorColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add TwoFactorEnabled column if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'TwoFactorEnabled')
                BEGIN
                    ALTER TABLE [Users] ADD [TwoFactorEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
                END
            ");

            // Add TwoFactorSecret column if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'TwoFactorSecret')
                BEGIN
                    ALTER TABLE [Users] ADD [TwoFactorSecret] nvarchar(max) NULL;
                END
            ");

            // Add TwoFactorBackupCodes column if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'TwoFactorBackupCodes')
                BEGIN
                    ALTER TABLE [Users] ADD [TwoFactorBackupCodes] nvarchar(max) NULL;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
