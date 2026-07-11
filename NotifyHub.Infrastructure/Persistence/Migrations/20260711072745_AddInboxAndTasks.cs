using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotifyHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxAndTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "threads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedStaffId = table.Column<long>(type: "bigint", nullable: true),
                    UnreadCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_threads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_threads_patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_threads_users_AssignedStaffId",
                        column: x => x.AssignedStaffId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inbound_messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ThreadId = table.Column<long>(type: "bigint", nullable: false),
                    Body = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inbound_messages_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ThreadId = table.Column<long>(type: "bigint", nullable: false),
                    Priority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DueAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedStaffId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalOwnerId = table.Column<long>(type: "bigint", nullable: false),
                    IsRecurring = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RecurrenceIntervalDays = table.Column<int>(type: "int", nullable: true),
                    RecurrenceEndDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RecurrenceMaxOccurrences = table.Column<int>(type: "int", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tasks_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tasks_users_AssignedStaffId",
                        column: x => x.AssignedStaffId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_users_OriginalOwnerId",
                        column: x => x.OriginalOwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_messages_ThreadId_CreatedAt",
                table: "outbound_messages",
                columns: new[] { "ThreadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_messages_ThreadId_ReceivedAt",
                table: "inbound_messages",
                columns: new[] { "ThreadId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_AssignedStaffId",
                table: "tasks",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_OriginalOwnerId",
                table: "tasks",
                column: "OriginalOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Status_DueAt",
                table: "tasks",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ThreadId",
                table: "tasks",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_threads_AssignedStaffId",
                table: "threads",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_threads_PatientId",
                table: "threads",
                column: "PatientId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_outbound_messages_threads_ThreadId",
                table: "outbound_messages",
                column: "ThreadId",
                principalTable: "threads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_outbound_messages_threads_ThreadId",
                table: "outbound_messages");

            migrationBuilder.DropTable(
                name: "inbound_messages");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "threads");

            migrationBuilder.DropIndex(
                name: "IX_outbound_messages_ThreadId_CreatedAt",
                table: "outbound_messages");
        }
    }
}
