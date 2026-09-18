using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedByBookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CareRequesterLastReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CaregiverLastReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversations_care_requester_profiles_CareRequesterProfileId",
                        column: x => x.CareRequesterProfileId,
                        principalTable: "care_requester_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_conversations_caregiver_profiles_CaregiverProfileId",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentByCaregiver = table.Column<bool>(type: "boolean", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FlaggedForReview = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_CaregiverProfileId_LastMessageAtUtc",
                table: "conversations",
                columns: new[] { "CaregiverProfileId", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_CareRequesterProfileId_CaregiverProfileId",
                table: "conversations",
                columns: new[] { "CareRequesterProfileId", "CaregiverProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_CareRequesterProfileId_LastMessageAtUtc",
                table: "conversations",
                columns: new[] { "CareRequesterProfileId", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId_SentAtUtc",
                table: "messages",
                columns: new[] { "ConversationId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_SentAtUtc",
                table: "messages",
                column: "SentAtUtc",
                filter: "\"FlaggedForReview\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_CreatedAtUtc",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_Unread",
                table: "notifications",
                column: "UserId",
                filter: "\"ReadAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "conversations");
        }
    }
}
