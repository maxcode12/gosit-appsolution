using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutAccountsAndRetries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "payouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAtUtc",
                table: "payouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountId",
                table: "caregiver_profiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PayoutDetailsSubmitted",
                table: "caregiver_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PayoutDisabledReason",
                table: "caregiver_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PayoutsEnabled",
                table: "caregiver_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_profiles_PayoutAccountId",
                table: "caregiver_profiles",
                column: "PayoutAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_caregiver_profiles_PayoutAccountId",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "payouts");

            migrationBuilder.DropColumn(
                name: "LastAttemptAtUtc",
                table: "payouts");

            migrationBuilder.DropColumn(
                name: "PayoutAccountId",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "PayoutDetailsSubmitted",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "PayoutDisabledReason",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "PayoutsEnabled",
                table: "caregiver_profiles");
        }
    }
}
