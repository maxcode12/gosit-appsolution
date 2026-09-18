using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPetBoarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NightlyRate",
                table: "caregiver_profiles",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CareLocation",
                table: "care_services",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PricingUnit",
                table: "care_services",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CareLocation",
                table: "care_bookings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "NightlyRateSnapshot",
                table: "care_bookings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingUnit",
                table: "care_bookings",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NightlyRate",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "CareLocation",
                table: "care_services");

            migrationBuilder.DropColumn(
                name: "PricingUnit",
                table: "care_services");

            migrationBuilder.DropColumn(
                name: "CareLocation",
                table: "care_bookings");

            migrationBuilder.DropColumn(
                name: "NightlyRateSnapshot",
                table: "care_bookings");

            migrationBuilder.DropColumn(
                name: "PricingUnit",
                table: "care_bookings");
        }
    }
}
