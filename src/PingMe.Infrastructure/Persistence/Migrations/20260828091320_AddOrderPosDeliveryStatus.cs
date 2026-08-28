using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PingMe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPosDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PosDeliveryStatus",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PosDeliveryStatus",
                table: "Orders");
        }
    }
}
