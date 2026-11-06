using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoSit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Until now the only way to get a staff account was the bootstrap setting, so every
            // existing administrator is a bootstrap one. They become super administrators;
            // staff invited from here on are ordinary.
            migrationBuilder.Sql("UPDATE users SET \"IsSuperAdmin\" = TRUE WHERE \"Role\" = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "users");
        }
    }
}
