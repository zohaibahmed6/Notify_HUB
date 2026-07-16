using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateCommunicationModeAndBookmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommunicationMode",
                table: "message_templates",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sms")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "message_template_bookmarks",
                columns: table => new
                {
                    BookmarksId = table.Column<long>(type: "bigint", nullable: false),
                    TemplatesId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_template_bookmarks", x => new { x.BookmarksId, x.TemplatesId });
                    table.ForeignKey(
                        name: "FK_message_template_bookmarks_bookmarks_BookmarksId",
                        column: x => x.BookmarksId,
                        principalTable: "bookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_message_template_bookmarks_message_templates_TemplatesId",
                        column: x => x.TemplatesId,
                        principalTable: "message_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_message_template_bookmarks_TemplatesId",
                table: "message_template_bookmarks",
                column: "TemplatesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "message_template_bookmarks");

            migrationBuilder.DropColumn(
                name: "CommunicationMode",
                table: "message_templates");
        }
    }
}
