using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailAttemptCount",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAtUtc",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailSuppressed",
                table: "notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEmailAttemptAtUtc",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_pending_email",
                table: "notifications",
                column: "CreatedAtUtc",
                filter: "\"EmailSentAtUtc\" IS NULL AND NOT \"EmailSuppressed\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_pending_email",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "EmailAttemptCount",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "EmailSentAtUtc",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "EmailSuppressed",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "LastEmailAttemptAtUtc",
                table: "notifications");
        }
    }
}
