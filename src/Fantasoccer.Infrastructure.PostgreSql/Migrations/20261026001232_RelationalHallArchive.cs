using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RelationalHallArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HallArchives",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallArchives", x => new { x.GroupId, x.LeagueId });
                    table.ForeignKey(
                        name: "FK_HallArchives_Leagues_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "Leagues",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallCareerRanks",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerEmail = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<float>(type: "real", nullable: false),
                    Victories = table.Column<int>(type: "integer", nullable: false),
                    Draws = table.Column<int>(type: "integer", nullable: false),
                    Defeats = table.Column<int>(type: "integer", nullable: false),
                    Goals = table.Column<int>(type: "integer", nullable: false),
                    SufferedGoals = table.Column<int>(type: "integer", nullable: false),
                    ValuePoints = table.Column<float>(type: "real", nullable: false),
                    SufferedValuePoints = table.Column<float>(type: "real", nullable: false),
                    PlusMoney = table.Column<int>(type: "integer", nullable: false),
                    Money = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallCareerRanks", x => new { x.GroupId, x.LeagueId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_HallCareerRanks_HallArchives_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "HallArchives",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallCareerRanks_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallPlayerRecords",
                columns: table => new
                {
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Points = table.Column<float>(type: "real", nullable: false),
                    Player_PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Player_ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    Player_Name = table.Column<string>(type: "text", nullable: false),
                    Player_PlayerRole = table.Column<int>(type: "integer", nullable: false),
                    Player_IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Player_IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Player_Price = table.Column<int>(type: "integer", nullable: false),
                    Player_Revenue = table.Column<int>(type: "integer", nullable: false),
                    Player_Status = table.Column<int>(type: "integer", nullable: false),
                    Player_Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallPlayerRecords", x => new { x.GroupId, x.LeagueId, x.Kind });
                    table.ForeignKey(
                        name: "FK_HallPlayerRecords_Clubs_Player_ClubId",
                        column: x => x.Player_ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallPlayerRecords_HallArchives_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "HallArchives",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallPlayerRecords_Players_Player_PlayerId",
                        column: x => x.Player_PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallPlayerRecords_Seasons_Year",
                        column: x => x.Year,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallRecordGames",
                columns: table => new
                {
                    HallArchiveGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    HallArchiveLeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    LegacyGameId = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Home = table.Column<string>(type: "text", nullable: false),
                    HomeOwner = table.Column<string>(type: "text", nullable: true),
                    Away = table.Column<string>(type: "text", nullable: false),
                    AwayOwner = table.Column<string>(type: "text", nullable: true),
                    Result_IsCancelled = table.Column<bool>(type: "boolean", nullable: true),
                    Result_HomeGoals = table.Column<int>(type: "integer", nullable: true),
                    Result_AwayGoals = table.Column<int>(type: "integer", nullable: true),
                    Result_Home_Value = table.Column<float>(type: "real", nullable: true),
                    Result_Home_DefensiveBonus = table.Column<bool>(type: "boolean", nullable: true),
                    Result_Home_GoodPeople = table.Column<bool>(type: "boolean", nullable: true),
                    Result_Home_OwnGoal = table.Column<bool>(type: "boolean", nullable: true),
                    Result_Away_Value = table.Column<float>(type: "real", nullable: true),
                    Result_Away_DefensiveBonus = table.Column<bool>(type: "boolean", nullable: true),
                    Result_Away_GoodPeople = table.Column<bool>(type: "boolean", nullable: true),
                    Result_Away_OwnGoal = table.Column<bool>(type: "boolean", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallRecordGames", x => new { x.HallArchiveGroupId, x.HallArchiveLeagueId });
                    table.ForeignKey(
                        name: "FK_HallRecordGames_FantasyMatches_GroupId_LeagueId_Year_Linked~",
                        columns: x => new { x.GroupId, x.LeagueId, x.Year, x.LinkedMatchId },
                        principalTable: "FantasyMatches",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallRecordGames_HallArchives_HallArchiveGroupId_HallArchive~",
                        columns: x => new { x.HallArchiveGroupId, x.HallArchiveLeagueId },
                        principalTable: "HallArchives",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallRecordGames_Seasons_Year",
                        column: x => x.Year,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallWinningOwners",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerEmail = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallWinningOwners", x => new { x.GroupId, x.LeagueId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_HallWinningOwners_HallArchives_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "HallArchives",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallWinningOwners_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallWinningPlayers",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Player_PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Player_ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    Player_Name = table.Column<string>(type: "text", nullable: false),
                    Player_PlayerRole = table.Column<int>(type: "integer", nullable: false),
                    Player_IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Player_IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Player_Price = table.Column<int>(type: "integer", nullable: false),
                    Player_Revenue = table.Column<int>(type: "integer", nullable: false),
                    Player_Status = table.Column<int>(type: "integer", nullable: false),
                    Player_Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallWinningPlayers", x => new { x.GroupId, x.LeagueId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_HallWinningPlayers_Clubs_Player_ClubId",
                        column: x => x.Player_ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallWinningPlayers_HallArchives_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "HallArchives",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallWinningPlayers_Players_Player_PlayerId",
                        column: x => x.Player_PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallOwnerWinStages",
                columns: table => new
                {
                    Stage = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinnerOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallOwnerWinStages", x => new { x.GroupId, x.LeagueId, x.WinnerOrdinal, x.Stage });
                    table.ForeignKey(
                        name: "FK_HallOwnerWinStages_HallWinningOwners_GroupId_LeagueId_Winne~",
                        columns: x => new { x.GroupId, x.LeagueId, x.WinnerOrdinal },
                        principalTable: "HallWinningOwners",
                        principalColumns: new[] { "GroupId", "LeagueId", "Ordinal" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HallPlayerWinStages",
                columns: table => new
                {
                    Stage = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinnerOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallPlayerWinStages", x => new { x.GroupId, x.LeagueId, x.WinnerOrdinal, x.Stage });
                    table.ForeignKey(
                        name: "FK_HallPlayerWinStages_HallWinningPlayers_GroupId_LeagueId_Win~",
                        columns: x => new { x.GroupId, x.LeagueId, x.WinnerOrdinal },
                        principalTable: "HallWinningPlayers",
                        principalColumns: new[] { "GroupId", "LeagueId", "Ordinal" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HallOwnerWinYears",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinnerOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallOwnerWinYears", x => new { x.GroupId, x.LeagueId, x.WinnerOrdinal, x.Stage, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_HallOwnerWinYears_HallOwnerWinStages_GroupId_LeagueId_Winne~",
                        columns: x => new { x.GroupId, x.LeagueId, x.WinnerOrdinal, x.Stage },
                        principalTable: "HallOwnerWinStages",
                        principalColumns: new[] { "GroupId", "LeagueId", "WinnerOrdinal", "Stage" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallOwnerWinYears_Seasons_Year",
                        column: x => x.Year,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallPlayerWinYears",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinnerOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallPlayerWinYears", x => new { x.GroupId, x.LeagueId, x.WinnerOrdinal, x.Stage, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_HallPlayerWinYears_HallPlayerWinStages_GroupId_LeagueId_Win~",
                        columns: x => new { x.GroupId, x.LeagueId, x.WinnerOrdinal, x.Stage },
                        principalTable: "HallPlayerWinStages",
                        principalColumns: new[] { "GroupId", "LeagueId", "WinnerOrdinal", "Stage" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallPlayerWinYears_Seasons_Year",
                        column: x => x.Year,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HallCareerRanks_OwnerUserId",
                table: "HallCareerRanks",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HallOwnerWinYears_Year",
                table: "HallOwnerWinYears",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_HallPlayerRecords_Player_ClubId",
                table: "HallPlayerRecords",
                column: "Player_ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_HallPlayerRecords_Player_PlayerId",
                table: "HallPlayerRecords",
                column: "Player_PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_HallPlayerRecords_Year",
                table: "HallPlayerRecords",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_HallPlayerWinYears_Year",
                table: "HallPlayerWinYears",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_HallRecordGames_GroupId_LeagueId_Year_LinkedMatchId",
                table: "HallRecordGames",
                columns: new[] { "GroupId", "LeagueId", "Year", "LinkedMatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_HallRecordGames_Year",
                table: "HallRecordGames",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_HallWinningOwners_OwnerUserId",
                table: "HallWinningOwners",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HallWinningPlayers_Player_ClubId",
                table: "HallWinningPlayers",
                column: "Player_ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_HallWinningPlayers_Player_PlayerId",
                table: "HallWinningPlayers",
                column: "Player_PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HallCareerRanks");

            migrationBuilder.DropTable(
                name: "HallOwnerWinYears");

            migrationBuilder.DropTable(
                name: "HallPlayerRecords");

            migrationBuilder.DropTable(
                name: "HallPlayerWinYears");

            migrationBuilder.DropTable(
                name: "HallRecordGames");

            migrationBuilder.DropTable(
                name: "HallOwnerWinStages");

            migrationBuilder.DropTable(
                name: "HallPlayerWinStages");

            migrationBuilder.DropTable(
                name: "HallWinningOwners");

            migrationBuilder.DropTable(
                name: "HallWinningPlayers");

            migrationBuilder.DropTable(
                name: "HallArchives");
        }
    }
}
