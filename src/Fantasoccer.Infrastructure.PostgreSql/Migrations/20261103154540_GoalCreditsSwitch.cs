using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class GoalCreditsSwitch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GoalCreditsEnabled",
                table: "LeagueRules",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoalCreditsEnabled",
                table: "LeagueRules");
        }
    }
}
