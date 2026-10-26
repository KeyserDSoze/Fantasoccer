using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class LeaguePlayerOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaguePlayerOrders",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaguePlayerOrders", x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
                    table.ForeignKey(
                        name: "FK_LeaguePlayerOrders_LeagueSeasons_GroupId_LeagueId_SeasonInd~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeagueSeasons",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaguePlayerOrderEntries",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerRole = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaguePlayerOrderEntries", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_LeaguePlayerOrderEntries_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaguePlayerOrderEntries_LeaguePlayerOrders_GroupId_LeagueI~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex },
                        principalTable: "LeaguePlayerOrders",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaguePlayerOrderEntries_PlayerSeasons_PlayerId_SeasonIndex",
                        columns: x => new { x.PlayerId, x.SeasonIndex },
                        principalTable: "PlayerSeasons",
                        principalColumns: new[] { "PlayerId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaguePlayerOrderEntries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaguePlayerOrderEntries_ClubId",
                table: "LeaguePlayerOrderEntries",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaguePlayerOrderEntries_GroupId_LeagueId_SeasonIndex_Playe~",
                table: "LeaguePlayerOrderEntries",
                columns: new[] { "GroupId", "LeagueId", "SeasonIndex", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaguePlayerOrderEntries_PlayerId_SeasonIndex",
                table: "LeaguePlayerOrderEntries",
                columns: new[] { "PlayerId", "SeasonIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaguePlayerOrderEntries");

            migrationBuilder.DropTable(
                name: "LeaguePlayerOrders");
        }
    }
}
