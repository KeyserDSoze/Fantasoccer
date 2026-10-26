using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RelationalMarketTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketBooks",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketBooks", x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
                    table.ForeignKey(
                        name: "FK_MarketBooks_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketTrades",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    BuyerTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerBasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerBasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerOwner = table.Column<string>(type: "text", nullable: false),
                    SellerOwner = table.Column<string>(type: "text", nullable: false),
                    MoneyFromBuyer = table.Column<int>(type: "integer", nullable: false),
                    MoneyFromSeller = table.Column<int>(type: "integer", nullable: false),
                    SellerAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LegacyCreatedUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTrades", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Id });
                    table.CheckConstraint("CK_MarketTrades_DistinctTeams", "\"BuyerTeamId\" <> \"SellerTeamId\"");
                    table.CheckConstraint("CK_MarketTrades_Status", "\"Status\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_MarketTrades_FantasyTeamSeasons_GroupId_BuyerBasketId_Seaso~",
                        columns: x => new { x.GroupId, x.BuyerBasketId, x.SeasonIndex, x.BuyerTeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTrades_FantasyTeamSeasons_GroupId_SellerBasketId_Seas~",
                        columns: x => new { x.GroupId, x.SellerBasketId, x.SeasonIndex, x.SellerTeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTrades_LeagueBasketSeasons_GroupId_LeagueId_BuyerBask~",
                        columns: x => new { x.GroupId, x.LeagueId, x.BuyerBasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTrades_LeagueBasketSeasons_GroupId_LeagueId_SellerBas~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SellerBasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTrades_MarketBooks_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "MarketBooks",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketTradePlayers",
                columns: table => new
                {
                    FromBuyer = table.Column<bool>(type: "boolean", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RosterOrdinal = table.Column<int>(type: "integer", nullable: true),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerRole = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Revenue = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTradePlayers", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TradeId, x.FromBuyer, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_MarketTradePlayers_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTradePlayers_MarketTrades_GroupId_LeagueId_SeasonInde~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TradeId },
                        principalTable: "MarketTrades",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketTradePlayers_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTradePlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketTradeVotes",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerEmail = table.Column<string>(type: "text", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTradeVotes", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TradeId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_MarketTradeVotes_FantasyTeamSeasons_GroupId_BasketId_Season~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTradeVotes_LeagueBasketSeasons_GroupId_LeagueId_Baske~",
                        columns: x => new { x.GroupId, x.LeagueId, x.BasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketTradeVotes_MarketTrades_GroupId_LeagueId_SeasonIndex_~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TradeId },
                        principalTable: "MarketTrades",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketTradeVotes_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTradePlayers_ClubId",
                table: "MarketTradePlayers",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTradePlayers_PlayerId_SeasonIndex",
                table: "MarketTradePlayers",
                columns: new[] { "PlayerId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTrades_GroupId_BuyerBasketId_SeasonIndex_BuyerTeamId",
                table: "MarketTrades",
                columns: new[] { "GroupId", "BuyerBasketId", "SeasonIndex", "BuyerTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTrades_GroupId_LeagueId_BuyerBasketId_SeasonIndex",
                table: "MarketTrades",
                columns: new[] { "GroupId", "LeagueId", "BuyerBasketId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTrades_GroupId_LeagueId_SellerBasketId_SeasonIndex",
                table: "MarketTrades",
                columns: new[] { "GroupId", "LeagueId", "SellerBasketId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTrades_GroupId_SellerBasketId_SeasonIndex_SellerTeamId",
                table: "MarketTrades",
                columns: new[] { "GroupId", "SellerBasketId", "SeasonIndex", "SellerTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTradeVotes_ActorUserId",
                table: "MarketTradeVotes",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTradeVotes_GroupId_BasketId_SeasonIndex_TeamId",
                table: "MarketTradeVotes",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTradeVotes_GroupId_LeagueId_BasketId_SeasonIndex",
                table: "MarketTradeVotes",
                columns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketTradePlayers");

            migrationBuilder.DropTable(
                name: "MarketTradeVotes");

            migrationBuilder.DropTable(
                name: "MarketTrades");

            migrationBuilder.DropTable(
                name: "MarketBooks");
        }
    }
}
