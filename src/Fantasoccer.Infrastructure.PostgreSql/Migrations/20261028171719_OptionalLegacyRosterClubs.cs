using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OptionalLegacyRosterClubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "RosterPlayers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "PlayedMatchPlayers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "HallWinningPlayers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "HallPlayerRecords",
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
            // Never invent a club to satisfy the old NOT NULL constraint.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM "RosterPlayers" WHERE "ClubCode" IS NULL)
                        OR EXISTS(SELECT 1 FROM "PlayedMatchPlayers" WHERE "ClubCode" IS NULL)
                        OR EXISTS(SELECT 1 FROM "HallWinningPlayers" WHERE "ClubCode" IS NULL)
                        OR EXISTS(SELECT 1 FROM "HallPlayerRecords" WHERE "ClubCode" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade legacy roster or hall snapshots without a recorded club: no club would be invented. Use a verified backup.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "RosterPlayers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "PlayedMatchPlayers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "HallWinningPlayers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClubCode",
                table: "HallPlayerRecords",
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
