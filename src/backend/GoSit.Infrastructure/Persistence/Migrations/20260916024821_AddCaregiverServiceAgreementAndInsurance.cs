using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaregiverServiceAgreementAndInsurance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AgreementAcceptedAtUtc",
                table: "caregiver_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementAcceptedVersion",
                table: "caregiver_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementSignedName",
                table: "caregiver_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "caregiver_insurance_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoverageType = table.Column<int>(type: "integer", nullable: false),
                    Insurer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LiabilityLimit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryNoticeStage = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caregiver_insurance_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_caregiver_insurance_policies_caregiver_profiles_CaregiverPr~",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_insurance_policies_CaregiverProfileId",
                table: "caregiver_insurance_policies",
                column: "CaregiverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_insurance_policies_Status_ExpiresOn",
                table: "caregiver_insurance_policies",
                columns: new[] { "Status", "ExpiresOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "caregiver_insurance_policies");

            migrationBuilder.DropColumn(
                name: "AgreementAcceptedAtUtc",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "AgreementAcceptedVersion",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "AgreementSignedName",
                table: "caregiver_profiles");
        }
    }
}
