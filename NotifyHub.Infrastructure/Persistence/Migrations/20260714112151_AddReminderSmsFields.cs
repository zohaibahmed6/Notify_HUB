using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSmsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EventTime",
                table: "outbound_messages",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderExpiryOffsetMinutes",
                table: "outbound_messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderOffsetMinutes",
                table: "outbound_messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "outbound_messages",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventTime",
                table: "outbound_messages");

            migrationBuilder.DropColumn(
                name: "ReminderExpiryOffsetMinutes",
                table: "outbound_messages");

            migrationBuilder.DropColumn(
                name: "ReminderOffsetMinutes",
                table: "outbound_messages");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "outbound_messages");
        }
    }
}
