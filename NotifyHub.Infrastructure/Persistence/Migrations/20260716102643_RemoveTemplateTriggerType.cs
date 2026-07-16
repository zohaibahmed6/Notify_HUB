using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTemplateTriggerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "message_templates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "message_templates",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
