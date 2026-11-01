using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CoachVoteArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FootballCoachVotes",
                columns: table => new
                {
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    ClubCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CoachKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<float>(type: "real", nullable: false),
                    HasVote = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderVoteJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballCoachVotes", x => new { x.SeasonIndex, x.Day, x.ClubCode, x.CoachKey });
                    table.CheckConstraint("CK_FootballCoachVotes_Day", "\"Day\" BETWEEN 1 AND 38");
                    table.CheckConstraint("CK_FootballCoachVotes_Role", "\"Role\" = 4");
                    table.ForeignKey(
                        name: "FK_FootballCoachVotes_Clubs_ClubCode",
                        column: x => x.ClubCode,
                        principalTable: "Clubs",
                        principalColumn: "ClubCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FootballCoachVotes_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FootballCoachVotes_ClubCode",
                table: "FootballCoachVotes",
                column: "ClubCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FootballCoachVotes");
        }
    }
}
