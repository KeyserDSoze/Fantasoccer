using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RelationalPushPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PushDeviceBindings",
                columns: table => new
                {
                    EndpointHash = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    P256dh = table.Column<string>(type: "text", nullable: false),
                    Auth = table.Column<string>(type: "text", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    DeviceProofHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDeviceBindings", x => x.EndpointHash);
                    table.ForeignKey(
                        name: "FK_PushDeviceBindings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserPushPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchEvents = table.Column<bool>(type: "boolean", nullable: false),
                    OpponentMatchEvents = table.Column<bool>(type: "boolean", nullable: false),
                    DeploymentReminder = table.Column<bool>(type: "boolean", nullable: false),
                    EndDay = table.Column<bool>(type: "boolean", nullable: false),
                    MarketEvents = table.Column<bool>(type: "boolean", nullable: false),
                    LiveChangesReminder = table.Column<bool>(type: "boolean", nullable: false),
                    LiveFormationChangeEvents = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPushPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserPushPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PushDeviceOwnershipEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointHash = table.Column<string>(type: "text", nullable: false),
                    PreviousUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BindingVersion = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDeviceOwnershipEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushDeviceOwnershipEvents_PushDeviceBindings_EndpointHash",
                        column: x => x.EndpointHash,
                        principalTable: "PushDeviceBindings",
                        principalColumn: "EndpointHash",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushDeviceOwnershipEvents_Users_PreviousUserId",
                        column: x => x.PreviousUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushDeviceOwnershipEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacyPushSubscriptions",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: true),
                    P256dh = table.Column<string>(type: "text", nullable: true),
                    Auth = table.Column<string>(type: "text", nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyPushSubscriptions", x => new { x.UserId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_LegacyPushSubscriptions_UserPushPreferences_UserId",
                        column: x => x.UserId,
                        principalTable: "UserPushPreferences",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceBindings_UserId",
                table: "PushDeviceBindings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceOwnershipEvents_EndpointHash",
                table: "PushDeviceOwnershipEvents",
                column: "EndpointHash");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceOwnershipEvents_PreviousUserId",
                table: "PushDeviceOwnershipEvents",
                column: "PreviousUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceOwnershipEvents_UserId",
                table: "PushDeviceOwnershipEvents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyPushSubscriptions");

            migrationBuilder.DropTable(
                name: "PushDeviceOwnershipEvents");

            migrationBuilder.DropTable(
                name: "UserPushPreferences");

            migrationBuilder.DropTable(
                name: "PushDeviceBindings");
        }
    }
}
