using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PingMe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQrCodeCodeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_Code",
                table: "QrCodes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QrCodes_Code",
                table: "QrCodes");
        }
    }
}
