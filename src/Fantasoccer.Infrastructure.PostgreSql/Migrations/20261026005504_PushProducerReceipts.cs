using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PushProducerReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QueuedAtUtc",
                table: "MarketTradeEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PushFormationChangeReceipts",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    ChangeOrdinal = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushFormationChangeReceipts", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Day, x.ChangeOrdinal });
                    table.ForeignKey(
                        name: "FK_PushFormationChangeReceipts_FantasyTeamSeasons_GroupId_Bask~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushFormationChangeReceipts");

            migrationBuilder.DropColumn(
                name: "QueuedAtUtc",
                table: "MarketTradeEvents");
        }
    }
}
