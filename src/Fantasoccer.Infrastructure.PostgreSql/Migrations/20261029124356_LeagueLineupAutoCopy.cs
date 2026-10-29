using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class LeagueLineupAutoCopy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueLineupAutoCopies",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnabledByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueLineupAutoCopies", x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId });
                    table.ForeignKey(
                        name: "FK_LeagueLineupAutoCopies_GroupMemberships_GroupId_EnabledByUs~",
                        columns: x => new { x.GroupId, x.EnabledByUserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeagueLineupAutoCopies_LeagueParticipations_GroupId_LeagueI~",
                        columns: x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId },
                        principalTable: "LeagueParticipations",
                        principalColumns: new[] { "GroupId", "LeagueId", "SeasonIndex", "TeamId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueLineupAutoCopies_GroupId_EnabledByUserId",
                table: "LeagueLineupAutoCopies",
                columns: new[] { "GroupId", "EnabledByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueLineupAutoCopies");
        }
    }
}
