using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingAutoCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WasAutoCompleted",
                table: "care_bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WasAutoCompleted",
                table: "care_bookings");
        }
    }
}
