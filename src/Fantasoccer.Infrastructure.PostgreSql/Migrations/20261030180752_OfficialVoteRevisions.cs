using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OfficialVoteRevisions : Migration
    {
        // Statement-level triggers with transition tables: one counter bump per
        // affected season/day per statement, whoever writes the votes (API, jobs,
        // importer, raw SQL). Rows are bumped in key order so two concurrent writers
        // can never lock the same days in opposite order. Only official votes
        // (Source = 2) count: live and push baselines are not scoring inputs.
        internal const string TriggersSql = """
            CREATE OR REPLACE FUNCTION fz_official_votes_changed() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    INSERT INTO "FootballDayRevisions" ("SeasonIndex", "Day", "OfficialRevision")
                    SELECT DISTINCT n."SeasonIndex", n."Day", 1 FROM new_rows n WHERE n."Source" = 2 ORDER BY 1, 2
                    ON CONFLICT ("SeasonIndex", "Day") DO UPDATE SET "OfficialRevision" = "FootballDayRevisions"."OfficialRevision" + 1;
                ELSIF TG_OP = 'DELETE' THEN
                    INSERT INTO "FootballDayRevisions" ("SeasonIndex", "Day", "OfficialRevision")
                    SELECT DISTINCT o."SeasonIndex", o."Day", 1 FROM old_rows o WHERE o."Source" = 2 ORDER BY 1, 2
                    ON CONFLICT ("SeasonIndex", "Day") DO UPDATE SET "OfficialRevision" = "FootballDayRevisions"."OfficialRevision" + 1;
                ELSE
                    INSERT INTO "FootballDayRevisions" ("SeasonIndex", "Day", "OfficialRevision")
                    SELECT c."SeasonIndex", c."Day", 1 FROM (
                        SELECT o."SeasonIndex", o."Day" FROM old_rows o WHERE o."Source" = 2
                        UNION
                        SELECT n."SeasonIndex", n."Day" FROM new_rows n WHERE n."Source" = 2) c ORDER BY 1, 2
                    ON CONFLICT ("SeasonIndex", "Day") DO UPDATE SET "OfficialRevision" = "FootballDayRevisions"."OfficialRevision" + 1;
                END IF;
                RETURN NULL;
            END $$;

            CREATE TRIGGER fz_official_votes_inserted AFTER INSERT ON "FootballVotes"
                REFERENCING NEW TABLE AS new_rows FOR EACH STATEMENT EXECUTE FUNCTION fz_official_votes_changed();
            CREATE TRIGGER fz_official_votes_updated AFTER UPDATE ON "FootballVotes"
                REFERENCING OLD TABLE AS old_rows NEW TABLE AS new_rows FOR EACH STATEMENT EXECUTE FUNCTION fz_official_votes_changed();
            CREATE TRIGGER fz_official_votes_deleted AFTER DELETE ON "FootballVotes"
                REFERENCING OLD TABLE AS old_rows FOR EACH STATEMENT EXECUTE FUNCTION fz_official_votes_changed();
            """;

        internal const string DropTriggersSql = """
            DROP TRIGGER IF EXISTS fz_official_votes_deleted ON "FootballVotes";
            DROP TRIGGER IF EXISTS fz_official_votes_updated ON "FootballVotes";
            DROP TRIGGER IF EXISTS fz_official_votes_inserted ON "FootballVotes";
            DROP FUNCTION IF EXISTS fz_official_votes_changed();
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FootballDayRevisions",
                columns: table => new
                {
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    OfficialRevision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballDayRevisions", x => new { x.SeasonIndex, x.Day });
                    table.CheckConstraint("CK_FootballDayRevisions_Day", "\"Day\" BETWEEN 1 AND 38");
                    table.ForeignKey(
                        name: "FK_FootballDayRevisions_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Cascade);
                });

            // Days that already hold official votes start at revision 1.
            migrationBuilder.Sql("""
                INSERT INTO "FootballDayRevisions" ("SeasonIndex", "Day", "OfficialRevision")
                SELECT DISTINCT "SeasonIndex", "Day", 1 FROM "FootballVotes" WHERE "Source" = 2;
                """);
            migrationBuilder.Sql(TriggersSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropTriggersSql);
            migrationBuilder.DropTable(
                name: "FootballDayRevisions");
        }
    }
}
