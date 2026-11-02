using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PlayerTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MissingSinceUtc",
                table: "PlayerSeasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousClubCode",
                table: "PlayerSeasons",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TransferredAtUtc",
                table: "PlayerSeasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasons_PreviousClubCode",
                table: "PlayerSeasons",
                column: "PreviousClubCode");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerSeasons_Clubs_PreviousClubCode",
                table: "PlayerSeasons",
                column: "PreviousClubCode",
                principalTable: "Clubs",
                principalColumn: "ClubCode",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerSeasons_Clubs_PreviousClubCode",
                table: "PlayerSeasons");

            migrationBuilder.DropIndex(
                name: "IX_PlayerSeasons_PreviousClubCode",
                table: "PlayerSeasons");

            migrationBuilder.DropColumn(
                name: "MissingSinceUtc",
                table: "PlayerSeasons");

            migrationBuilder.DropColumn(
                name: "PreviousClubCode",
                table: "PlayerSeasons");

            migrationBuilder.DropColumn(
                name: "TransferredAtUtc",
                table: "PlayerSeasons");
        }
    }
}
