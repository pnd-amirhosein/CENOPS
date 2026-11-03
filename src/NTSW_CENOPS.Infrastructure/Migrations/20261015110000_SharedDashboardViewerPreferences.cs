using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace NTSW_CENOPS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261015110000_SharedDashboardViewerPreferences")]
public partial class SharedDashboardViewerPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DashboardViewerPreferences",
            columns: table => new
            {
                UserId = table.Column<int>(type: "int", nullable: false),
                DashboardId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DashboardViewerPreferences", x => x.UserId);
                table.ForeignKey("FK_DashboardViewerPreferences_AspNetUsers_UserId", x => x.UserId,
                    "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_DashboardViewerPreferences_UserDashboardConfigurations_DashboardId", x => x.DashboardId,
                    "UserDashboardConfigurations", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_DashboardViewerPreferences_DashboardId",
            table: "DashboardViewerPreferences", column: "DashboardId");
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "DashboardViewerPreferences");
}
