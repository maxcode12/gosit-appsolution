using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HoldPayoutsForDisputeWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*
             * Existing payouts default to DateTime.MinValue, which makes them immediately
             * sendable — deliberately. Anything already queued when this deployed was
             * already going to be sent on the next sweep, and retroactively holding it
             * would delay money for work that was finished under the old rule.
             *
             * Everything created afterwards gets a real date from the booking's dispute
             * window.
             */
            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableAtUtc",
                table: "payouts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsHeldForDispute",
                table: "payouts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableAtUtc",
                table: "payouts");

            migrationBuilder.DropColumn(
                name: "IsHeldForDispute",
                table: "payouts");
        }
    }
}
