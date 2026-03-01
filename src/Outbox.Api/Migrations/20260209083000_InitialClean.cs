using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Outbox.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: true),
                    SubjectKey = table.Column<string>(type: "TEXT", nullable: true),
                    SignatureVersion = table.Column<string>(type: "TEXT", nullable: true),
                    SignatureSecretId = table.Column<string>(type: "TEXT", nullable: true),
                    ExtraHeadersJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", nullable: false),
                    Secret = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: true),
                    MaxConcurrency = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxAttempts = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAttemptUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", nullable: true),
                    SubjectKey = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxDeliveries_OutboxMessages_OutboxMessageId",
                        column: x => x.OutboxMessageId,
                        principalTable: "OutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutboxDeliveries_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OutboxDeliveryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: true),
                    ConsideredRetryable = table.Column<bool>(type: "INTEGER", nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryAttempts_OutboxDeliveries_OutboxDeliveryId",
                        column: x => x.OutboxDeliveryId,
                        principalTable: "OutboxDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_OutboxDeliveryId_AttemptedAtUtc",
                table: "DeliveryAttempts",
                columns: new[] { "OutboxDeliveryId", "AttemptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_TenantId",
                table: "DeliveryAttempts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_OutboxMessageId_SubscriptionId",
                table: "OutboxDeliveries",
                columns: new[] { "OutboxMessageId", "SubscriptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_Status_NextAttemptUtc",
                table: "OutboxDeliveries",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_SubscriptionId",
                table: "OutboxDeliveries",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_TenantId_Status_NextAttemptUtc",
                table: "OutboxDeliveries",
                columns: new[] { "TenantId", "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_TenantId_SubscriptionId_Status",
                table: "OutboxDeliveries",
                columns: new[] { "TenantId", "SubscriptionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedAtUtc",
                table: "OutboxMessages",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_TenantId_CreatedAtUtc",
                table: "OutboxMessages",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_TenantId_IdempotencyKey",
                table: "OutboxMessages",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_TenantId_SubjectKey",
                table: "OutboxMessages",
                columns: new[] { "TenantId", "SubjectKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_IsActive",
                table: "Subscriptions",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryAttempts");

            migrationBuilder.DropTable(
                name: "OutboxDeliveries");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "Subscriptions");
        }
    }
}
