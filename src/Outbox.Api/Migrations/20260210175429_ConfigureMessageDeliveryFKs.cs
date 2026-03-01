using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Outbox.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureMessageDeliveryFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutboxDeliveries_OutboxMessages_OutboxMessageId",
                table: "OutboxDeliveries");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboxDeliveries_OutboxMessages_OutboxMessageId",
                table: "OutboxDeliveries",
                column: "OutboxMessageId",
                principalTable: "OutboxMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutboxDeliveries_OutboxMessages_OutboxMessageId",
                table: "OutboxDeliveries");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboxDeliveries_OutboxMessages_OutboxMessageId",
                table: "OutboxDeliveries",
                column: "OutboxMessageId",
                principalTable: "OutboxMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
