using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewsAndTrustAndSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incident_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CareBookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incident_reports_users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_reports_users_SubjectUserId",
                        column: x => x.SubjectUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_pairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareBookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_pairs", x => x.Id);
                    table.UniqueConstraint("AK_review_pairs_CareBookingId", x => x.CareBookingId);
                    table.ForeignKey(
                        name: "FK_review_pairs_care_bookings_CareBookingId",
                        column: x => x.CareBookingId,
                        principalTable: "care_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "verification_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_verification_cases_caregiver_profiles_CaregiverProfileId",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareBookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectIsCaregiver = table.Column<bool>(type: "boolean", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reviews_review_pairs_CareBookingId",
                        column: x => x.CareBookingId,
                        principalTable: "review_pairs",
                        principalColumn: "CareBookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incident_reports_ReportedByUserId",
                table: "incident_reports",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_reports_Status_Severity_CreatedAtUtc",
                table: "incident_reports",
                columns: new[] { "Status", "Severity", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_reports_SubjectUserId",
                table: "incident_reports",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_review_pairs_CareBookingId",
                table: "review_pairs",
                column: "CareBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_pairs_CompletedAtUtc",
                table: "review_pairs",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_CareBookingId_AuthorProfileId",
                table: "reviews",
                columns: new[] { "CareBookingId", "AuthorProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_SubjectProfileId_Published",
                table: "reviews",
                column: "SubjectProfileId",
                filter: "\"PublishedAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_verification_cases_CaregiverProfileId_CheckType",
                table: "verification_cases",
                columns: new[] { "CaregiverProfileId", "CheckType" });

            migrationBuilder.CreateIndex(
                name: "IX_verification_cases_Status_SubmittedAtUtc",
                table: "verification_cases",
                columns: new[] { "Status", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_reports");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "verification_cases");

            migrationBuilder.DropTable(
                name: "review_pairs");
        }
    }
}
