using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace NTSW_CENOPS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102180000_AddReleaseStories")]
public partial class AddReleaseStories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReleaseStories",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SourceFile = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Markdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SourceCreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ReleaseStories", x => x.Id));
        migrationBuilder.CreateIndex("IX_ReleaseStories_SourceFile", "ReleaseStories", "SourceFile", unique: true);
        migrationBuilder.CreateTable(
            name: "ReleaseStoryViews",
            columns: table => new
            {
                StoryId = table.Column<int>(type: "int", nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                ViewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReleaseStoryViews", x => new { x.StoryId, x.UserId });
                table.ForeignKey("FK_ReleaseStoryViews_ReleaseStories_StoryId", x => x.StoryId,
                    "ReleaseStories", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_ReleaseStoryViews_AspNetUsers_UserId", x => x.UserId,
                    "AspNetUsers", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_ReleaseStoryViews_UserId", "ReleaseStoryViews", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ReleaseStoryViews");
        migrationBuilder.DropTable("ReleaseStories");
    }
}
