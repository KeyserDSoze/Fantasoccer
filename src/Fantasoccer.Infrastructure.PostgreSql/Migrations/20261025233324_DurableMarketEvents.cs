using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DurableMarketEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketTradeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ResultingStatus = table.Column<int>(type: "integer", nullable: false),
                    TradeVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTradeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketTradeEvents_MarketTrades_GroupId_LeagueId_SeasonIndex~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TradeId },
                        principalTable: "MarketTrades",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTradeEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTradeEvents_ActorUserId",
                table: "MarketTradeEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTradeEvents_CreatedAtUtc",
                table: "MarketTradeEvents",
                column: "CreatedAtUtc",
                filter: "\"DeliveredAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTradeEvents_GroupId_LeagueId_SeasonIndex_TradeId_Trad~",
                table: "MarketTradeEvents",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TradeId", "TradeVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketTradeEvents");
        }
    }
}
