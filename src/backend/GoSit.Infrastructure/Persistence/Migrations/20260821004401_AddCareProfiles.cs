using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "care_requester_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_province = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    address_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    address_latitude = table.Column<double>(type: "double precision", nullable: true),
                    address_longitude = table.Column<double>(type: "double precision", nullable: true),
                    AboutMe = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_requester_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "caregiver_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_province = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    address_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    address_latitude = table.Column<double>(type: "double precision", nullable: true),
                    address_longitude = table.Column<double>(type: "double precision", nullable: true),
                    ServiceRadiusKm = table.Column<int>(type: "integer", nullable: false),
                    VerificationStatus = table.Column<int>(type: "integer", nullable: false),
                    AcceptingWork = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caregiver_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "certifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequiresExpiry = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "care_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    RecipientType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_care_recipients_care_requester_profiles_CareRequesterProfil~",
                        column: x => x.CareRequesterProfileId,
                        principalTable: "care_requester_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "emergency_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Relationship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_emergency_contacts_care_requester_profiles_CareRequesterPro~",
                        column: x => x.CareRequesterProfileId,
                        principalTable: "care_requester_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "availability_slots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_availability_slots_caregiver_profiles_CaregiverProfileId",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "caregiver_certifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caregiver_certifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_caregiver_certifications_caregiver_profiles_CaregiverProfil~",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_caregiver_certifications_certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "care_needs_profiles",
                columns: table => new
                {
                    CareRecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    MobilityNeeds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CommunicationStyle = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SensoryNeeds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BehaviourSupportNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DietaryRestrictions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Allergies = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ToiletingAssistance = table.Column<bool>(type: "boolean", nullable: false),
                    FeedingAssistance = table.Column<bool>(type: "boolean", nullable: false),
                    RoutinePreferences = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Triggers = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CalmingStrategies = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EmergencyInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    required_skills = table.Column<string[]>(type: "character varying(50)[]", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_needs_profiles", x => x.CareRecipientId);
                    table.ForeignKey(
                        name: "FK_care_needs_profiles_care_recipients_CareRecipientId",
                        column: x => x.CareRecipientId,
                        principalTable: "care_recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "certifications",
                columns: new[] { "Id", "Code", "IsActive", "Name", "RequiresExpiry" },
                values: new object[,]
                {
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000001"), "CPR", true, "CPR Certification", true },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000002"), "FIRST_AID", true, "Standard First Aid", true },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000003"), "POLICE_CHECK", true, "Police Record Check", true },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000004"), "VULNERABLE_SECTOR", true, "Vulnerable Sector Check", true },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000005"), "ECE", true, "Early Childhood Education", false },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000006"), "PSW", true, "Personal Support Worker", false },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000007"), "DEMENTIA_CARE", true, "Dementia Care Training", false },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000008"), "AUTISM_SUPPORT", true, "Autism Support Training", false },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000009"), "MOBILITY_SUPPORT", true, "Safe Transfer and Mobility", false },
                    { new Guid("0f8f2f4a-1c1e-4f1a-9a01-00000000000a"), "BEHAVIOUR_SUPPORT", true, "Behavioural Support Training", false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_availability_slots_CaregiverProfileId_Day",
                table: "availability_slots",
                columns: new[] { "CaregiverProfileId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_care_recipients_CareRequesterProfileId",
                table: "care_recipients",
                column: "CareRequesterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_care_requester_profiles_address_city",
                table: "care_requester_profiles",
                column: "address_city");

            migrationBuilder.CreateIndex(
                name: "IX_care_requester_profiles_address_postal_code",
                table: "care_requester_profiles",
                column: "address_postal_code");

            migrationBuilder.CreateIndex(
                name: "IX_care_requester_profiles_UserId",
                table: "care_requester_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_certifications_CaregiverProfileId",
                table: "caregiver_certifications",
                column: "CaregiverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_certifications_CertificationId",
                table: "caregiver_certifications",
                column: "CertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_certifications_Status_ExpiresOn",
                table: "caregiver_certifications",
                columns: new[] { "Status", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_profiles_address_city",
                table: "caregiver_profiles",
                column: "address_city");

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_profiles_address_postal_code",
                table: "caregiver_profiles",
                column: "address_postal_code");

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_profiles_HourlyRate",
                table: "caregiver_profiles",
                column: "HourlyRate");

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_profiles_UserId",
                table: "caregiver_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_profiles_VerificationStatus_AcceptingWork",
                table: "caregiver_profiles",
                columns: new[] { "VerificationStatus", "AcceptingWork" });

            migrationBuilder.CreateIndex(
                name: "IX_certifications_Code",
                table: "certifications",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_emergency_contacts_CareRequesterProfileId",
                table: "emergency_contacts",
                column: "CareRequesterProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availability_slots");

            migrationBuilder.DropTable(
                name: "care_needs_profiles");

            migrationBuilder.DropTable(
                name: "caregiver_certifications");

            migrationBuilder.DropTable(
                name: "emergency_contacts");

            migrationBuilder.DropTable(
                name: "care_recipients");

            migrationBuilder.DropTable(
                name: "caregiver_profiles");

            migrationBuilder.DropTable(
                name: "certifications");

            migrationBuilder.DropTable(
                name: "care_requester_profiles");
        }
    }
}
