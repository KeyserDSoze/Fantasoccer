using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RelationalCardRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardLibraries",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardLibraries", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "RuleCards",
                columns: table => new
                {
                    Library = table.Column<string>(type: "text", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<float>(type: "real", nullable: false),
                    NegativePoints = table.Column<float>(type: "real", nullable: false),
                    EnemyPoints = table.Column<float>(type: "real", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ImageBlobName = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    MyCondition_HasPlayerCondition = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumNumberOfOccurrences = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumPlayer_IsPresent = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumPlayer_Vote = table.Column<float>(type: "real", nullable: true),
                    MyCondition_MinimumPlayer_HasYellow = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumPlayer_HasRed = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumPlayer_Goals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumPlayer_Assists = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumPlayer_OwnGoals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumPlayer_Penalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumPlayer_StoppedPenalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumPlayer_SufferedGoals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumPlayer_WrongedPenalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumPlayer_IsManOfTheMatch = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumPlayer_IsOut = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumPlayer_IsIn = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumPlayer_IsInjured = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MaximumPlayer_IsPresent = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MaximumPlayer_Vote = table.Column<float>(type: "real", nullable: true),
                    MyCondition_MaximumPlayer_HasYellow = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MaximumPlayer_HasRed = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MaximumPlayer_Goals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumPlayer_Assists = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumPlayer_OwnGoals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumPlayer_Penalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumPlayer_StoppedPenalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumPlayer_SufferedGoals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumPlayer_WrongedPenalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumPlayer_IsManOfTheMatch = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MaximumPlayer_IsOut = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MaximumPlayer_IsIn = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MaximumPlayer_IsInjured = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_HasTeamCondition = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumTeam_IsPresent = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MinimumTeam_WithVote = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_WithoutVote = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_WithSpecial = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_Goals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_Penalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_Assists = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_StoppedPenalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_SufferedGoals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_WrongedPenalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_OwnGoals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_YellowCards = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_RedCards = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_EnoughVotes = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_ManOfTheMatch = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MinimumTeam_Injured = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_IsPresent = table.Column<bool>(type: "boolean", nullable: true),
                    MyCondition_MaximumTeam_WithVote = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_WithoutVote = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_WithSpecial = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_Goals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_Penalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_Assists = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_StoppedPenalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_SufferedGoals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_WrongedPenalties = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_OwnGoals = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_YellowCards = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_RedCards = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_EnoughVotes = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_ManOfTheMatch = table.Column<int>(type: "integer", nullable: true),
                    MyCondition_MaximumTeam_Injured = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_HasPlayerCondition = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumNumberOfOccurrences = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumPlayer_IsPresent = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumPlayer_Vote = table.Column<float>(type: "real", nullable: true),
                    EnemyCondition_MinimumPlayer_HasYellow = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumPlayer_HasRed = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumPlayer_Goals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumPlayer_Assists = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumPlayer_OwnGoals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumPlayer_Penalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumPlayer_StoppedPenalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumPlayer_SufferedGoals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumPlayer_WrongedPenalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumPlayer_IsManOfTheMatch = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumPlayer_IsOut = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumPlayer_IsIn = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumPlayer_IsInjured = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MaximumPlayer_IsPresent = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MaximumPlayer_Vote = table.Column<float>(type: "real", nullable: true),
                    EnemyCondition_MaximumPlayer_HasYellow = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MaximumPlayer_HasRed = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MaximumPlayer_Goals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumPlayer_Assists = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumPlayer_OwnGoals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumPlayer_Penalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumPlayer_StoppedPenalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumPlayer_SufferedGoals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumPlayer_WrongedPenalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumPlayer_IsManOfTheMatch = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MaximumPlayer_IsOut = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MaximumPlayer_IsIn = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MaximumPlayer_IsInjured = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_HasTeamCondition = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumTeam_IsPresent = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MinimumTeam_WithVote = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_WithoutVote = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_WithSpecial = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_Goals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_Penalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_Assists = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_StoppedPenalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_SufferedGoals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_WrongedPenalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_OwnGoals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_YellowCards = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_RedCards = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_EnoughVotes = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_ManOfTheMatch = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MinimumTeam_Injured = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_IsPresent = table.Column<bool>(type: "boolean", nullable: true),
                    EnemyCondition_MaximumTeam_WithVote = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_WithoutVote = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_WithSpecial = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_Goals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_Penalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_Assists = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_StoppedPenalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_SufferedGoals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_WrongedPenalties = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_OwnGoals = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_YellowCards = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_RedCards = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_EnoughVotes = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_ManOfTheMatch = table.Column<int>(type: "integer", nullable: true),
                    EnemyCondition_MaximumTeam_Injured = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleCards", x => new { x.Library, x.Id });
                    table.ForeignKey(
                        name: "FK_RuleCards_CardLibraries_Library",
                        column: x => x.Library,
                        principalTable: "CardLibraries",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RuleCardEnemyRoles",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Library = table.Column<string>(type: "text", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleCardEnemyRoles", x => new { x.Library, x.CardId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_RuleCardEnemyRoles_RuleCards_Library_CardId",
                        columns: x => new { x.Library, x.CardId },
                        principalTable: "RuleCards",
                        principalColumns: new[] { "Library", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleCardMyRoles",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Library = table.Column<string>(type: "text", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleCardMyRoles", x => new { x.Library, x.CardId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_RuleCardMyRoles_RuleCards_Library_CardId",
                        columns: x => new { x.Library, x.CardId },
                        principalTable: "RuleCards",
                        principalColumns: new[] { "Library", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleCardEnemyRoles");

            migrationBuilder.DropTable(
                name: "RuleCardMyRoles");

            migrationBuilder.DropTable(
                name: "RuleCards");

            migrationBuilder.DropTable(
                name: "CardLibraries");
        }
    }
}
