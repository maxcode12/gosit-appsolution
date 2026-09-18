using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPetCare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClearedForPeopleCare",
                table: "caregiver_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Everyone verified before this migration passed the only bar there was: photo ID
            // and a vulnerable sector check. Leaving the new column at its default would
            // silently make every one of them unbookable for children and seniors.
            migrationBuilder.Sql(
                "UPDATE caregiver_profiles SET \"ClearedForPeopleCare\" = TRUE " +
                "WHERE \"VerificationStatus\"::text IN ('3', 'Verified');");

            migrationBuilder.AddColumn<int[]>(
                name: "applicable_pet_species",
                table: "care_services",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.CreateTable(
                name: "booking_pets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareBookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Species = table.Column<int>(type: "integer", nullable: false),
                    Breed = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Size = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_pets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_pets_care_bookings_CareBookingId",
                        column: x => x.CareBookingId,
                        principalTable: "care_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Species = table.Column<int>(type: "integer", nullable: false),
                    Breed = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Size = table.Column<int>(type: "integer", nullable: true),
                    FeedingInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Medication = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Behaviour = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VetClinic = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OtherNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pets_care_requester_profiles_CareRequesterProfileId",
                        column: x => x.CareRequesterProfileId,
                        principalTable: "care_requester_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_pets_CareBookingId_PetId",
                table: "booking_pets",
                columns: new[] { "CareBookingId", "PetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pets_CareRequesterProfileId",
                table: "pets",
                column: "CareRequesterProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_pets");

            migrationBuilder.DropTable(
                name: "pets");

            migrationBuilder.DropColumn(
                name: "ClearedForPeopleCare",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "applicable_pet_species",
                table: "care_services");
        }
    }
}
