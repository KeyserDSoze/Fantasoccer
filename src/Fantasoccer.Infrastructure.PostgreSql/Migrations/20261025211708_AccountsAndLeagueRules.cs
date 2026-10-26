using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AccountsAndLeagueRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FantasyTeamSeasons_GroupId_BasketId_SeasonIndex_NormalizedN~",
                table: "FantasyTeamSeasons");

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CompetitionType",
                table: "LeagueSeasons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "LeagueSeasons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CompetitionType",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Leagues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Groups",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "FantasyTeamSeasons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "BasketSeasons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Baskets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiPrompt = table.Column<string>(type: "text", nullable: true),
                    AutoOptimalFormation = table.Column<bool>(type: "boolean", nullable: false),
                    LiveWidgetEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AccountPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountSelections",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    WorkingUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSelections", x => x.UserId);
                    table.CheckConstraint("CK_AccountSelections_LeagueGroup", "\"LeagueId\" IS NULL OR \"GroupId\" IS NOT NULL");
                    table.CheckConstraint("CK_AccountSelections_WorkingGroup", "\"WorkingUserId\" IS NULL OR \"GroupId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_AccountSelections_GroupMemberships_GroupId_UserId",
                        columns: x => new { x.GroupId, x.UserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountSelections_GroupMemberships_GroupId_WorkingUserId",
                        columns: x => new { x.GroupId, x.WorkingUserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountSelections_Leagues_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "Leagues",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountSelections_Seasons_Year",
                        column: x => x.Year,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountSelections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountSelections_Users_WorkingUserId",
                        column: x => x.WorkingUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueRules",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Formation = table.Column<int>(type: "integer", nullable: false),
                    StartingMoney = table.Column<int>(type: "integer", nullable: false),
                    DelayedDay = table.Column<int>(type: "integer", nullable: false),
                    CancelledDay = table.Column<int>(type: "integer", nullable: false),
                    PointForFirstGoal = table.Column<int>(type: "integer", nullable: false),
                    PointForNextGoal = table.Column<int>(type: "integer", nullable: false),
                    PointForOwnGoal = table.Column<int>(type: "integer", nullable: false),
                    DifferencePointForOwnGoal = table.Column<int>(type: "integer", nullable: false),
                    PointInHome = table.Column<int>(type: "integer", nullable: false),
                    PointForVictory = table.Column<int>(type: "integer", nullable: false),
                    PointForDefeat = table.Column<int>(type: "integer", nullable: false),
                    PointForDraw = table.Column<int>(type: "integer", nullable: false),
                    PointForStrongDefense = table.Column<int>(type: "integer", nullable: false),
                    PointForStrongDefense4 = table.Column<int>(type: "integer", nullable: false),
                    PointForStrongDefense5 = table.Column<int>(type: "integer", nullable: false),
                    PointForGoodPeople = table.Column<int>(type: "integer", nullable: false),
                    PointForCleanSheet = table.Column<int>(type: "integer", nullable: false),
                    MoneyForGoal = table.Column<int>(type: "integer", nullable: false),
                    MoneyForSufferedGoal = table.Column<int>(type: "integer", nullable: false),
                    RandomAuction = table.Column<bool>(type: "boolean", nullable: false),
                    RankWithValuePoints = table.Column<bool>(type: "boolean", nullable: false),
                    Market = table.Column<int>(type: "integer", nullable: false),
                    LiveFormationChanges = table.Column<int>(type: "integer", nullable: false),
                    AllowLiveModuleChange = table.Column<bool>(type: "boolean", nullable: false),
                    OpeningCompetitionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OpeningCompetitionSerieADays = table.Column<int>(type: "integer", nullable: false),
                    HasTypeSettings = table.Column<bool>(type: "boolean", nullable: false),
                    CalendarType = table.Column<int>(type: "integer", nullable: false),
                    MaxPlayersInTeam = table.Column<int>(type: "integer", nullable: false),
                    MaxGoalKeepersInTeam = table.Column<int>(type: "integer", nullable: false),
                    MaxDefendersInTeam = table.Column<int>(type: "integer", nullable: false),
                    MaxMidfieldersInTeam = table.Column<int>(type: "integer", nullable: false),
                    MaxForwardsInTeam = table.Column<int>(type: "integer", nullable: false),
                    MaxGoalKeepersInBench = table.Column<int>(type: "integer", nullable: false),
                    MaxDefendersInBench = table.Column<int>(type: "integer", nullable: false),
                    MaxMidfieldersInBench = table.Column<int>(type: "integer", nullable: false),
                    MaxForwardsInBench = table.Column<int>(type: "integer", nullable: false),
                    HasPreviousYearSettings = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousYearMaxTeamsPerLeague = table.Column<int>(type: "integer", nullable: false),
                    PreviousYearRoundType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueRules", x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
                    table.ForeignKey(
                        name: "FK_LeagueRules_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OAuthRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    DeleteAfterUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamOwnershipChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Added = table.Column<bool>(type: "boolean", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamOwnershipChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamOwnershipChanges_FantasyTeamSeasons_GroupId_BasketId_Se~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamOwnershipChanges_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamOwnershipChanges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnresolvedAccountSelections",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacyGroupId = table.Column<string>(type: "text", nullable: true),
                    LegacyLeagueId = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    WorkingEmail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnresolvedAccountSelections", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UnresolvedAccountSelections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueCardLimits",
                columns: table => new
                {
                    CardType = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Maximum = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueCardLimits", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.CardType });
                    table.ForeignKey(
                        name: "FK_LeagueCardLimits_LeagueRules_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueRules",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueOpeningPrizes",
                columns: table => new
                {
                    Position = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Credits = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueOpeningPrizes", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Position });
                    table.ForeignKey(
                        name: "FK_LeagueOpeningPrizes_LeagueRules_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueRules",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueStageRules",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    FromStart = table.Column<bool>(type: "boolean", nullable: false),
                    FromRankingStartTeam = table.Column<int>(type: "integer", nullable: true),
                    FromRankingEndTeam = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueStageRules", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_LeagueStageRules_LeagueRules_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueRules",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueVoteWeights",
                columns: table => new
                {
                    PlayerRole = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Goal = table.Column<float>(type: "real", nullable: false),
                    Penalty = table.Column<float>(type: "real", nullable: false),
                    SufferedGoal = table.Column<float>(type: "real", nullable: false),
                    StoppedPenalty = table.Column<float>(type: "real", nullable: false),
                    WrongedPenalty = table.Column<float>(type: "real", nullable: false),
                    OwnGoal = table.Column<float>(type: "real", nullable: false),
                    Assist = table.Column<float>(type: "real", nullable: false),
                    YellowCard = table.Column<float>(type: "real", nullable: false),
                    RedCard = table.Column<float>(type: "real", nullable: false),
                    Injury = table.Column<int>(type: "integer", nullable: false),
                    ManOfTheMatch = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueVoteWeights", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.PlayerRole });
                    table.ForeignKey(
                        name: "FK_LeagueVoteWeights_LeagueRules_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueRules",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreviousLeagueQualifications",
                columns: table => new
                {
                    SourceLeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreviousLeagueQualifications", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SourceLeagueId });
                    table.ForeignKey(
                        name: "FK_PreviousLeagueQualifications_LeagueRules_GroupId_LeagueId_S~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueRules",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreviousLeagueQualifications_Leagues_GroupId_SourceLeagueId",
                        columns: x => new { x.GroupId, x.SourceLeagueId },
                        principalTable: "Leagues",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyTeamSeasons_GroupId_BasketId_SeasonIndex_NormalizedN~",
                table: "FantasyTeamSeasons",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "NormalizedName" },
                unique: true,
                filter: "NOT \"IsArchived\"");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSelections_GroupId_LeagueId",
                table: "AccountSelections",
                columns: new[] { "GroupId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSelections_GroupId_UserId",
                table: "AccountSelections",
                columns: new[] { "GroupId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSelections_GroupId_WorkingUserId",
                table: "AccountSelections",
                columns: new[] { "GroupId", "WorkingUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSelections_WorkingUserId",
                table: "AccountSelections",
                column: "WorkingUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSelections_Year",
                table: "AccountSelections",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthRecords_DeleteAfterUtc",
                table: "OAuthRecords",
                column: "DeleteAfterUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthRecords_Kind",
                table: "OAuthRecords",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_PreviousLeagueQualifications_GroupId_SourceLeagueId",
                table: "PreviousLeagueQualifications",
                columns: new[] { "GroupId", "SourceLeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamOwnershipChanges_ActorUserId",
                table: "TeamOwnershipChanges",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamOwnershipChanges_GroupId_BasketId_SeasonIndex_TeamId",
                table: "TeamOwnershipChanges",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamOwnershipChanges_GroupId_TeamId_ChangedAtUtc",
                table: "TeamOwnershipChanges",
                columns: new[] { "GroupId", "TeamId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamOwnershipChanges_UserId",
                table: "TeamOwnershipChanges",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPreferences");

            migrationBuilder.DropTable(
                name: "AccountSelections");

            migrationBuilder.DropTable(
                name: "LeagueCardLimits");

            migrationBuilder.DropTable(
                name: "LeagueOpeningPrizes");

            migrationBuilder.DropTable(
                name: "LeagueStageRules");

            migrationBuilder.DropTable(
                name: "LeagueVoteWeights");

            migrationBuilder.DropTable(
                name: "OAuthRecords");

            migrationBuilder.DropTable(
                name: "PreviousLeagueQualifications");

            migrationBuilder.DropTable(
                name: "TeamOwnershipChanges");

            migrationBuilder.DropTable(
                name: "UnresolvedAccountSelections");

            migrationBuilder.DropTable(
                name: "LeagueRules");

            migrationBuilder.DropIndex(
                name: "IX_FantasyTeamSeasons_GroupId_BasketId_SeasonIndex_NormalizedN~",
                table: "FantasyTeamSeasons");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompetitionType",
                table: "LeagueSeasons");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "LeagueSeasons");

            migrationBuilder.DropColumn(
                name: "CompetitionType",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "FantasyTeamSeasons");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "BasketSeasons");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Baskets");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyTeamSeasons_GroupId_BasketId_SeasonIndex_NormalizedN~",
                table: "FantasyTeamSeasons",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "NormalizedName" },
                unique: true);
        }
    }
}
