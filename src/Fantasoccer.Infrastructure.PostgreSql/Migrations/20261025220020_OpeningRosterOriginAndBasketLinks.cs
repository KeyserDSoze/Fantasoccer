using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OpeningRosterOriginAndBasketLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RosterSnapshots" WHERE "Kind" IN (2,3)) THEN
                        RAISE EXCEPTION 'Opening snapshots from the preliminary schema require explicit reconciliation of their previous-season team identity. No data was automatically remapped.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_RosterSnapshots_FantasyTeamSeasons_GroupId_BasketId_SeasonI~",
                table: "RosterSnapshots");

            migrationBuilder.AddColumn<int>(
                name: "TeamOriginYear",
                table: "RosterSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE \"RosterSnapshots\" SET \"TeamOriginYear\" = \"SeasonIndex\"");

            migrationBuilder.CreateTable(
                name: "GroupLeagueBaskets",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupLeagueBaskets", x => new { x.GroupId, x.LeagueId, x.BasketId });
                    table.ForeignKey(
                        name: "FK_GroupLeagueBaskets_Baskets_GroupId_BasketId",
                        columns: x => new { x.GroupId, x.BasketId },
                        principalTable: "Baskets",
                        principalColumns: new[] { "GroupId", "BasketId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupLeagueBaskets_Leagues_GroupId_LeagueId",
                        columns: x => new { x.GroupId, x.LeagueId },
                        principalTable: "Leagues",
                        principalColumns: new[] { "GroupId", "LeagueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "GroupLeagueBaskets" ("GroupId", "LeagueId", "BasketId")
                SELECT DISTINCT "GroupId", "LeagueId", "BasketId" FROM "LeagueBasketSeasons";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RosterSnapshots_GroupId_BasketId_TeamOriginYear_TeamId",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "BasketId", "TeamOriginYear", "TeamId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RosterSnapshots_OriginYear",
                table: "RosterSnapshots",
                sql: "(\"Kind\" IN (0,1) AND \"TeamOriginYear\" = \"SeasonIndex\") OR (\"Kind\" IN (2,3) AND \"TeamOriginYear\" = \"SeasonIndex\" - 1)");

            migrationBuilder.CreateIndex(
                name: "IX_GroupLeagueBaskets_GroupId_BasketId",
                table: "GroupLeagueBaskets",
                columns: new[] { "GroupId", "BasketId" });

            migrationBuilder.AddForeignKey(
                name: "FK_RosterSnapshots_FantasyTeamSeasons_GroupId_BasketId_TeamOri~",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "BasketId", "TeamOriginYear", "TeamId" },
                principalTable: "FantasyTeamSeasons",
                principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RosterSnapshots" WHERE "Kind" IN (2,3)) THEN
                        RAISE EXCEPTION 'Cannot downgrade opening snapshots to the preliminary same-season identity model without an explicit reverse migration.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_RosterSnapshots_FantasyTeamSeasons_GroupId_BasketId_TeamOri~",
                table: "RosterSnapshots");

            migrationBuilder.DropTable(
                name: "GroupLeagueBaskets");

            migrationBuilder.DropIndex(
                name: "IX_RosterSnapshots_GroupId_BasketId_TeamOriginYear_TeamId",
                table: "RosterSnapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RosterSnapshots_OriginYear",
                table: "RosterSnapshots");

            migrationBuilder.DropColumn(
                name: "TeamOriginYear",
                table: "RosterSnapshots");

            migrationBuilder.AddForeignKey(
                name: "FK_RosterSnapshots_FantasyTeamSeasons_GroupId_BasketId_SeasonI~",
                table: "RosterSnapshots",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" },
                principalTable: "FantasyTeamSeasons",
                principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
