using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledSendAndSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "outbound_messages",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "outbound_messages");
        }
    }
}
