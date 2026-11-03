using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace NTSW_CENOPS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261015190000_AddCalendarEvents")]
public partial class AddCalendarEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CalendarEvents",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Repeat = table.Column<byte>(type: "tinyint", nullable: false),
                CalendarBasis = table.Column<byte>(type: "tinyint", nullable: false),
                Interval = table.Column<int>(type: "int", nullable: false),
                WeekdaysMask = table.Column<int>(type: "int", nullable: false),
                MonthDays = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Months = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                RepeatUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedById = table.Column<int>(type: "int", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                table.ForeignKey("FK_CalendarEvents_AspNetUsers_CreatedById", x => x.CreatedById,
                    "AspNetUsers", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_CalendarEvents_CreatedById", "CalendarEvents", "CreatedById");
        migrationBuilder.CreateIndex("IX_CalendarEvents_IsDeleted_StartsAt", "CalendarEvents", new[] { "IsDeleted", "StartsAt" });

        // Grant the new feature to existing built-in roles; not to unknown/custom roles.
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Accesses] WHERE [Key] = N'team.events.view' AND [IsDeleted] = 0)
    INSERT INTO [Accesses] ([Key], [Name], [Description], [IsDeleted])
    VALUES (N'team.events.view', N'تقویم و رویدادها', N'مشاهده و ثبت رویدادهای تیم', 0);
INSERT INTO [RoleAccesses] ([RoleId], [AccessId])
SELECT r.[Id], a.[Id] FROM [AspNetRoles] r CROSS JOIN [Accesses] a
WHERE r.[NormalizedName] IN (N'USER', N'ADMIN', N'OWNER') AND r.[IsDeleted] = 0
  AND a.[Key] = N'team.events.view' AND a.[IsDeleted] = 0
  AND NOT EXISTS (SELECT 1 FROM [RoleAccesses] ra WHERE ra.[RoleId] = r.[Id] AND ra.[AccessId] = a.[Id]);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CalendarEvents");
        migrationBuilder.Sql(@"
DELETE ra FROM [RoleAccesses] ra INNER JOIN [Accesses] a ON ra.[AccessId] = a.[Id]
WHERE a.[Key] = N'team.events.view';
DELETE FROM [Accesses] WHERE [Key] = N'team.events.view';");
    }
}
