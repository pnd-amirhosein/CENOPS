using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace NTSW_CENOPS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102160000_AddChatMessageAttachments")]
public partial class AddChatMessageAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChatMessageAttachments",
            columns: table => new
            {
                MessageId = table.Column<long>(type: "bigint", nullable: false),
                FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Position = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessageAttachments", x => new { x.MessageId, x.FileId });
                table.ForeignKey("FK_ChatMessageAttachments_ChatMessages_MessageId", x => x.MessageId,
                    "ChatMessages", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_ChatMessageAttachments_FileId", "ChatMessageAttachments", "FileId");
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("ChatMessageAttachments");
}
