using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArrivalEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CheckInDistanceMetres",
                table: "care_bookings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckInVerification",
                table: "care_bookings",
                type: "integer",
                nullable: false,
                // ArrivalVerification.NotProvided. EF defaults a new enum column to 0, which
                // is not a member — every booking that already existed would have
                // reported an invalid status through the API.
                defaultValue: 1);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutDistanceMetres",
                table: "care_bookings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckOutVerification",
                table: "care_bookings",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInDistanceMetres",
                table: "care_bookings");

            migrationBuilder.DropColumn(
                name: "CheckInVerification",
                table: "care_bookings");

            migrationBuilder.DropColumn(
                name: "CheckOutDistanceMetres",
                table: "care_bookings");

            migrationBuilder.DropColumn(
                name: "CheckOutVerification",
                table: "care_bookings");
        }
    }
}
