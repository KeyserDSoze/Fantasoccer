using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class SeasonPlayerKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "PlayerSeasons",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Prefer the exact key already recorded by the legacy import. For
            // untracked data, only perform an unambiguous ASCII backfill; never
            // guess culture-dependent mappings or merge existing player IDs.
            var asciiCaseSupported = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToLower() == "abcdefghijklmnopqrstuvwxyz";
            migrationBuilder.Sql($$"""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT m."TargetId" FROM "LegacyIdentityMaps" m
                        WHERE m."Kind" = 'player' AND EXISTS (
                            SELECT 1 FROM "PlayerSeasons" s WHERE s."PlayerId" = m."TargetId")
                        GROUP BY m."TargetId"
                        HAVING count(DISTINCT m."LegacyKey") > 1 OR bool_or(m."LegacyKey" !~ '^[a-z]+$' OR length(m."LegacyKey") > 200)
                    ) THEN
                        RAISE EXCEPTION 'Cannot backfill ambiguous legacy player keys. Reconcile existing identities; no IDs were merged.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "PlayerSeasons" s JOIN "Players" p ON p."Id" = s."PlayerId"
                        WHERE NOT EXISTS (SELECT 1 FROM "LegacyIdentityMaps" m WHERE m."Kind" = 'player' AND m."TargetId" = s."PlayerId")
                          AND (octet_length(p."Name") <> length(p."Name") OR NOT {{(asciiCaseSupported ? "true" : "false")}})
                    ) THEN
                        RAISE EXCEPTION 'Cannot backfill player keys without a verifiable normalization. Reconcile existing identities; no IDs were changed.';
                    END IF;
                END $$;
                WITH recorded AS (
                    SELECT "TargetId", min("LegacyKey") AS key FROM "LegacyIdentityMaps"
                    WHERE "Kind" = 'player' GROUP BY "TargetId"
                )
                UPDATE "PlayerSeasons" s SET "NormalizedName" = coalesce(recorded.key,
                    regexp_replace(lower(p."Name" COLLATE "C"), '[^a-z]', '', 'g'))
                FROM "Players" p LEFT JOIN recorded ON recorded."TargetId" = p."Id"
                WHERE s."PlayerId" = p."Id";
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "PlayerSeasons" WHERE "NormalizedName" IS NULL
                        OR "NormalizedName" !~ '^[a-z]+$' OR length("NormalizedName") > 200)
                    OR EXISTS (SELECT 1 FROM "PlayerSeasons" GROUP BY "SeasonIndex", "NormalizedName" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Cannot establish unique seasonal player keys. Reconcile existing identities; no rows were merged or deleted.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName", table: "PlayerSeasons", type: "character varying(200)", maxLength: 200,
                nullable: false, oldClrType: typeof(string), oldType: "character varying(200)", oldMaxLength: 200, oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlayerKey",
                table: "PlayerSeasons",
                type: "character varying(220)",
                maxLength: 220,
                nullable: false,
                computedColumnSql: "\"SeasonIndex\"::text || ':' || \"NormalizedName\"",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasons_PlayerKey",
                table: "PlayerSeasons",
                column: "PlayerKey",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerSeasons_NormalizedName",
                table: "PlayerSeasons",
                sql: "\"NormalizedName\" ~ '^[a-z]+$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "PlayerSeasons") THEN
                        RAISE EXCEPTION 'Cannot remove seasonal player keys while player seasons exist. No identity mapping was discarded.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_PlayerSeasons_PlayerKey",
                table: "PlayerSeasons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerSeasons_NormalizedName",
                table: "PlayerSeasons");

            migrationBuilder.DropColumn(
                name: "PlayerKey",
                table: "PlayerSeasons");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "PlayerSeasons");
        }
    }
}
