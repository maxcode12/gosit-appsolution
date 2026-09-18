using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "interviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByCaregiver = table.Column<bool>(type: "boolean", nullable: true),
                    CareRequesterConsentedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CaregiverConsentedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CareRequesterJoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CaregiverJoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordingLeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordingsOnHold = table.Column<bool>(type: "boolean", nullable: false),
                    RecordingHoldReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordingHeldByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordingHeldAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_interviews_care_requester_profiles_CareRequesterProfileId",
                        column: x => x.CareRequesterProfileId,
                        principalTable: "care_requester_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_interviews_caregiver_profiles_CaregiverProfileId",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_interviews_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interview_recordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderRecordingId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeleteAfterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_recordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_interview_recordings_interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_interview_recordings_DeleteAfterUtc",
                table: "interview_recordings",
                column: "DeleteAfterUtc",
                filter: "\"StorageKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_interview_recordings_InterviewId",
                table: "interview_recordings",
                column: "InterviewId");

            migrationBuilder.CreateIndex(
                name: "IX_interview_recordings_ProviderRecordingId",
                table: "interview_recordings",
                column: "ProviderRecordingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_interviews_CaregiverProfileId_ScheduledStartUtc",
                table: "interviews",
                columns: new[] { "CaregiverProfileId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_interviews_CareRequesterProfileId_ScheduledStartUtc",
                table: "interviews",
                columns: new[] { "CareRequesterProfileId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_interviews_ConversationId",
                table: "interviews",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interview_recordings");

            migrationBuilder.DropTable(
                name: "interviews");
        }
    }
}
