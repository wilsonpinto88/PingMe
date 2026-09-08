using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PingMe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueBrandingAndProductPresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "Venues",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                // Existing venues must land on a usable theme, not an empty color string.
                defaultValue: "#F97316");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Venues",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EUR");

            migrationBuilder.AddColumn<string>(
                name: "HeroImageUrl",
                table: "Venues",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Venues",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Venues",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#111827");

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "Venues",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThemeMode",
                table: "Venues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Products",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Products",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "HeroImageUrl",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "ThemeMode",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Products");
        }
    }
}
