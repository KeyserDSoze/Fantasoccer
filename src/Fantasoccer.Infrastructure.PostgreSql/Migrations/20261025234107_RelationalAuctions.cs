using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RelationalAuctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuctionSessions",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorEmail = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LegacyCreatedUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SecondsPerAuction = table.Column<int>(type: "integer", nullable: false),
                    CurrentRole = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionSessions", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Id });
                    table.ForeignKey(
                        name: "FK_AuctionSessions_GroupMemberships_GroupId_CreatorUserId",
                        columns: x => new { x.GroupId, x.CreatorUserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionSessions_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionCurrentLots",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerRole = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimerStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LegacyStartedUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LegacyTimerStartedUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WinningTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    WinningBasketId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerEmail = table.Column<string>(type: "text", nullable: true),
                    SubstitutedPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Statistics_Summatory = table.Column<float>(type: "real", nullable: false),
                    Statistics_FantaSummatory = table.Column<float>(type: "real", nullable: false),
                    Statistics_WithVote = table.Column<int>(type: "integer", nullable: false),
                    Statistics_WithoutVote = table.Column<int>(type: "integer", nullable: false),
                    Statistics_NoPlayed = table.Column<int>(type: "integer", nullable: false),
                    Statistics_WithSpecial = table.Column<int>(type: "integer", nullable: false),
                    Statistics_Goals = table.Column<int>(type: "integer", nullable: false),
                    Statistics_Penalties = table.Column<int>(type: "integer", nullable: false),
                    Statistics_Assists = table.Column<int>(type: "integer", nullable: false),
                    Statistics_StoppedPenalties = table.Column<int>(type: "integer", nullable: false),
                    Statistics_SufferedGoals = table.Column<int>(type: "integer", nullable: false),
                    Statistics_WrongedPenalties = table.Column<int>(type: "integer", nullable: false),
                    Statistics_OwnGoals = table.Column<int>(type: "integer", nullable: false),
                    Statistics_YellowCards = table.Column<int>(type: "integer", nullable: false),
                    Statistics_RedCards = table.Column<int>(type: "integer", nullable: false),
                    Statistics_EnoughVotes = table.Column<int>(type: "integer", nullable: false),
                    Statistics_ManOfTheMatch = table.Column<int>(type: "integer", nullable: false),
                    Statistics_Injured = table.Column<int>(type: "integer", nullable: false),
                    Statistics_HasDescription = table.Column<bool>(type: "boolean", nullable: false),
                    Statistics_DescriptionTop = table.Column<string>(type: "text", nullable: true),
                    Statistics_DescriptionDown = table.Column<string>(type: "text", nullable: true),
                    Statistics_HasGameHistory = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionCurrentLots", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId });
                    table.CheckConstraint("CK_AuctionCurrentLots_Team", "(\"WinningTeamId\" IS NULL) = (\"WinningBasketId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_AuctionCurrentLots_AuctionSessions_GroupId_LeagueId_SeasonI~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionCurrentLots_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionCurrentLots_FantasyTeamSeasons_GroupId_WinningBasket~",
                        columns: x => new { x.GroupId, x.WinningBasketId, x.SeasonIndex, x.WinningTeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionCurrentLots_LeagueBasketSeasons_GroupId_LeagueId_Win~",
                        columns: x => new { x.GroupId, x.LeagueId, x.WinningBasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionCurrentLots_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionCurrentLots_PlayerSeasons_SubstitutedPlayerId_Season~",
                        columns: x => new { x.SubstitutedPlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionLastShown",
                columns: table => new
                {
                    Role = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionLastShown", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.Role });
                    table.ForeignKey(
                        name: "FK_AuctionLastShown_AuctionSessions_GroupId_LeagueId_SeasonInd~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionLastShown_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionLegacyParticipants",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OwnerEmail = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionLegacyParticipants", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_AuctionLegacyParticipants_AuctionSessions_GroupId_LeagueId_~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionPlayerEntries",
                columns: table => new
                {
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsShown = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionPlayerEntries", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.Role, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_AuctionPlayerEntries_AuctionSessions_GroupId_LeagueId_Seaso~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionPlayerEntries_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionPresence",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionPresence", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.ConnectionId });
                    table.ForeignKey(
                        name: "FK_AuctionPresence_AuctionSessions_GroupId_LeagueId_SeasonInde~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionPresence_GroupMemberships_GroupId_UserId",
                        columns: x => new { x.GroupId, x.UserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionWinnings",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerEmail = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionWinnings", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.Ordinal });
                    table.CheckConstraint("CK_AuctionWinnings_Team", "(\"TeamId\" IS NULL) = (\"BasketId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_AuctionWinnings_AuctionSessions_GroupId_LeagueId_SeasonInde~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionWinnings_FantasyTeamSeasons_GroupId_BasketId_SeasonI~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionWinnings_LeagueBasketSeasons_GroupId_LeagueId_Basket~",
                        columns: x => new { x.GroupId, x.LeagueId, x.BasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionWinnings_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionCurrentStatisticGames",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Vote = table.Column<float>(type: "real", nullable: true),
                    Positiveness = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionCurrentStatisticGames", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_AuctionCurrentStatisticGames_AuctionCurrentLots_GroupId_Lea~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId },
                        principalTable: "AuctionCurrentLots",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "AuctionId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionLegacyParticipantCoOwners",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionLegacyParticipantCoOwners", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.ParticipantOrdinal, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_AuctionLegacyParticipantCoOwners_AuctionLegacyParticipants_~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.ParticipantOrdinal },
                        principalTable: "AuctionLegacyParticipants",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "AuctionId", "Ordinal" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCurrentLots_ClubId",
                table: "AuctionCurrentLots",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCurrentLots_GroupId_LeagueId_WinningBasketId_SeasonI~",
                table: "AuctionCurrentLots",
                columns: new[] { "GroupId", "LeagueId", "WinningBasketId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCurrentLots_GroupId_WinningBasketId_SeasonIndex_Winn~",
                table: "AuctionCurrentLots",
                columns: new[] { "GroupId", "WinningBasketId", "SeasonIndex", "WinningTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCurrentLots_PlayerId_SeasonIndex",
                table: "AuctionCurrentLots",
                columns: new[] { "PlayerId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCurrentLots_SubstitutedPlayerId_SeasonIndex",
                table: "AuctionCurrentLots",
                columns: new[] { "SubstitutedPlayerId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionLastShown_PlayerId_SeasonIndex",
                table: "AuctionLastShown",
                columns: new[] { "PlayerId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionPlayerEntries_PlayerId_SeasonIndex",
                table: "AuctionPlayerEntries",
                columns: new[] { "PlayerId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionPresence_ConnectionId",
                table: "AuctionPresence",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionPresence_GroupId_UserId",
                table: "AuctionPresence",
                columns: new[] { "GroupId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSessions_GroupId_CreatorUserId",
                table: "AuctionSessions",
                columns: new[] { "GroupId", "CreatorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSessions_Id",
                table: "AuctionSessions",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionWinnings_GroupId_BasketId_SeasonIndex_TeamId",
                table: "AuctionWinnings",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionWinnings_GroupId_LeagueId_BasketId_SeasonIndex",
                table: "AuctionWinnings",
                columns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionWinnings_PlayerId_SeasonIndex",
                table: "AuctionWinnings",
                columns: new[] { "PlayerId", "SeasonIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionCurrentStatisticGames");

            migrationBuilder.DropTable(
                name: "AuctionLastShown");

            migrationBuilder.DropTable(
                name: "AuctionLegacyParticipantCoOwners");

            migrationBuilder.DropTable(
                name: "AuctionPlayerEntries");

            migrationBuilder.DropTable(
                name: "AuctionPresence");

            migrationBuilder.DropTable(
                name: "AuctionWinnings");

            migrationBuilder.DropTable(
                name: "AuctionCurrentLots");

            migrationBuilder.DropTable(
                name: "AuctionLegacyParticipants");

            migrationBuilder.DropTable(
                name: "AuctionSessions");
        }
    }
}
