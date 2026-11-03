using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace NTSW_CENOPS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261018090000_AddSourceIntelligenceAccess")]
public partial class AddSourceIntelligenceAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Accesses] WHERE [Key] = N'tools.source-intelligence' AND [IsDeleted] = 0)
    INSERT INTO [Accesses] ([Key], [Name], [Description], [IsDeleted])
    VALUES (N'tools.source-intelligence', N'تحلیل سورس', N'ثبت، جستجو و تحلیل پروژه‌های واردشده', 0);
INSERT INTO [RoleAccesses] ([RoleId], [AccessId])
SELECT r.[Id], a.[Id] FROM [AspNetRoles] r CROSS JOIN [Accesses] a
WHERE r.[NormalizedName] IN (N'ADMIN', N'OWNER') AND r.[IsDeleted] = 0
  AND a.[Key] = N'tools.source-intelligence' AND a.[IsDeleted] = 0
  AND NOT EXISTS (SELECT 1 FROM [RoleAccesses] ra WHERE ra.[RoleId] = r.[Id] AND ra.[AccessId] = a.[Id]);");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DELETE ra FROM [RoleAccesses] ra INNER JOIN [Accesses] a ON ra.[AccessId] = a.[Id]
WHERE a.[Key] = N'tools.source-intelligence';
DELETE FROM [Accesses] WHERE [Key] = N'tools.source-intelligence';");
    }
}
