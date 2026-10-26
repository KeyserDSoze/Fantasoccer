using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DurablePushOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegacyPushActiveDays",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyPushActiveDays", x => x.Ordinal);
                    table.ForeignKey(
                        name: "FK_LegacyPushActiveDays_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PushDayCheckpoints",
                columns: table => new
                {
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    DeploymentReminder = table.Column<bool>(type: "boolean", nullable: false),
                    EndDay = table.Column<bool>(type: "boolean", nullable: false),
                    LiveChangesReminder = table.Column<bool>(type: "boolean", nullable: false),
                    HasMatchEventVotes = table.Column<bool>(type: "boolean", nullable: false),
                    MatchEventSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDayCheckpoints", x => new { x.SeasonIndex, x.Day });
                    table.CheckConstraint("CK_PushDayCheckpoints_Day", "\"Day\" BETWEEN 0 AND 38");
                    table.ForeignKey(
                        name: "FK_PushDayCheckpoints_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PushDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "text", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    HasData = table.Column<bool>(type: "boolean", nullable: false),
                    Uri = table.Column<string>(type: "text", nullable: true),
                    LegacyRecipientEmail = table.Column<string>(type: "text", nullable: true),
                    Icon = table.Column<string>(type: "text", nullable: true),
                    Badge = table.Column<string>(type: "text", nullable: true),
                    Sound = table.Column<string>(type: "text", nullable: true),
                    Tag = table.Column<string>(type: "text", nullable: true),
                    RequireInteraction = table.Column<bool>(type: "boolean", nullable: false),
                    LegacyId = table.Column<string>(type: "text", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    QuarantineReason = table.Column<string>(type: "text", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDeliveries", x => x.Id);
                    table.CheckConstraint("CK_PushDeliveries_Day", "\"Day\" BETWEEN 0 AND 38");
                    table.CheckConstraint("CK_PushDeliveries_Scope", "(\"GroupId\" IS NULL) = (\"LeagueId\" IS NULL) AND (\"TeamId\" IS NULL) = (\"BasketId\" IS NULL) AND (\"TeamId\" IS NULL OR \"GroupId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PushDeliveries_FantasyTeamSeasons_GroupId_BasketId_SeasonIn~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushDeliveries_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushDeliveries_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushDeliveries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacyFormationPushReceipts",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Receipt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyFormationPushReceipts", x => new { x.SeasonIndex, x.Day, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_LegacyFormationPushReceipts_PushDayCheckpoints_SeasonIndex_~",
                        columns: x => new { x.SeasonIndex, x.Day },
                        principalTable: "PushDayCheckpoints",
                        principalColumns: new[] { "SeasonIndex", "Day" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushVoteBaselines",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_PushVoteBaselines", x => new { x.SeasonIndex, x.Day, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_PushVoteBaselines_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushVoteBaselines_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushVoteBaselines_PushDayCheckpoints_SeasonIndex_Day",
                        columns: x => new { x.SeasonIndex, x.Day },
                        principalTable: "PushDayCheckpoints",
                        principalColumns: new[] { "SeasonIndex", "Day" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushDeliveryScopeConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Uri = table.Column<string>(type: "text", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDeliveryScopeConfirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushDeliveryScopeConfirmations_LeagueSeasons_GroupId_League~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushDeliveryScopeConfirmations_PushDeliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "PushDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushDeliveryScopeConfirmations_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PushEndpointReceipts",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointHash = table.Column<string>(type: "text", nullable: false),
                    LegacyEndpoint = table.Column<string>(type: "text", nullable: true),
                    BindingVersion = table.Column<long>(type: "bigint", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushEndpointReceipts", x => new { x.DeliveryId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_PushEndpointReceipts_PushDeliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "PushDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyPushActiveDays_SeasonIndex",
                table: "LegacyPushActiveDays",
                column: "SeasonIndex");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveries_DeduplicationKey",
                table: "PushDeliveries",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveries_GroupId_BasketId_SeasonIndex_TeamId",
                table: "PushDeliveries",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveries_GroupId_LeagueId_SeasonIndex",
                table: "PushDeliveries",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveries_SeasonIndex",
                table: "PushDeliveries",
                column: "SeasonIndex");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveries_Status_NextAttemptAtUtc_LeaseUntilUtc",
                table: "PushDeliveries",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveries_UserId",
                table: "PushDeliveries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveryScopeConfirmations_ActorUserId",
                table: "PushDeliveryScopeConfirmations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveryScopeConfirmations_DeliveryId",
                table: "PushDeliveryScopeConfirmations",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveryScopeConfirmations_GroupId_LeagueId_SeasonIndex",
                table: "PushDeliveryScopeConfirmations",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PushVoteBaselines_ClubId",
                table: "PushVoteBaselines",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_PushVoteBaselines_PlayerId_SeasonIndex",
                table: "PushVoteBaselines",
                columns: new[] { "PlayerId", "SeasonIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyFormationPushReceipts");

            migrationBuilder.DropTable(
                name: "LegacyPushActiveDays");

            migrationBuilder.DropTable(
                name: "PushDeliveryScopeConfirmations");

            migrationBuilder.DropTable(
                name: "PushEndpointReceipts");

            migrationBuilder.DropTable(
                name: "PushVoteBaselines");

            migrationBuilder.DropTable(
                name: "PushDeliveries");

            migrationBuilder.DropTable(
                name: "PushDayCheckpoints");
        }
    }
}
