using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AuctionRosterAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuctionRosterChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RosterOrdinal = table.Column<int>(type: "integer", nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Revenue = table.Column<int>(type: "integer", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionRosterChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionRosterChanges_AuctionSessions_GroupId_LeagueId_Seaso~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId },
                        principalTable: "AuctionSessions",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionRosterChanges_FantasyTeamSeasons_GroupId_BasketId_Se~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionRosterChanges_GroupMemberships_GroupId_ActorUserId",
                        columns: x => new { x.GroupId, x.ActorUserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionRosterChanges_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRosterChanges_GroupId_ActorUserId",
                table: "AuctionRosterChanges",
                columns: new[] { "GroupId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRosterChanges_GroupId_BasketId_SeasonIndex_TeamId",
                table: "AuctionRosterChanges",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRosterChanges_GroupId_LeagueId_SeasonIndex_AuctionId",
                table: "AuctionRosterChanges",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "AuctionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRosterChanges_PlayerId_SeasonIndex",
                table: "AuctionRosterChanges",
                columns: new[] { "PlayerId", "SeasonIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionRosterChanges");
        }
    }
}
