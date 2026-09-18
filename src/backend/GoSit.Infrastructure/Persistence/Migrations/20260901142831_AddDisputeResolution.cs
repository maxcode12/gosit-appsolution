using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisputeResolutionNotes",
                table: "care_bookings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputeResolvedAtUtc",
                table: "care_bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputedAtUtc",
                table: "care_bookings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisputeResolutionNotes",
                table: "care_bookings");

            migrationBuilder.DropColumn(
                name: "DisputeResolvedAtUtc",
                table: "care_bookings");

            migrationBuilder.DropColumn(
                name: "DisputedAtUtc",
                table: "care_bookings");
        }
    }
}
