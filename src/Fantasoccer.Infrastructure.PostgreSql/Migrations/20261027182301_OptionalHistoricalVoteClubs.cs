using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OptionalHistoricalVoteClubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "PushVoteBaselines",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "FootballVotes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Unknown snapshots cannot be replaced by an empty code or by a
            // player's present-day club merely to satisfy the old constraint.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM "FootballVotes" WHERE "ClubCode" IS NULL)
                        OR EXISTS(SELECT 1 FROM "PushVoteBaselines" WHERE "ClubCode" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade vote snapshots with unknown historical clubs. No clubs were guessed and no votes were discarded; use a verified backup.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "PushVoteBaselines",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "FootballVotes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);
        }
    }
}
