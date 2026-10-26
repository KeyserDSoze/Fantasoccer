using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AccountScopedTeamAdvice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "TeamAdvice" a
                        WHERE a."GeneratedByUserId" IS NULL OR NOT EXISTS (
                            SELECT 1 FROM "GroupMemberships" m
                            WHERE m."GroupId" = a."GroupId" AND m."UserId" = a."GeneratedByUserId"
                        )
                    ) THEN
                        RAISE EXCEPTION 'Preliminary advice rows lack a verifiable private recipient. Reimport/reconcile explicitly; do not guess the current owner.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamAdvice",
                table: "TeamAdvice");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "TeamAdvice",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE \"TeamAdvice\" SET \"OwnerUserId\" = \"GeneratedByUserId\"");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamAdvice",
                table: "TeamAdvice",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Day", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamAdvice_GroupId_OwnerUserId",
                table: "TeamAdvice",
                columns: new[] { "GroupId", "OwnerUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TeamAdvice_GroupMemberships_GroupId_OwnerUserId",
                table: "TeamAdvice",
                columns: new[] { "GroupId", "OwnerUserId" },
                principalTable: "GroupMemberships",
                principalColumns: new[] { "GroupId", "UserId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TeamAdvice") THEN
                        RAISE EXCEPTION 'Cannot remove the private advice recipient from populated data. Reconcile explicitly before downgrade.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_TeamAdvice_GroupMemberships_GroupId_OwnerUserId",
                table: "TeamAdvice");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamAdvice",
                table: "TeamAdvice");

            migrationBuilder.DropIndex(
                name: "IX_TeamAdvice_GroupId_OwnerUserId",
                table: "TeamAdvice");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "TeamAdvice");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamAdvice",
                table: "TeamAdvice",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId", "Day" });
        }
    }
}
