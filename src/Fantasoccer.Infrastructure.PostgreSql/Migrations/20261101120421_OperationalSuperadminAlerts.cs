using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OperationalSuperadminAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OperationalAlerts",
                table: "UserPushPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OperationalAlertsEnabledAtUtc",
                table: "UserPushPreferences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperationalFailures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalFailures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalFailures_OccurredAtUtc",
                table: "OperationalFailures",
                column: "OccurredAtUtc");
            migrationBuilder.CreateIndex(
                name: "IX_SqlJobRuns_CompletedAtUtc",
                table: "SqlJobRuns",
                column: "CompletedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_SqlJobRuns_CompletedAtUtc", table: "SqlJobRuns");
            migrationBuilder.DropTable(
                name: "OperationalFailures");

            migrationBuilder.DropColumn(
                name: "OperationalAlerts",
                table: "UserPushPreferences");

            migrationBuilder.DropColumn(
                name: "OperationalAlertsEnabledAtUtc",
                table: "UserPushPreferences");
        }
    }
}
