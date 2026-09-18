using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaregiverExposures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "caregiver_exposures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Impressions = table.Column<int>(type: "integer", nullable: false),
                    ProfileViews = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caregiver_exposures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_caregiver_exposures_profile_date",
                table: "caregiver_exposures",
                columns: new[] { "CaregiverProfileId", "OnDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "caregiver_exposures");
        }
    }
}
