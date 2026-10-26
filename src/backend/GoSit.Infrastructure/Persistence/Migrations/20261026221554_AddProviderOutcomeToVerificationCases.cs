using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderOutcomeToVerificationCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderScore",
                table: "verification_cases",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                table: "verification_cases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderUpdatedAtUtc",
                table: "verification_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_verification_cases_ProviderReference",
                table: "verification_cases",
                column: "ProviderReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_verification_cases_ProviderReference",
                table: "verification_cases");

            migrationBuilder.DropColumn(
                name: "ProviderScore",
                table: "verification_cases");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                table: "verification_cases");

            migrationBuilder.DropColumn(
                name: "ProviderUpdatedAtUtc",
                table: "verification_cases");
        }
    }
}
