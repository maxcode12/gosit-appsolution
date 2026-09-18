using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAtUtc",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModeratedByUserId",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationOutcome",
                table: "messages",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModeratedAtUtc",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "ModeratedByUserId",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "ModerationOutcome",
                table: "messages");
        }
    }
}
