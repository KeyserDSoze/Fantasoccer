using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CurrentOnlyFantasyStandings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing daily copies may contain historical financial values.
            // Never delete them merely to enforce the new storage policy.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM "FantasyStandingSnapshots" WHERE "SnapshotDay" <> 0) THEN
                        RAISE EXCEPTION 'Daily fantasy standings already exist. Preserve and verify a target backup, then explicitly reconcile the old daily copies before this upgrade. No standings or financial values were deleted.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_FantasyStandingSnapshots_Day",
                table: "FantasyStandingSnapshots");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FantasyStandingSnapshots_Day",
                table: "FantasyStandingSnapshots",
                sql: "\"SnapshotDay\" = 0 AND \"SerieADay\" BETWEEN 0 AND 38");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FantasyStandingSnapshots_Day",
                table: "FantasyStandingSnapshots");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FantasyStandingSnapshots_Day",
                table: "FantasyStandingSnapshots",
                sql: "\"SnapshotDay\" BETWEEN 0 AND 38 AND \"SerieADay\" BETWEEN 0 AND 38");
        }
    }
}
