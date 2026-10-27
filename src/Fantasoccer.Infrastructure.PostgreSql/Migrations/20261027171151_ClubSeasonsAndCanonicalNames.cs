using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ClubSeasonsAndCanonicalNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClubSeasons",
                columns: table => new
                {
                    ClubCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Sources = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubSeasons", x => new { x.ClubCode, x.SeasonIndex });
                    table.CheckConstraint("CK_ClubSeasons_Sources", "\"Sources\" BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_ClubSeasons_Clubs_ClubCode",
                        column: x => x.ClubCode,
                        principalTable: "Clubs",
                        principalColumn: "ClubCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClubSeasons_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            // Freeze the verified canonical names in this migration. Later
            // catalogue changes must not change previously published SQL.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "_CanonicalClubNames" ON COMMIT DROP AS
                SELECT * FROM (VALUES
                    ('ata','Atalanta'), ('ben','Benevento'), ('bol','Bologna'), ('bre','Brescia'),
                    ('cag','Cagliari'), ('car','Carpi'), ('cat','Catania'), ('ces','Cesena'),
                    ('chi','Chievo'), ('com','Como'), ('cre','Cremonese'), ('cro','Crotone'),
                    ('emp','Empoli'), ('fio','Fiorentina'), ('fro','Frosinone'), ('gen','Genoa'),
                    ('int','Inter'), ('juv','Juventus'), ('laz','Lazio'), ('lec','Lecce'),
                    ('liv','Livorno'), ('mil','Milan'), ('mon','Monza'), ('nap','Napoli'),
                    ('pal','Palermo'), ('par','Parma'), ('pes','Pescara'), ('pis','Pisa'),
                    ('rom','Roma'), ('sal','Salernitana'), ('sam','Sampdoria'), ('sas','Sassuolo'),
                    ('sie','Siena'), ('spa','SPAL'), ('spe','Spezia'), ('tor','Torino'),
                    ('udi','Udinese'), ('ven','Venezia'), ('ver','Verona')
                ) AS n("ClubCode","Name");
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Clubs" c JOIN (
                            SELECT "ClubCode", "Name" FROM "_CanonicalClubNames"
                            UNION ALL SELECT 'juv', 'Juve'
                        ) n ON lower(btrim(c."Name") COLLATE "C") = lower(n."Name" COLLATE "C")
                        WHERE c."ClubCode" <> n."ClubCode"
                    ) OR EXISTS (
                        SELECT 1 FROM "Clubs" WHERE length(btrim("Abbreviation")) > 0
                            AND lower("Abbreviation" COLLATE "C") <> "ClubCode"
                    ) THEN
                        RAISE EXCEPTION 'Existing conflicting club names or codes require reconciliation. No identities were merged or changed.';
                    END IF;
                END $$;
                UPDATE "Clubs" SET "Name" = btrim("Name"), "Abbreviation" = "ClubCode";
                UPDATE "Clubs" c SET "Name" = n."Name"
                FROM "_CanonicalClubNames" n WHERE c."ClubCode" = n."ClubCode";
                DROP TABLE "_CanonicalClubNames";

                -- A player or historical roster reference is NOT proof that a
                -- club played in Serie A that year. Only real fixtures/ranks
                -- can be recovered from the pre-existing relational schema.
                INSERT INTO "ClubSeasons" ("ClubCode","SeasonIndex","Sources")
                SELECT e."ClubCode", e."SeasonIndex", bit_or(e."Source")
                FROM (
                    SELECT "HomeClubCode" AS "ClubCode", "SeasonIndex", 2 AS "Source" FROM "FootballMatches"
                    UNION ALL SELECT "AwayClubCode", "SeasonIndex", 2 FROM "FootballMatches"
                    UNION ALL SELECT "ClubCode", "SeasonIndex", 4 FROM "FootballClubRanks"
                ) e GROUP BY e."ClubCode", e."SeasonIndex";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clubs_Abbreviation",
                table: "Clubs",
                sql: "\"Abbreviation\" = \"ClubCode\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clubs_CanonicalName",
                table: "Clubs",
                sql: "\"Name\" = CASE \"ClubCode\" WHEN 'ata' THEN 'Atalanta' WHEN 'ben' THEN 'Benevento' WHEN 'bol' THEN 'Bologna' WHEN 'bre' THEN 'Brescia' WHEN 'cag' THEN 'Cagliari' WHEN 'car' THEN 'Carpi' WHEN 'cat' THEN 'Catania' WHEN 'ces' THEN 'Cesena' WHEN 'chi' THEN 'Chievo' WHEN 'com' THEN 'Como' WHEN 'cre' THEN 'Cremonese' WHEN 'cro' THEN 'Crotone' WHEN 'emp' THEN 'Empoli' WHEN 'fio' THEN 'Fiorentina' WHEN 'fro' THEN 'Frosinone' WHEN 'gen' THEN 'Genoa' WHEN 'int' THEN 'Inter' WHEN 'juv' THEN 'Juventus' WHEN 'laz' THEN 'Lazio' WHEN 'lec' THEN 'Lecce' WHEN 'liv' THEN 'Livorno' WHEN 'mil' THEN 'Milan' WHEN 'mon' THEN 'Monza' WHEN 'nap' THEN 'Napoli' WHEN 'pal' THEN 'Palermo' WHEN 'par' THEN 'Parma' WHEN 'pes' THEN 'Pescara' WHEN 'pis' THEN 'Pisa' WHEN 'rom' THEN 'Roma' WHEN 'sal' THEN 'Salernitana' WHEN 'sam' THEN 'Sampdoria' WHEN 'sas' THEN 'Sassuolo' WHEN 'sie' THEN 'Siena' WHEN 'spa' THEN 'SPAL' WHEN 'spe' THEN 'Spezia' WHEN 'tor' THEN 'Torino' WHEN 'udi' THEN 'Udinese' WHEN 'ven' THEN 'Venezia' WHEN 'ver' THEN 'Verona' ELSE \"Name\" END");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clubs_Name",
                table: "Clubs",
                sql: "length(btrim(\"Name\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ClubSeasons_SeasonIndex",
                table: "ClubSeasons",
                column: "SeasonIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Neither annual membership nor the previous first-seen alias can
            // be restored by guessing. Allow a complete empty-schema teardown.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM "Clubs") OR EXISTS(SELECT 1 FROM "Players") OR EXISTS(SELECT 1 FROM "ClubSeasons") THEN
                        RAISE EXCEPTION 'Cannot downgrade populated canonical clubs, players or club seasons. Use a verified backup; no memberships or names were discarded.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "ClubSeasons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clubs_Abbreviation",
                table: "Clubs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clubs_CanonicalName",
                table: "Clubs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clubs_Name",
                table: "Clubs");
        }
    }
}
