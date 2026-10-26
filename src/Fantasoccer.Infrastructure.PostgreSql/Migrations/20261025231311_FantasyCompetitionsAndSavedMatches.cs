using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class FantasyCompetitionsAndSavedMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FantasyCalendars",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyCalendars", x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
                    table.ForeignKey(
                        name: "FK_FantasyCalendars_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FantasyStandingSnapshots",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    SnapshotDay = table.Column<int>(type: "integer", nullable: false),
                    SerieADay = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyStandingSnapshots", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SnapshotDay });
                    table.CheckConstraint("CK_FantasyStandingSnapshots_Day", "\"SnapshotDay\" BETWEEN 0 AND 38 AND \"SerieADay\" BETWEEN 0 AND 38");
                    table.ForeignKey(
                        name: "FK_FantasyStandingSnapshots_LeagueSeasons_GroupId_LeagueId_Sea~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FantasyStages",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyStages", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Stage });
                    table.ForeignKey(
                        name: "FK_FantasyStages_FantasyCalendars_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "FantasyCalendars",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FantasyStandingStages",
                columns: table => new
                {
                    Stage = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    SnapshotDay = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyStandingStages", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SnapshotDay, x.Stage });
                    table.ForeignKey(
                        name: "FK_FantasyStandingStages_FantasyStandingSnapshots_GroupId_Leag~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SnapshotDay },
                        principalTable: "FantasyStandingSnapshots",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "SnapshotDay" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FantasyMatchdays",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    FantasyTurn = table.Column<int>(type: "integer", nullable: false),
                    SerieADay = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyMatchdays", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Stage, x.Ordinal });
                    table.CheckConstraint("CK_FantasyMatchdays_Day", "\"SerieADay\" BETWEEN 1 AND 38");
                    table.ForeignKey(
                        name: "FK_FantasyMatchdays_FantasyStages_GroupId_LeagueId_SeasonIndex~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Stage },
                        principalTable: "FantasyStages",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Stage" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FantasyStandingRows",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    SnapshotDay = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<float>(type: "real", nullable: false),
                    Victories = table.Column<int>(type: "integer", nullable: false),
                    Draws = table.Column<int>(type: "integer", nullable: false),
                    Defeats = table.Column<int>(type: "integer", nullable: false),
                    GoalsFor = table.Column<int>(type: "integer", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "integer", nullable: false),
                    ValuePoints = table.Column<float>(type: "real", nullable: false),
                    SufferedValuePoints = table.Column<float>(type: "real", nullable: false),
                    PlusMoney = table.Column<int>(type: "integer", nullable: false),
                    Money = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyStandingRows", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SnapshotDay, x.Stage, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_FantasyStandingRows_FantasyStandingStages_GroupId_LeagueId_~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SnapshotDay, x.Stage },
                        principalTable: "FantasyStandingStages",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "SnapshotDay", "Stage" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FantasyStandingRows_FantasyTeamSeasons_GroupId_BasketId_Sea~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FantasyStandingRows_LeagueBasketSeasons_GroupId_LeagueId_Ba~",
                        columns: x => new { x.GroupId, x.LeagueId, x.BasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FantasyMatches",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    DayOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    HomeBasketId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    AwayBasketId = table.Column<Guid>(type: "uuid", nullable: true),
                    AwayTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeName = table.Column<string>(type: "text", nullable: false),
                    AwayName = table.Column<string>(type: "text", nullable: false),
                    HomeOwner = table.Column<string>(type: "text", nullable: true),
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
                    Result_Away_OwnGoal = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyMatches", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Id });
                    table.CheckConstraint("CK_FantasyMatches_Away", "(\"AwayBasketId\" IS NULL) = (\"AwayTeamId\" IS NULL)");
                    table.CheckConstraint("CK_FantasyMatches_Home", "(\"HomeBasketId\" IS NULL) = (\"HomeTeamId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_FantasyMatches_FantasyMatchdays_GroupId_LeagueId_SeasonInde~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Stage, x.DayOrdinal },
                        principalTable: "FantasyMatchdays",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Stage", "Ordinal" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FantasyMatches_FantasyTeamSeasons_GroupId_AwayBasketId_Seas~",
                        columns: x => new { x.GroupId, x.AwayBasketId, x.SeasonIndex, x.AwayTeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FantasyMatches_FantasyTeamSeasons_GroupId_HomeBasketId_Seas~",
                        columns: x => new { x.GroupId, x.HomeBasketId, x.SeasonIndex, x.HomeTeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FantasyMatches_LeagueBasketSeasons_GroupId_LeagueId_AwayBas~",
                        columns: x => new { x.GroupId, x.LeagueId, x.AwayBasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FantasyMatches_LeagueBasketSeasons_GroupId_LeagueId_HomeBas~",
                        columns: x => new { x.GroupId, x.LeagueId, x.HomeBasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayedMatchSnapshots",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    HomeName = table.Column<string>(type: "text", nullable: false),
                    AwayName = table.Column<string>(type: "text", nullable: false),
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
                    Result_Away_OwnGoal = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayedMatchSnapshots", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.MatchId, x.Day });
                    table.ForeignKey(
                        name: "FK_PlayedMatchSnapshots_FantasyMatches_GroupId_LeagueId_Season~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.MatchId },
                        principalTable: "FantasyMatches",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayedMatchPlayers",
                columns: table => new
                {
                    IsHome = table.Column<bool>(type: "boolean", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PlayedMatchPlayers", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.MatchId, x.Day, x.IsHome, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_PlayedMatchPlayers_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayedMatchPlayers_PlayedMatchSnapshots_GroupId_LeagueId_Se~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.MatchId, x.Day },
                        principalTable: "PlayedMatchSnapshots",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "MatchId", "Day" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayedMatchPlayers_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyMatches_GroupId_AwayBasketId_SeasonIndex_AwayTeamId",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "AwayBasketId", "SeasonIndex", "AwayTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyMatches_GroupId_HomeBasketId_SeasonIndex_HomeTeamId",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "HomeBasketId", "SeasonIndex", "HomeTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyMatches_GroupId_LeagueId_AwayBasketId_SeasonIndex",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "LeagueId", "AwayBasketId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyMatches_GroupId_LeagueId_HomeBasketId_SeasonIndex",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "LeagueId", "HomeBasketId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyMatches_GroupId_LeagueId_SeasonIndex_Stage_DayOrdinal",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "Stage", "DayOrdinal" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyStandingRows_GroupId_BasketId_SeasonIndex_TeamId",
                table: "FantasyStandingRows",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyStandingRows_GroupId_LeagueId_BasketId_SeasonIndex",
                table: "FantasyStandingRows",
                columns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayedMatchPlayers_ClubId",
                table: "PlayedMatchPlayers",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayedMatchPlayers_PlayerId_SeasonIndex",
                table: "PlayedMatchPlayers",
                columns: new[] { "PlayerId", "SeasonIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FantasyStandingRows");

            migrationBuilder.DropTable(
                name: "PlayedMatchPlayers");

            migrationBuilder.DropTable(
                name: "FantasyStandingStages");

            migrationBuilder.DropTable(
                name: "PlayedMatchSnapshots");

            migrationBuilder.DropTable(
                name: "FantasyStandingSnapshots");

            migrationBuilder.DropTable(
                name: "FantasyMatches");

            migrationBuilder.DropTable(
                name: "FantasyMatchdays");

            migrationBuilder.DropTable(
                name: "FantasyStages");

            migrationBuilder.DropTable(
                name: "FantasyCalendars");
        }
    }
}
