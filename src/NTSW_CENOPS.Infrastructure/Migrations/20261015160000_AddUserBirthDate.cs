using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace NTSW_CENOPS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261015160000_AddUserBirthDate")]
public partial class AddUserBirthDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<DateTime>(
        name: "BirthDate", table: "AspNetUsers", type: "date", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "BirthDate", table: "AspNetUsers");
}
