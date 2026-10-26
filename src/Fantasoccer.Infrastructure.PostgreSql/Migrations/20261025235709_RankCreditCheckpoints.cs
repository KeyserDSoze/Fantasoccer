using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RankCreditCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RankCreditGranted",
                table: "RosterSnapshots",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RankCreditConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousGranted = table.Column<int>(type: "integer", nullable: true),
                    Granted = table.Column<int>(type: "integer", nullable: false),
                    BalanceAtConfirmation = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankCreditConfirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RankCreditConfirmations_FantasyTeamSeasons_GroupId_BasketId~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RankCreditConfirmations_GroupMemberships_GroupId_ActorUserId",
                        columns: x => new { x.GroupId, x.ActorUserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RankCreditConfirmations_GroupId_ActorUserId",
                table: "RankCreditConfirmations",
                columns: new[] { "GroupId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RankCreditConfirmations_GroupId_BasketId_SeasonIndex_TeamId",
                table: "RankCreditConfirmations",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankCreditConfirmations");

            migrationBuilder.DropColumn(
                name: "RankCreditGranted",
                table: "RosterSnapshots");
        }
    }
}
