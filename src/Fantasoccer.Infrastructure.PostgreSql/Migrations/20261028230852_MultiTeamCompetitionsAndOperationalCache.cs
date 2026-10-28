using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class MultiTeamCompetitionsAndOperationalCache : Migration
    {
        // Database-level guarantees that must hold for API calls, jobs, importer and
        // raw SQL alike. Error messages carry an "fz:" code mapped to API faults.
        internal const string IntegritySql = """
            CREATE OR REPLACE FUNCTION fz_authoritative_team(p_group uuid, p_team uuid) RETURNS uuid LANGUAGE sql STABLE AS $$
                SELECT COALESCE((SELECT d."OriginTeamId" FROM "TeamDerivations" d WHERE d."GroupId" = p_group AND d."TeamId" = p_team), p_team)
            $$;

            CREATE OR REPLACE FUNCTION fz_sync_participant_owners(p_group uuid, p_league uuid, p_season integer, p_team uuid) RETURNS void LANGUAGE plpgsql AS $$
            BEGIN
                DELETE FROM "LeagueParticipantOwners"
                WHERE "GroupId" = p_group AND "LeagueId" = p_league AND "SeasonIndex" = p_season AND "TeamId" = p_team;
                -- A derived team never has its own owners: they are those of its origin.
                INSERT INTO "LeagueParticipantOwners" ("GroupId", "LeagueId", "SeasonIndex", "UserId", "TeamId")
                SELECT DISTINCT p."GroupId", p."LeagueId", p."SeasonIndex", o."UserId", p."TeamId"
                FROM "LeagueParticipations" p
                JOIN "TeamOwnerships" o ON o."GroupId" = p."GroupId" AND o."TeamId" = fz_authoritative_team(p."GroupId", p."TeamId")
                WHERE p."GroupId" = p_group AND p."LeagueId" = p_league AND p."SeasonIndex" = p_season AND p."TeamId" = p_team;
            END $$;

            CREATE OR REPLACE FUNCTION fz_sync_team_participations(p_group uuid, p_team uuid) RETURNS void LANGUAGE plpgsql AS $$
            DECLARE r record;
            BEGIN
                FOR r IN SELECT p."GroupId", p."LeagueId", p."SeasonIndex", p."TeamId" FROM "LeagueParticipations" p
                         WHERE p."GroupId" = p_group AND (p."TeamId" = p_team OR p."TeamId" IN
                             (SELECT d."TeamId" FROM "TeamDerivations" d WHERE d."GroupId" = p_group AND d."OriginTeamId" = p_team))
                LOOP
                    PERFORM fz_sync_participant_owners(r."GroupId", r."LeagueId", r."SeasonIndex", r."TeamId");
                END LOOP;
            END $$;

            CREATE OR REPLACE FUNCTION fz_league_participations_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE v_kind integer;
            BEGIN
                SELECT b."Kind" INTO v_kind FROM "Baskets" b WHERE b."GroupId" = NEW."GroupId" AND b."BasketId" = NEW."BasketId";
                IF v_kind = 1 AND NOT EXISTS (SELECT 1 FROM "TeamDerivations" d WHERE d."GroupId" = NEW."GroupId" AND d."TeamId" = NEW."TeamId") THEN
                    RAISE EXCEPTION 'fz:derived_basket_team_without_origin' USING ERRCODE = 'check_violation';
                END IF;
                IF NEW."Source" = 2 AND NOT EXISTS (
                    SELECT 1 FROM "LeagueParticipations" s
                    WHERE s."GroupId" = NEW."GroupId" AND s."LeagueId" = NEW."SourceLeagueId" AND s."SeasonIndex" = NEW."SeasonIndex"
                      AND fz_authoritative_team(s."GroupId", s."TeamId") = fz_authoritative_team(NEW."GroupId", NEW."TeamId")) THEN
                    RAISE EXCEPTION 'fz:qualification_without_source_participation' USING ERRCODE = 'check_violation';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fz_league_participations_guard BEFORE INSERT OR UPDATE ON "LeagueParticipations"
                FOR EACH ROW EXECUTE FUNCTION fz_league_participations_guard();

            CREATE OR REPLACE FUNCTION fz_league_participations_owners() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                PERFORM fz_sync_participant_owners(NEW."GroupId", NEW."LeagueId", NEW."SeasonIndex", NEW."TeamId");
                RETURN NULL;
            END $$;
            CREATE TRIGGER fz_league_participations_owners AFTER INSERT ON "LeagueParticipations"
                FOR EACH ROW EXECUTE FUNCTION fz_league_participations_owners();

            CREATE OR REPLACE FUNCTION fz_team_ownerships_changed() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP <> 'DELETE' AND EXISTS (SELECT 1 FROM "TeamDerivations" d WHERE d."GroupId" = NEW."GroupId" AND d."TeamId" = NEW."TeamId") THEN
                    RAISE EXCEPTION 'fz:derived_team_ownership' USING ERRCODE = 'check_violation';
                END IF;
                IF TG_OP <> 'INSERT' THEN PERFORM fz_sync_team_participations(OLD."GroupId", OLD."TeamId"); END IF;
                IF TG_OP <> 'DELETE' THEN PERFORM fz_sync_team_participations(NEW."GroupId", NEW."TeamId"); END IF;
                RETURN NULL;
            END $$;
            CREATE TRIGGER fz_team_ownerships_changed AFTER INSERT OR UPDATE OR DELETE ON "TeamOwnerships"
                FOR EACH ROW EXECUTE FUNCTION fz_team_ownerships_changed();

            CREATE OR REPLACE FUNCTION fz_team_derivations_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP = 'UPDATE' THEN
                    RAISE EXCEPTION 'fz:team_derivation_immutable' USING ERRCODE = 'check_violation';
                END IF;
                IF EXISTS (SELECT 1 FROM "TeamOwnerships" o WHERE o."GroupId" = NEW."GroupId" AND o."TeamId" = NEW."TeamId")
                   OR EXISTS (SELECT 1 FROM "RosterSnapshots" r WHERE r."GroupId" = NEW."GroupId" AND r."TeamId" = NEW."TeamId") THEN
                    RAISE EXCEPTION 'fz:derived_team_has_own_data' USING ERRCODE = 'check_violation';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fz_team_derivations_guard BEFORE INSERT OR UPDATE ON "TeamDerivations"
                FOR EACH ROW EXECUTE FUNCTION fz_team_derivations_guard();

            CREATE OR REPLACE FUNCTION fz_team_derivations_changed() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE r record; v_group uuid; v_team uuid;
            BEGIN
                IF TG_OP = 'DELETE' THEN v_group := OLD."GroupId"; v_team := OLD."TeamId"; ELSE v_group := NEW."GroupId"; v_team := NEW."TeamId"; END IF;
                FOR r IN SELECT p."LeagueId", p."SeasonIndex" FROM "LeagueParticipations" p WHERE p."GroupId" = v_group AND p."TeamId" = v_team LOOP
                    PERFORM fz_sync_participant_owners(v_group, r."LeagueId", r."SeasonIndex", v_team);
                END LOOP;
                RETURN NULL;
            END $$;
            CREATE TRIGGER fz_team_derivations_changed AFTER INSERT OR DELETE ON "TeamDerivations"
                FOR EACH ROW EXECUTE FUNCTION fz_team_derivations_changed();

            -- Economy (season roster, prices, credits) exists only on the origin team.
            CREATE OR REPLACE FUNCTION fz_roster_snapshots_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF EXISTS (SELECT 1 FROM "TeamDerivations" d WHERE d."GroupId" = NEW."GroupId" AND d."TeamId" = NEW."TeamId") THEN
                    RAISE EXCEPTION 'fz:derived_team_roster' USING ERRCODE = 'check_violation';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fz_roster_snapshots_guard BEFORE INSERT ON "RosterSnapshots"
                FOR EACH ROW EXECUTE FUNCTION fz_roster_snapshots_guard();

            CREATE OR REPLACE FUNCTION fz_league_basket_seasons_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE v_mode integer; v_kind integer;
            BEGIN
                -- Missing rows (cross-group/season links) are left to the composite FKs.
                SELECT l."Mode" INTO v_mode FROM "Leagues" l WHERE l."GroupId" = NEW."GroupId" AND l."LeagueId" = NEW."LeagueId";
                SELECT b."Kind" INTO v_kind FROM "Baskets" b WHERE b."GroupId" = NEW."GroupId" AND b."BasketId" = NEW."BasketId";
                IF v_mode IS NOT NULL AND v_kind IS NOT NULL AND v_mode <> v_kind THEN
                    RAISE EXCEPTION 'fz:league_basket_kind_mismatch' USING ERRCODE = 'check_violation';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fz_league_basket_seasons_guard BEFORE INSERT OR UPDATE ON "LeagueBasketSeasons"
                FOR EACH ROW EXECUTE FUNCTION fz_league_basket_seasons_guard();

            CREATE OR REPLACE FUNCTION fz_leagues_mode_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD."Mode" IS DISTINCT FROM NEW."Mode" AND (
                    EXISTS (SELECT 1 FROM "LeagueBasketSeasons" x WHERE x."GroupId" = NEW."GroupId" AND x."LeagueId" = NEW."LeagueId") OR
                    EXISTS (SELECT 1 FROM "LeagueDerivations" x WHERE x."GroupId" = NEW."GroupId" AND (x."LeagueId" = NEW."LeagueId" OR x."SourceLeagueId" = NEW."LeagueId"))) THEN
                    RAISE EXCEPTION 'fz:league_mode_locked' USING ERRCODE = 'check_violation';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fz_leagues_mode_guard BEFORE UPDATE ON "Leagues"
                FOR EACH ROW EXECUTE FUNCTION fz_leagues_mode_guard();

            CREATE OR REPLACE FUNCTION fz_league_derivations_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                -- Serialize derivation changes so two concurrent edges cannot form a cycle.
                PERFORM pg_advisory_xact_lock(59127502874020);
                IF NOT EXISTS (SELECT 1 FROM "Leagues" l WHERE l."GroupId" = NEW."GroupId" AND l."LeagueId" = NEW."LeagueId" AND l."Mode" = 1) THEN
                    RAISE EXCEPTION 'fz:league_derivation_requires_derived_league' USING ERRCODE = 'check_violation';
                END IF;
                IF EXISTS (
                    WITH RECURSIVE chain(league) AS (
                        SELECT NEW."SourceLeagueId"
                        UNION
                        SELECT d."SourceLeagueId" FROM "LeagueDerivations" d JOIN chain c ON d."LeagueId" = c.league
                        WHERE d."GroupId" = NEW."GroupId" AND d."SeasonIndex" = NEW."SeasonIndex")
                    SELECT 1 FROM chain WHERE league = NEW."LeagueId") THEN
                    RAISE EXCEPTION 'fz:league_derivation_cycle' USING ERRCODE = 'check_violation';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fz_league_derivations_guard BEFORE INSERT OR UPDATE ON "LeagueDerivations"
                FOR EACH ROW EXECUTE FUNCTION fz_league_derivations_guard();
            """;

        internal const string DropIntegritySql = """
            DROP TRIGGER IF EXISTS fz_league_derivations_guard ON "LeagueDerivations";
            DROP TRIGGER IF EXISTS fz_leagues_mode_guard ON "Leagues";
            DROP TRIGGER IF EXISTS fz_league_basket_seasons_guard ON "LeagueBasketSeasons";
            DROP TRIGGER IF EXISTS fz_roster_snapshots_guard ON "RosterSnapshots";
            DROP TRIGGER IF EXISTS fz_team_derivations_changed ON "TeamDerivations";
            DROP TRIGGER IF EXISTS fz_team_derivations_guard ON "TeamDerivations";
            DROP TRIGGER IF EXISTS fz_team_ownerships_changed ON "TeamOwnerships";
            DROP TRIGGER IF EXISTS fz_league_participations_owners ON "LeagueParticipations";
            DROP TRIGGER IF EXISTS fz_league_participations_guard ON "LeagueParticipations";
            DROP FUNCTION IF EXISTS fz_league_derivations_guard();
            DROP FUNCTION IF EXISTS fz_leagues_mode_guard();
            DROP FUNCTION IF EXISTS fz_league_basket_seasons_guard();
            DROP FUNCTION IF EXISTS fz_roster_snapshots_guard();
            DROP FUNCTION IF EXISTS fz_team_derivations_changed();
            DROP FUNCTION IF EXISTS fz_team_derivations_guard();
            DROP FUNCTION IF EXISTS fz_team_ownerships_changed();
            DROP FUNCTION IF EXISTS fz_league_participations_owners();
            DROP FUNCTION IF EXISTS fz_league_participations_guard();
            DROP FUNCTION IF EXISTS fz_sync_team_participations(uuid, uuid);
            DROP FUNCTION IF EXISTS fz_sync_participant_owners(uuid, uuid, integer, uuid);
            DROP FUNCTION IF EXISTS fz_authoritative_team(uuid, uuid);
            """;
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PushFormationChangeReceipts",
                table: "PushFormationChangeReceipts");

            migrationBuilder.AddColumn<Guid>(
                name: "LeagueId",
                table: "PushFormationChangeReceipts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAtUtc",
                table: "PushDayCheckpoints",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Baskets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PushFormationChangeReceipts",
                table: "PushFormationChangeReceipts",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Day", "LeagueId", "ChangeOrdinal" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Baskets_GroupId_BasketId_Kind",
                table: "Baskets",
                columns: new[] { "GroupId", "BasketId", "Kind" });

            migrationBuilder.CreateTable(
                name: "BasketDerivations",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginBasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    DerivedKind = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    OriginKind = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketDerivations", x => new { x.GroupId, x.BasketId });
                    table.UniqueConstraint("AK_BasketDerivations_GroupId_BasketId_OriginBasketId", x => new { x.GroupId, x.BasketId, x.OriginBasketId });
                    table.CheckConstraint("CK_BasketDerivations_Kinds", "\"DerivedKind\" = 1 AND \"OriginKind\" = 0 AND \"BasketId\" <> \"OriginBasketId\"");
                    table.ForeignKey(
                        name: "FK_BasketDerivations_Baskets_GroupId_BasketId_DerivedKind",
                        columns: x => new { x.GroupId, x.BasketId, x.DerivedKind },
                        principalTable: "Baskets",
                        principalColumns: new[] { "GroupId", "BasketId", "Kind" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BasketDerivations_Baskets_GroupId_OriginBasketId_OriginKind",
                        columns: x => new { x.GroupId, x.OriginBasketId, x.OriginKind },
                        principalTable: "Baskets",
                        principalColumns: new[] { "GroupId", "BasketId", "Kind" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueDerivations",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    SourceLeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    QualifiedTeams = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueDerivations", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SourceLeagueId });
                    table.CheckConstraint("CK_LeagueDerivations_Distinct", "\"LeagueId\" <> \"SourceLeagueId\"");
                    table.CheckConstraint("CK_LeagueDerivations_Qualified", "\"QualifiedTeams\" BETWEEN 1 AND 64");
                    table.ForeignKey(
                        name: "FK_LeagueDerivations_LeagueSeasons_GroupId_LeagueId_SeasonIndex",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeagueDerivations_LeagueSeasons_GroupId_SourceLeagueId_Seas~",
                        columns: x => new { x.GroupId, x.SourceLeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamDerivations",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginBasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamDerivations", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId });
                    table.CheckConstraint("CK_TeamDerivations_Distinct", "\"TeamId\" <> \"OriginTeamId\"");
                    table.ForeignKey(
                        name: "FK_TeamDerivations_BasketDerivations_GroupId_BasketId_OriginBa~",
                        columns: x => new { x.GroupId, x.BasketId, x.OriginBasketId },
                        principalTable: "BasketDerivations",
                        principalColumns: new[] { "GroupId", "BasketId", "OriginBasketId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamDerivations_FantasyTeamSeasons_GroupId_BasketId_SeasonI~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamDerivations_FantasyTeamSeasons_GroupId_OriginBasketId_S~",
                        columns: x => new { x.GroupId, x.OriginBasketId, x.SeasonIndex, x.OriginTeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueParticipations",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceLeagueId = table.Column<Guid>(type: "uuid", nullable: true),
                    QualificationRank = table.Column<int>(type: "integer", nullable: true),
                    AdmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AdmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueParticipations", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId });
                    table.CheckConstraint("CK_LeagueParticipations_Rank", "\"QualificationRank\" IS NULL OR \"QualificationRank\" BETWEEN 1 AND 64");
                    table.CheckConstraint("CK_LeagueParticipations_Source", "(\"Source\" = 2) = (\"SourceLeagueId\" IS NOT NULL) AND (\"QualificationRank\" IS NULL OR \"Source\" = 2)");
                    table.ForeignKey(
                        name: "FK_LeagueParticipations_FantasyTeamSeasons_GroupId_BasketId_Se~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeagueParticipations_GroupMemberships_GroupId_AdmittedByUse~",
                        columns: x => new { x.GroupId, x.AdmittedByUserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeagueParticipations_LeagueBasketSeasons_GroupId_LeagueId_B~",
                        columns: x => new { x.GroupId, x.LeagueId, x.BasketId, x.SeasonIndex },
                        principalTable: "LeagueBasketSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueParticipations_LeagueDerivations_GroupId_LeagueId_Sea~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SourceLeagueId },
                        principalTable: "LeagueDerivations",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "SourceLeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueLineups",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    FormationChanges = table.Column<int>(type: "integer", nullable: false),
                    LastUpdateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueLineups", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId, x.Day });
                    table.CheckConstraint("CK_LeagueLineups_Day", "\"Day\" BETWEEN 1 AND 38");
                    table.ForeignKey(
                        name: "FK_LeagueLineups_LeagueParticipations_GroupId_LeagueId_SeasonI~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId },
                        principalTable: "LeagueParticipations",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeagueLineups_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueParticipantOwners",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueParticipantOwners", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.UserId });
                    table.ForeignKey(
                        name: "FK_LeagueParticipantOwners_GroupMemberships_GroupId_UserId",
                        columns: x => new { x.GroupId, x.UserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeagueParticipantOwners_LeagueParticipations_GroupId_League~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId },
                        principalTable: "LeagueParticipations",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueLineupChanges",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    DateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueLineupChanges", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId, x.Day, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_LeagueLineupChanges_LeagueLineups_GroupId_LeagueId_SeasonIn~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId, x.Day },
                        principalTable: "LeagueLineups",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId", "Day" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueLineupPlayers",
                columns: table => new
                {
                    PlayerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueLineupPlayers", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId, x.Day, x.PlayerKey });
                    table.CheckConstraint("CK_LeagueLineupPlayers_Position", "\"Position\" BETWEEN 0 AND 11");
                    table.ForeignKey(
                        name: "FK_LeagueLineupPlayers_LeagueLineups_GroupId_LeagueId_SeasonIn~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId, x.Day },
                        principalTable: "LeagueLineups",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId", "Day" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueLineupPlayers_Players_PlayerKey",
                        column: x => x.PlayerKey,
                        principalTable: "Players",
                        principalColumn: "PlayerKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueLineupChangePlayers",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    ChangeOrdinal = table.Column<int>(type: "integer", nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromPosition = table.Column<int>(type: "integer", nullable: false),
                    ToPosition = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueLineupChangePlayers", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId, x.Day, x.ChangeOrdinal, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_LeagueLineupChangePlayers_LeagueLineupChanges_GroupId_Leagu~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId, x.Day, x.ChangeOrdinal },
                        principalTable: "LeagueLineupChanges",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId", "Day", "Ordinal" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Leagues_Mode",
                table: "Leagues",
                sql: "\"Mode\" IN (0,1)");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyMatches_GroupId_LeagueId_SeasonIndex_AwayTeamId",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "AwayTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyMatches_GroupId_LeagueId_SeasonIndex_HomeTeamId",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "HomeTeamId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Baskets_Kind",
                table: "Baskets",
                sql: "\"Kind\" IN (0,1)");

            migrationBuilder.CreateIndex(
                name: "IX_BasketDerivations_GroupId_BasketId_DerivedKind",
                table: "BasketDerivations",
                columns: new[] { "GroupId", "BasketId", "DerivedKind" });

            migrationBuilder.CreateIndex(
                name: "IX_BasketDerivations_GroupId_OriginBasketId",
                table: "BasketDerivations",
                columns: new[] { "GroupId", "OriginBasketId" });

            migrationBuilder.CreateIndex(
                name: "IX_BasketDerivations_GroupId_OriginBasketId_OriginKind",
                table: "BasketDerivations",
                columns: new[] { "GroupId", "OriginBasketId", "OriginKind" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueDerivations_GroupId_SourceLeagueId_SeasonIndex",
                table: "LeagueDerivations",
                columns: new[] { "GroupId", "SourceLeagueId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueLineupPlayers_PlayerKey",
                table: "LeagueLineupPlayers",
                column: "PlayerKey");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueLineups_UpdatedByUserId",
                table: "LeagueLineups",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipantOwners_GroupId_LeagueId_SeasonIndex_TeamId",
                table: "LeagueParticipantOwners",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipantOwners_GroupId_UserId",
                table: "LeagueParticipantOwners",
                columns: new[] { "GroupId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipations_GroupId_AdmittedByUserId",
                table: "LeagueParticipations",
                columns: new[] { "GroupId", "AdmittedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipations_GroupId_BasketId_SeasonIndex_TeamId",
                table: "LeagueParticipations",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipations_GroupId_LeagueId_BasketId_SeasonIndex",
                table: "LeagueParticipations",
                columns: new[] { "GroupId", "LeagueId", "BasketId", "SeasonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipations_GroupId_LeagueId_SeasonIndex_SourceLea~",
                table: "LeagueParticipations",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "SourceLeagueId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipations_GroupId_TeamId",
                table: "LeagueParticipations",
                columns: new[] { "GroupId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamDerivations_GroupId_BasketId_OriginBasketId",
                table: "TeamDerivations",
                columns: new[] { "GroupId", "BasketId", "OriginBasketId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamDerivations_GroupId_BasketId_SeasonIndex_OriginTeamId",
                table: "TeamDerivations",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "OriginTeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamDerivations_GroupId_OriginBasketId_SeasonIndex_OriginTe~",
                table: "TeamDerivations",
                columns: new[] { "GroupId", "OriginBasketId", "SeasonIndex", "OriginTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamDerivations_GroupId_OriginTeamId",
                table: "TeamDerivations",
                columns: new[] { "GroupId", "OriginTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamDerivations_GroupId_TeamId",
                table: "TeamDerivations",
                columns: new[] { "GroupId", "TeamId" },
                unique: true);

            // Legacy semantics, made explicit: every team of a basket linked to a league
            // season took part in it. Archived teams are kept for saved history.
            migrationBuilder.Sql("""
                INSERT INTO "LeagueParticipations" ("GroupId", "LeagueId", "SeasonIndex", "TeamId", "BasketId", "Source", "AdmittedAtUtc")
                SELECT l."GroupId", l."LeagueId", l."SeasonIndex", t."Id", t."BasketId", 3, now()
                FROM "LeagueBasketSeasons" l
                JOIN "FantasyTeamSeasons" t ON t."GroupId" = l."GroupId" AND t."BasketId" = l."BasketId" AND t."SeasonIndex" = l."SeasonIndex";
                """);
            migrationBuilder.Sql(IntegritySql);
            // Fail loudly, with the coordinates to reconcile, instead of silently
            // dropping a participation that violates the one-team-per-league rule.
            migrationBuilder.Sql("""
                DO $$
                DECLARE c record;
                BEGIN
                    SELECT p."GroupId", p."LeagueId", p."SeasonIndex", o."UserId", count(DISTINCT p."TeamId") AS teams INTO c
                    FROM "LeagueParticipations" p
                    JOIN "TeamOwnerships" o ON o."GroupId" = p."GroupId" AND o."TeamId" = p."TeamId"
                    GROUP BY p."GroupId", p."LeagueId", p."SeasonIndex", o."UserId"
                    HAVING count(DISTINCT p."TeamId") > 1
                    LIMIT 1;
                    IF FOUND THEN
                        RAISE EXCEPTION 'league_owner_conflict: user % controls % teams in league % season % of group %; reconcile ownerships before applying this migration.',
                            c."UserId", c.teams, c."LeagueId", c."SeasonIndex", c."GroupId";
                    END IF;
                END $$;
                INSERT INTO "LeagueParticipantOwners" ("GroupId", "LeagueId", "SeasonIndex", "UserId", "TeamId")
                SELECT DISTINCT p."GroupId", p."LeagueId", p."SeasonIndex", o."UserId", p."TeamId"
                FROM "LeagueParticipations" p
                JOIN "TeamOwnerships" o ON o."GroupId" = p."GroupId" AND o."TeamId" = p."TeamId";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_FantasyMatches_LeagueParticipations_GroupId_LeagueId_Season~",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "AwayTeamId" },
                principalTable: "LeagueParticipations",
                principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FantasyMatches_LeagueParticipations_GroupId_LeagueId_Seaso~1",
                table: "FantasyMatches",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "HomeTeamId" },
                principalTable: "LeagueParticipations",
                principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropIntegritySql);
            migrationBuilder.DropForeignKey(
                name: "FK_FantasyMatches_LeagueParticipations_GroupId_LeagueId_Season~",
                table: "FantasyMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_FantasyMatches_LeagueParticipations_GroupId_LeagueId_Seaso~1",
                table: "FantasyMatches");

            migrationBuilder.DropTable(
                name: "LeagueLineupChangePlayers");

            migrationBuilder.DropTable(
                name: "LeagueLineupPlayers");

            migrationBuilder.DropTable(
                name: "LeagueParticipantOwners");

            migrationBuilder.DropTable(
                name: "TeamDerivations");

            migrationBuilder.DropTable(
                name: "LeagueLineupChanges");

            migrationBuilder.DropTable(
                name: "BasketDerivations");

            migrationBuilder.DropTable(
                name: "LeagueLineups");

            migrationBuilder.DropTable(
                name: "LeagueParticipations");

            migrationBuilder.DropTable(
                name: "LeagueDerivations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PushFormationChangeReceipts",
                table: "PushFormationChangeReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Leagues_Mode",
                table: "Leagues");

            migrationBuilder.DropIndex(
                name: "IX_FantasyMatches_GroupId_LeagueId_SeasonIndex_AwayTeamId",
                table: "FantasyMatches");

            migrationBuilder.DropIndex(
                name: "IX_FantasyMatches_GroupId_LeagueId_SeasonIndex_HomeTeamId",
                table: "FantasyMatches");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Baskets_GroupId_BasketId_Kind",
                table: "Baskets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Baskets_Kind",
                table: "Baskets");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "PushFormationChangeReceipts");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "PushDayCheckpoints");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Baskets");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PushFormationChangeReceipts",
                table: "PushFormationChangeReceipts",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Day", "ChangeOrdinal" });
        }
    }
}
