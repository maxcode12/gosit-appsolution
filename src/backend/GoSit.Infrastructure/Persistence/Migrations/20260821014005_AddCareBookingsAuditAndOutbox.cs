using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareBookingsAuditAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "care_bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CareServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_province = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    address_postal_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    address_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    address_latitude = table.Column<double>(type: "double precision", nullable: true),
                    address_longitude = table.Column<double>(type: "double precision", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HourlyRateSnapshot = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EstimatedTotal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ActualTotal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CheckedInAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedOutAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<int>(type: "integer", nullable: true),
                    IsLateCancellation = table.Column<bool>(type: "boolean", nullable: false),
                    ResolutionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_care_bookings_care_requester_profiles_CareRequesterProfileId",
                        column: x => x.CareRequesterProfileId,
                        principalTable: "care_requester_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_care_bookings_care_services_CareServiceId",
                        column: x => x.CareServiceId,
                        principalTable: "care_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_care_bookings_caregiver_profiles_CaregiverProfileId",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "booking_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareBookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRecipientId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_recipients_care_bookings_CareBookingId",
                        column: x => x.CareBookingId,
                        principalTable: "care_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_recipients_care_recipients_CareRecipientId",
                        column: x => x.CareRecipientId,
                        principalTable: "care_recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ActorUserId",
                table: "audit_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_EntityType_EntityId_OccurredAtUtc",
                table: "audit_events",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_recipients_CareBookingId_CareRecipientId",
                table: "booking_recipients",
                columns: new[] { "CareBookingId", "CareRecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_recipients_CareRecipientId",
                table: "booking_recipients",
                column: "CareRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_care_bookings_address_city",
                table: "care_bookings",
                column: "address_city");

            migrationBuilder.CreateIndex(
                name: "IX_care_bookings_address_postal_code",
                table: "care_bookings",
                column: "address_postal_code");

            migrationBuilder.CreateIndex(
                name: "IX_care_bookings_CaregiverProfileId_StartAtUtc",
                table: "care_bookings",
                columns: new[] { "CaregiverProfileId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_care_bookings_CareRequesterProfileId_StartAtUtc",
                table: "care_bookings",
                columns: new[] { "CareRequesterProfileId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_care_bookings_CareServiceId",
                table: "care_bookings",
                column: "CareServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_care_bookings_Status_StartAtUtc",
                table: "care_bookings",
                columns: new[] { "Status", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_OccurredAtUtc",
                table: "outbox_messages",
                column: "OccurredAtUtc",
                filter: "\"ProcessedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "booking_recipients");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "care_bookings");
        }
    }
}
