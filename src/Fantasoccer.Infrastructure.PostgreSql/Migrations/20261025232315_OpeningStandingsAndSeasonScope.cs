using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OpeningStandingsAndSeasonScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RosterSnapshots_GroupId_LeagueId",
                table: "RosterSnapshots");

            migrationBuilder.CreateTable(
                name: "OpeningStandingSnapshots",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    SerieADays = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningStandingSnapshots", x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
                    table.ForeignKey(
                        name: "FK_OpeningStandingSnapshots_LeagueSeasons_GroupId_LeagueId_Sea~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningStandingRows",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamOriginYear = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Score = table.Column<float>(type: "real", nullable: false),
                    Prize = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningStandingRows", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId });
                    table.CheckConstraint("CK_OpeningStandingRows_Origin", "\"TeamOriginYear\" = \"SeasonIndex\" - 1");
                    table.ForeignKey(
                        name: "FK_OpeningStandingRows_FantasyTeamSeasons_GroupId_BasketId_Tea~",
                        columns: x => new { x.GroupId, x.BasketId, x.TeamOriginYear, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningStandingRows_GroupLeagueBaskets_GroupId_LeagueId_Bas~",
                        columns: x => new { x.GroupId, x.LeagueId, x.BasketId },
                        principalTable: "GroupLeagueBaskets",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningStandingRows_OpeningStandingSnapshots_GroupId_League~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "OpeningStandingSnapshots",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RosterSnapshots_GroupId_LeagueId_SeasonIndex",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningStandingRows_GroupId_BasketId_TeamOriginYear_TeamId",
                table: "OpeningStandingRows",
                columns: new[] { "GroupId", "BasketId", "TeamOriginYear", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningStandingRows_GroupId_LeagueId_BasketId",
                table: "OpeningStandingRows",
                columns: new[] { "GroupId", "LeagueId", "BasketId" });

            migrationBuilder.AddForeignKey(
                name: "FK_RosterSnapshots_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                principalTable: "LeagueSeasons",
                principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RosterSnapshots_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                table: "RosterSnapshots");

            migrationBuilder.DropTable(
                name: "OpeningStandingRows");

            migrationBuilder.DropTable(
                name: "OpeningStandingSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_RosterSnapshots_GroupId_LeagueId_SeasonIndex",
                table: "RosterSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_RosterSnapshots_GroupId_LeagueId",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "LeagueId" });
        }
    }
}
