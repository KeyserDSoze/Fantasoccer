using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PostponedMatchPoliciesAndLegacyVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComplete",
                table: "OpeningStandingSnapshots",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<float>(
                name: "PoliticalDefender",
                table: "LeagueRules",
                type: "real",
                nullable: false,
                defaultValue: 6f);

            migrationBuilder.AddColumn<float>(
                name: "PoliticalForward",
                table: "LeagueRules",
                type: "real",
                nullable: false,
                defaultValue: 6f);

            migrationBuilder.AddColumn<float>(
                name: "PoliticalGoalkeeper",
                table: "LeagueRules",
                type: "real",
                nullable: false,
                defaultValue: 6f);

            migrationBuilder.AddColumn<float>(
                name: "PoliticalMidfielder",
                table: "LeagueRules",
                type: "real",
                nullable: false,
                defaultValue: 6f);

            migrationBuilder.AddColumn<bool>(
                name: "WaitForPostponedMatches",
                table: "LeagueRules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ActualOfficialVotesReady",
                table: "FootballMatches",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinished",
                table: "FootballMatches",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OriginalKickoffUtc",
                table: "FootballMatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasPostponed",
                table: "FootballMatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRecoveryCheckUtc",
                table: "FootballMatchdays",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AwaitingPostponedMatches",
                table: "FantasyMatchdays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LegacyPoliticalVoteEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArchiveReference = table.Column<string>(type: "text", nullable: false),
                    JsonPath = table.Column<string>(type: "text", nullable: false),
                    SourceOrdinal = table.Column<int>(type: "integer", nullable: false),
                    PlayerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    ClubCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    OriginalPayloadJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyPoliticalVoteEntries", x => x.Id);
                    table.CheckConstraint("CK_LegacyPoliticalVotes_Day", "\"Day\" BETWEEN 1 AND 38");
                    table.ForeignKey(
                        name: "FK_LegacyPoliticalVoteEntries_Clubs_ClubCode",
                        column: x => x.ClubCode,
                        principalTable: "Clubs",
                        principalColumn: "ClubCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegacyPoliticalVoteEntries_PlayerSeasons_PlayerKey_SeasonIn~",
                        columns: x => new { x.PlayerKey, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerKey", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeagueRules_PoliticalVotes",
                table: "LeagueRules",
                sql: "\"PoliticalGoalkeeper\" BETWEEN 0 AND 10 AND \"PoliticalDefender\" BETWEEN 0 AND 10 AND \"PoliticalMidfielder\" BETWEEN 0 AND 10 AND \"PoliticalForward\" BETWEEN 0 AND 10");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyPoliticalVoteEntries_ClubCode",
                table: "LegacyPoliticalVoteEntries",
                column: "ClubCode");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyPoliticalVoteEntries_PlayerKey_SeasonIndex",
                table: "LegacyPoliticalVoteEntries",
                columns: new[] { "PlayerKey", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyPoliticalVoteEntries_SeasonIndex_Day",
                table: "LegacyPoliticalVoteEntries",
                columns: new[] { "SeasonIndex", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyPoliticalVoteEntries_SourceId_DocumentSha256_SourceOr~",
                table: "LegacyPoliticalVoteEntries",
                columns: new[] { "SourceId", "DocumentSha256", "SourceOrdinal" },
                unique: true);

            // Preserve only attested information: the prior Delayed flag and
            // the best kickoff known at upgrade time. Do not invent completion
            // or reclassify old official rows from a numeric value alone.
            migrationBuilder.Sql("""
                UPDATE "FootballMatches"
                SET "WasPostponed" = "Delayed", "OriginalKickoffUtc" = "KickoffUtc",
                    "ActualOfficialVotesReady" = CASE WHEN "Delayed" THEN false ELSE NULL END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM "LegacyPoliticalVoteEntries")
                        OR EXISTS(SELECT 1 FROM "LeagueRules" WHERE "WaitForPostponedMatches"
                            OR "PoliticalGoalkeeper" <> 6 OR "PoliticalDefender" <> 6 OR "PoliticalMidfielder" <> 6 OR "PoliticalForward" <> 6)
                        OR EXISTS(SELECT 1 FROM "OpeningStandingSnapshots" WHERE NOT "IsComplete")
                        OR EXISTS(SELECT 1 FROM "FantasyMatchdays" WHERE "AwaitingPostponedMatches")
                        OR EXISTS(SELECT 1 FROM "FootballMatches" WHERE "WasPostponed" <> "Delayed" OR "IsFinished" IS NOT NULL
                            OR "ActualOfficialVotesReady" = true OR ("ActualOfficialVotesReady" = false AND NOT "Delayed")
                            OR ("OriginalKickoffUtc" IS NOT NULL AND "OriginalKickoffUtc" IS DISTINCT FROM "KickoffUtc")) THEN
                        RAISE EXCEPTION 'Cannot downgrade postponement policies, preserved legacy substitutes, pending recoveries or attested match history without losing information. No votes, results, balances or settings were discarded; use a verified backup.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "LegacyPoliticalVoteEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeagueRules_PoliticalVotes",
                table: "LeagueRules");

            migrationBuilder.DropColumn(
                name: "IsComplete",
                table: "OpeningStandingSnapshots");

            migrationBuilder.DropColumn(
                name: "PoliticalDefender",
                table: "LeagueRules");

            migrationBuilder.DropColumn(
                name: "PoliticalForward",
                table: "LeagueRules");

            migrationBuilder.DropColumn(
                name: "PoliticalGoalkeeper",
                table: "LeagueRules");

            migrationBuilder.DropColumn(
                name: "PoliticalMidfielder",
                table: "LeagueRules");

            migrationBuilder.DropColumn(
                name: "WaitForPostponedMatches",
                table: "LeagueRules");

            migrationBuilder.DropColumn(
                name: "ActualOfficialVotesReady",
                table: "FootballMatches");

            migrationBuilder.DropColumn(
                name: "IsFinished",
                table: "FootballMatches");

            migrationBuilder.DropColumn(
                name: "OriginalKickoffUtc",
                table: "FootballMatches");

            migrationBuilder.DropColumn(
                name: "WasPostponed",
                table: "FootballMatches");

            migrationBuilder.DropColumn(
                name: "LastRecoveryCheckUtc",
                table: "FootballMatchdays");

            migrationBuilder.DropColumn(
                name: "AwaitingPostponedMatches",
                table: "FantasyMatchdays");
        }
    }
}
