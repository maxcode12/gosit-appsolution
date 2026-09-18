using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenAtUtc",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HiddenByUserId",
                table: "reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenReason",
                table: "reviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportDismissedAtUtc",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportReason",
                table: "reviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportedAtUtc",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ReportedAtUtc",
                table: "reviews",
                column: "ReportedAtUtc",
                filter: "\"ReportedAtUtc\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reviews_ReportedAtUtc",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "HiddenAtUtc",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "HiddenByUserId",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "HiddenReason",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ReportDismissedAtUtc",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ReportReason",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ReportedAtUtc",
                table: "reviews");
        }
    }
}
