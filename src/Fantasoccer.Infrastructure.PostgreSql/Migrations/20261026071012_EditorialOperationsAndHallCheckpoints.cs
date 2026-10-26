using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class EditorialOperationsAndHallCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "MarketTradeEvents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "ContributionsInitialized",
                table: "HallArchives",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ArchivedOperationalLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionKey = table.Column<string>(type: "text", nullable: false),
                    RowKey = table.Column<string>(type: "text", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ETag = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedOperationalLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HallBaselineConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchiveVersion = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallBaselineConfirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallBaselineConfirmations_GroupMemberships_GroupId_ActorUse~",
                        columns: x => new { x.GroupId, x.ActorUserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallBaselineConfirmations_HallArchives_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "HallArchives",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallRankContributions",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_HallRankContributions", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.OwnerUserId });
                    table.ForeignKey(
                        name: "FK_HallRankContributions_HallArchives_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "HallArchives",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallRankContributions_LeagueSeasons_GroupId_LeagueId_Season~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallRankContributions_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NewsEditions",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsEditions", x => new { x.GroupId, x.SeasonIndex, x.Day });
                    table.CheckConstraint("CK_NewsEditions_Day", "\"Day\" BETWEEN 1 AND 38");
                    table.ForeignKey(
                        name: "FK_NewsEditions_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsEditions_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SqlJobStates",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    NextRunAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SqlJobStates", x => new { x.Name, x.SeasonIndex });
                    table.ForeignKey(
                        name: "FK_SqlJobStates_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamAdvice",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    IsGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRosterVersion = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamAdvice", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Day });
                    table.CheckConstraint("CK_TeamAdvice_Day", "\"Day\" BETWEEN 1 AND 38");
                    table.ForeignKey(
                        name: "FK_TeamAdvice_FantasyTeamSeasons_GroupId_BasketId_SeasonIndex_~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamAdvice_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedOperationalLogProperties",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    LogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    EncodedValue = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedOperationalLogProperties", x => new { x.LogId, x.Name });
                    table.ForeignKey(
                        name: "FK_ArchivedOperationalLogProperties_ArchivedOperationalLogs_Lo~",
                        column: x => x.LogId,
                        principalTable: "ArchivedOperationalLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsArticles",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeagueName = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Image = table.Column<string>(type: "text", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LegacyDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceVersion = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsArticles", x => new { x.GroupId, x.SeasonIndex, x.Day, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_NewsArticles_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsArticles_NewsEditions_GroupId_SeasonIndex_Day",
                        columns: x => new { x.GroupId, x.SeasonIndex, x.Day },
                        principalTable: "NewsEditions",
                        principalColumns: new[] { "GroupId", "SeasonIndex", "Day" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsArticles_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SqlJobRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Processed = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SqlJobRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SqlJobRuns_SqlJobStates_Name_SeasonIndex",
                        columns: x => new { x.Name, x.SeasonIndex },
                        principalTable: "SqlJobStates",
                        principalColumns: new[] { "Name", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SqlJobRuns_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedOperationalLogs_PartitionKey_RowKey",
                table: "ArchivedOperationalLogs",
                columns: new[] { "PartitionKey", "RowKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedOperationalLogs_TimestampUtc",
                table: "ArchivedOperationalLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HallBaselineConfirmations_GroupId_ActorUserId",
                table: "HallBaselineConfirmations",
                columns: new[] { "GroupId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_HallBaselineConfirmations_GroupId_LeagueId",
                table: "HallBaselineConfirmations",
                columns: new[] { "GroupId", "LeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_HallRankContributions_OwnerUserId",
                table: "HallRankContributions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_GeneratedByUserId",
                table: "NewsArticles",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_GroupId_LeagueId_SeasonIndex",
                table: "NewsArticles",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsEditions_SeasonIndex",
                table: "NewsEditions",
                column: "SeasonIndex");

            migrationBuilder.CreateIndex(
                name: "IX_SqlJobRuns_ActorUserId",
                table: "SqlJobRuns",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SqlJobRuns_Name_SeasonIndex",
                table: "SqlJobRuns",
                columns: new[] { "Name", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_SqlJobRuns_StartedAtUtc",
                table: "SqlJobRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SqlJobStates_SeasonIndex",
                table: "SqlJobStates",
                column: "SeasonIndex");

            migrationBuilder.CreateIndex(
                name: "IX_TeamAdvice_GeneratedByUserId",
                table: "TeamAdvice",
                column: "GeneratedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "MarketTradeEvents" WHERE "ActorUserId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade system market events by inventing a user identity. Reconcile explicitly before downgrade.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "ArchivedOperationalLogProperties");

            migrationBuilder.DropTable(
                name: "HallBaselineConfirmations");

            migrationBuilder.DropTable(
                name: "HallRankContributions");

            migrationBuilder.DropTable(
                name: "NewsArticles");

            migrationBuilder.DropTable(
                name: "SqlJobRuns");

            migrationBuilder.DropTable(
                name: "TeamAdvice");

            migrationBuilder.DropTable(
                name: "ArchivedOperationalLogs");

            migrationBuilder.DropTable(
                name: "NewsEditions");

            migrationBuilder.DropTable(
                name: "SqlJobStates");

            migrationBuilder.DropColumn(
                name: "ContributionsInitialized",
                table: "HallArchives");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "MarketTradeEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
