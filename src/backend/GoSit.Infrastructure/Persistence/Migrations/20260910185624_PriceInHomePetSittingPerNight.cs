using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PriceInHomePetSittingPerNight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The seeder only inserts services that are missing, so an existing catalogue
            // keeps in-home sitting hourly unless it is switched here. Bookings snapshot their
            // pricing unit, so ones already made keep the price they were made at.
            migrationBuilder.Sql(
                """UPDATE care_services SET "PricingUnit" = 2 WHERE "Code" = 'IN_HOME_PET_SITTING';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE care_services SET "PricingUnit" = 1 WHERE "Code" = 'IN_HOME_PET_SITTING';""");
        }
    }
}
