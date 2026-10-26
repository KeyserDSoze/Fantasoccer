using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class SharedFootballSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FootballChances",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerRole = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Fantagazzetta = table.Column<bool>(type: "boolean", nullable: false),
                    Gazzetta = table.Column<bool>(type: "boolean", nullable: false),
                    Mediaset = table.Column<bool>(type: "boolean", nullable: false),
                    Sky = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LegacyLastGameDay = table.Column<int>(type: "integer", nullable: true),
                    LegacyLastGameVote = table.Column<float>(type: "real", nullable: true),
                    LegacyLastGamePositiveness = table.Column<int>(type: "integer", nullable: true),
                    LegacyTrend = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballChances", x => new { x.PlayerId, x.SeasonIndex, x.Day });
                    table.CheckConstraint("CK_FootballChances_Day", "\"Day\" BETWEEN 1 AND 38");
                    table.ForeignKey(
                        name: "FK_FootballChances_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballChances_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballChances_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FootballClubRanks",
                columns: table => new
                {
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<float>(type: "real", nullable: false),
                    Victories = table.Column<int>(type: "integer", nullable: false),
                    Draws = table.Column<int>(type: "integer", nullable: false),
                    Defeats = table.Column<int>(type: "integer", nullable: false),
                    GoalsFor = table.Column<int>(type: "integer", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "integer", nullable: false),
                    Played = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballClubRanks", x => new { x.SeasonIndex, x.ClubId });
                    table.ForeignKey(
                        name: "FK_FootballClubRanks_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballClubRanks_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FootballDatasets",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "text", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballDatasets", x => new { x.Kind, x.SeasonIndex, x.Day });
                    table.CheckConstraint("CK_FootballDatasets_Day", "\"Day\" BETWEEN 0 AND 38");
                    table.ForeignKey(
                        name: "FK_FootballDatasets_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FootballPlayerStatistics",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerRole = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Summatory = table.Column<float>(type: "real", nullable: false),
                    FantaSummatory = table.Column<float>(type: "real", nullable: false),
                    WithVote = table.Column<int>(type: "integer", nullable: false),
                    WithoutVote = table.Column<int>(type: "integer", nullable: false),
                    NoPlayed = table.Column<int>(type: "integer", nullable: false),
                    WithSpecial = table.Column<int>(type: "integer", nullable: false),
                    Goals = table.Column<int>(type: "integer", nullable: false),
                    Penalties = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    StoppedPenalties = table.Column<int>(type: "integer", nullable: false),
                    SufferedGoals = table.Column<int>(type: "integer", nullable: false),
                    WrongedPenalties = table.Column<int>(type: "integer", nullable: false),
                    OwnGoals = table.Column<int>(type: "integer", nullable: false),
                    YellowCards = table.Column<int>(type: "integer", nullable: false),
                    RedCards = table.Column<int>(type: "integer", nullable: false),
                    EnoughVotes = table.Column<int>(type: "integer", nullable: false),
                    ManOfTheMatch = table.Column<int>(type: "integer", nullable: false),
                    Injured = table.Column<int>(type: "integer", nullable: false),
                    DescriptionTop = table.Column<string>(type: "text", nullable: true),
                    DescriptionDown = table.Column<string>(type: "text", nullable: true),
                    HasDescription = table.Column<bool>(type: "boolean", nullable: false),
                    HasGameHistory = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballPlayerStatistics", x => new { x.PlayerId, x.SeasonIndex });
                    table.ForeignKey(
                        name: "FK_FootballPlayerStatistics_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballPlayerStatistics_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballPlayerStatistics_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FootballVotes",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerRole = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    VoteRole = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<float>(type: "real", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false),
                    Goals = table.Column<int>(type: "integer", nullable: false),
                    Penalties = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    StoppedPenalties = table.Column<int>(type: "integer", nullable: false),
                    SufferedGoals = table.Column<int>(type: "integer", nullable: false),
                    WrongedPenalties = table.Column<int>(type: "integer", nullable: false),
                    OwnGoals = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ManOfTheMatch = table.Column<bool>(type: "boolean", nullable: false),
                    HasVote = table.Column<bool>(type: "boolean", nullable: false),
                    IsOut = table.Column<bool>(type: "boolean", nullable: false),
                    IsIn = table.Column<bool>(type: "boolean", nullable: false),
                    Injured = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballVotes", x => new { x.PlayerId, x.SeasonIndex, x.Day, x.Source });
                    table.CheckConstraint("CK_FootballVotes_Day", "\"Day\" BETWEEN 1 AND 38");
                    table.CheckConstraint("CK_FootballVotes_Source", "\"Source\" BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_FootballVotes_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballVotes_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballVotes_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FootballStatisticGames",
                columns: table => new
                {
                    Day = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Vote = table.Column<float>(type: "real", nullable: true),
                    Positiveness = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballStatisticGames", x => new { x.PlayerId, x.SeasonIndex, x.Day });
                    table.ForeignKey(
                        name: "FK_FootballStatisticGames_FootballPlayerStatistics_PlayerId_Se~",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "FootballPlayerStatistics",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FootballChances_ClubId",
                table: "FootballChances",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_FootballChances_SeasonIndex_Day",
                table: "FootballChances",
                columns: new[] { "SeasonIndex", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_FootballClubRanks_ClubId",
                table: "FootballClubRanks",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_FootballDatasets_SeasonIndex",
                table: "FootballDatasets",
                column: "SeasonIndex");

            migrationBuilder.CreateIndex(
                name: "IX_FootballPlayerStatistics_ClubId",
                table: "FootballPlayerStatistics",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_FootballVotes_ClubId",
                table: "FootballVotes",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_FootballVotes_SeasonIndex_Day_Source",
                table: "FootballVotes",
                columns: new[] { "SeasonIndex", "Day", "Source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FootballChances");

            migrationBuilder.DropTable(
                name: "FootballClubRanks");

            migrationBuilder.DropTable(
                name: "FootballDatasets");

            migrationBuilder.DropTable(
                name: "FootballStatisticGames");

            migrationBuilder.DropTable(
                name: "FootballVotes");

            migrationBuilder.DropTable(
                name: "FootballPlayerStatistics");
        }
    }
}
