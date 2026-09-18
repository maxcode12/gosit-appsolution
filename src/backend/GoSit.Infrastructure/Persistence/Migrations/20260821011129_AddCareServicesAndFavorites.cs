using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareServicesAndFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageRating",
                table: "caregiver_profiles",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "caregiver_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "care_service_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_service_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "favorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareRequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_favorites_caregiver_profiles_CaregiverProfileId",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "care_services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CareServiceCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    required_certification_codes = table.Column<string[]>(type: "character varying(50)[]", nullable: false),
                    applicable_recipient_types = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_care_services_care_service_categories_CareServiceCategoryId",
                        column: x => x.CareServiceCategoryId,
                        principalTable: "care_service_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "caregiver_services",
                columns: table => new
                {
                    CaregiverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicesId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caregiver_services", x => new { x.CaregiverProfileId, x.ServicesId });
                    table.ForeignKey(
                        name: "FK_caregiver_services_care_services_ServicesId",
                        column: x => x.ServicesId,
                        principalTable: "care_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_caregiver_services_caregiver_profiles_CaregiverProfileId",
                        column: x => x.CaregiverProfileId,
                        principalTable: "caregiver_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_care_service_categories_Code",
                table: "care_service_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_care_services_CareServiceCategoryId",
                table: "care_services",
                column: "CareServiceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_care_services_Code",
                table: "care_services",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_caregiver_services_ServicesId",
                table: "caregiver_services",
                column: "ServicesId");

            migrationBuilder.CreateIndex(
                name: "IX_favorites_CaregiverProfileId",
                table: "favorites",
                column: "CaregiverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_favorites_CareRequesterProfileId_CaregiverProfileId",
                table: "favorites",
                columns: new[] { "CareRequesterProfileId", "CaregiverProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "caregiver_services");

            migrationBuilder.DropTable(
                name: "favorites");

            migrationBuilder.DropTable(
                name: "care_services");

            migrationBuilder.DropTable(
                name: "care_service_categories");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "caregiver_profiles");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "caregiver_profiles");
        }
    }
}
