using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace NTSW_CENOPS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261026140000_AddDatabaseInspectorAccess")]
public partial class AddDatabaseInspectorAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Accesses] WHERE [Key] = N'tools.database-inspector')
    INSERT INTO [Accesses] ([Key], [Name], [Description], [IsDeleted])
    VALUES (N'tools.database-inspector', N'Database Inspector', N'مشاهده ساختار پایگاه‌های داده خارجی', 0);
ELSE
    UPDATE [Accesses] SET [IsDeleted]=0 WHERE [Key]=N'tools.database-inspector';
INSERT INTO [RoleAccesses] ([RoleId], [AccessId])
SELECT r.[Id], a.[Id] FROM [AspNetRoles] r CROSS JOIN [Accesses] a
WHERE r.[IsDeleted]=0 AND a.[Key]=N'tools.database-inspector' AND a.[IsDeleted]=0
  AND NOT EXISTS (SELECT 1 FROM [RoleAccesses] ra WHERE ra.[RoleId]=r.[Id] AND ra.[AccessId]=a.[Id]);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DELETE ra FROM [RoleAccesses] ra INNER JOIN [Accesses] a ON a.[Id]=ra.[AccessId]
WHERE a.[Key]=N'tools.database-inspector';
DELETE FROM [Accesses] WHERE [Key]=N'tools.database-inspector';");
    }
}
