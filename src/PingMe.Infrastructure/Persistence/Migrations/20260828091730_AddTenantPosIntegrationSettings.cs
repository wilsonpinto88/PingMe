using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PingMe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPosIntegrationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantPosIntegrationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    WebhookUrl = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPosIntegrationSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantPosIntegrationSettings_TenantId",
                table: "TenantPosIntegrationSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantPosIntegrationSettings");
        }
    }
}
