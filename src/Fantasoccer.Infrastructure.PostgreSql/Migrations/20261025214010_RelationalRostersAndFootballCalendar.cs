using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RelationalRostersAndFootballCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Abbreviation",
                table: "Clubs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "FootballMatchdays",
                columns: table => new
                {
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    SerieADay = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballMatchdays", x => new { x.SeasonIndex, x.SerieADay });
                    table.CheckConstraint("CK_FootballMatchdays_Day", "\"SerieADay\" BETWEEN 1 AND 38");
                    table.ForeignKey(
                        name: "FK_FootballMatchdays_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterSnapshots",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MoneyFromRank = table.Column<int>(type: "integer", nullable: false),
                    OpeningCompetitionPrize = table.Column<int>(type: "integer", nullable: false),
                    FormationChanges = table.Column<int>(type: "integer", nullable: false),
                    LegacyLastUpdateLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastUpdateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterSnapshots", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId });
                    table.CheckConstraint("CK_RosterSnapshots_Competition", "(\"Kind\" IN (0,1) AND \"LeagueId\" IS NULL AND \"CompetitionId\" = '00000000-0000-0000-0000-000000000000') OR (\"Kind\" IN (2,3) AND \"LeagueId\" IS NOT NULL AND \"LeagueId\" = \"CompetitionId\")");
                    table.CheckConstraint("CK_RosterSnapshots_KindDay", "(\"Kind\" IN (0,2) AND \"Day\" = 0) OR (\"Kind\" IN (1,3) AND \"Day\" BETWEEN 1 AND 38)");
                    table.ForeignKey(
                        name: "FK_RosterSnapshots_FantasyTeamSeasons_GroupId_BasketId_SeasonI~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterSnapshots_GroupMemberships_GroupId_OwnerUserId",
                        columns: x => new { x.GroupId, x.OwnerUserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterSnapshots_Leagues_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "Leagues",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FootballMatches",
                columns: table => new
                {
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    SerieADay = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    HomeClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    KickoffUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LegacyKickoffLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HomeGoals = table.Column<int>(type: "integer", nullable: true),
                    AwayGoals = table.Column<int>(type: "integer", nullable: true),
                    Delayed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballMatches", x => new { x.SeasonIndex, x.SerieADay, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_FootballMatches_Clubs_AwayClubId",
                        column: x => x.AwayClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballMatches_Clubs_HomeClubId",
                        column: x => x.HomeClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballMatches_FootballMatchdays_SeasonIndex_SerieADay",
                        columns: x => new { x.SeasonIndex, x.SerieADay },
                        principalTable: "FootballMatchdays",
                        principalColumns: new[] { "SeasonIndex", "SerieADay" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterFormationChanges",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacyDateLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Author = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterFormationChanges", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_RosterFormationChanges_RosterSnapshots_GroupId_BasketId_Sea~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId },
                        principalTable: "RosterSnapshots",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Kind", "Day", "CompetitionId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RosterOwners",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterOwners", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_RosterOwners_GroupMemberships_GroupId_UserId",
                        columns: x => new { x.GroupId, x.UserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterOwners_RosterSnapshots_GroupId_BasketId_SeasonIndex_T~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId },
                        principalTable: "RosterSnapshots",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Kind", "Day", "CompetitionId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RosterOwners_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterPlayers",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_RosterPlayers", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_RosterPlayers_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterPlayers_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterPlayers_RosterSnapshots_GroupId_BasketId_SeasonIndex_~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId },
                        principalTable: "RosterSnapshots",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Kind", "Day", "CompetitionId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RosterFormationPlayerChanges",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeOrdinal = table.Column<int>(type: "integer", nullable: false),
                    PlayerName = table.Column<string>(type: "text", nullable: false),
                    FromPosition = table.Column<int>(type: "integer", nullable: false),
                    ToPosition = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterFormationPlayerChanges", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId, x.ChangeOrdinal, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_RosterFormationPlayerChanges_RosterFormationChanges_GroupId~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Kind, x.Day, x.CompetitionId, x.ChangeOrdinal },
                        principalTable: "RosterFormationChanges",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Kind", "Day", "CompetitionId", "Ordinal" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FootballMatches_AwayClubId",
                table: "FootballMatches",
                column: "AwayClubId");

            migrationBuilder.CreateIndex(
                name: "IX_FootballMatches_HomeClubId",
                table: "FootballMatches",
                column: "HomeClubId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterOwners_GroupId_UserId",
                table: "RosterOwners",
                columns: new[] { "GroupId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterOwners_UserId",
                table: "RosterOwners",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterPlayers_ClubId",
                table: "RosterPlayers",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterPlayers_GroupId_BasketId_SeasonIndex_TeamId_Kind_Day_~",
                table: "RosterPlayers",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Kind", "Day", "CompetitionId", "PlayerId" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RosterPlayers_PlayerId_SeasonIndex",
                table: "RosterPlayers",
                columns: new[] { "PlayerId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterSnapshots_GroupId_BasketId_SeasonIndex_Kind_Day_Compe~",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "Kind", "Day", "CompetitionId", "OwnerUserId" },
                unique: true,
                filter: "\"Kind\" IN (1,3)");

            migrationBuilder.CreateIndex(
                name: "IX_RosterSnapshots_GroupId_LeagueId",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterSnapshots_GroupId_OwnerUserId",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "OwnerUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FootballMatches");

            migrationBuilder.DropTable(
                name: "RosterFormationPlayerChanges");

            migrationBuilder.DropTable(
                name: "RosterOwners");

            migrationBuilder.DropTable(
                name: "RosterPlayers");

            migrationBuilder.DropTable(
                name: "FootballMatchdays");

            migrationBuilder.DropTable(
                name: "RosterFormationChanges");

            migrationBuilder.DropTable(
                name: "RosterSnapshots");

            migrationBuilder.DropColumn(
                name: "Abbreviation",
                table: "Clubs");
        }
    }
}
