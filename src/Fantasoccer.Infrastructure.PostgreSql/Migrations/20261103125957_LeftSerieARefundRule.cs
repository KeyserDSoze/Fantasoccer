using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class LeftSerieARefundRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeftSerieARefundPercent",
                table: "LeagueRules",
                type: "integer",
                nullable: false,
                // Existing leagues get the default rule: half the price back.
                defaultValue: 50);

            migrationBuilder.AddColumn<bool>(
                name: "LeftSerieARefundRoundUp",
                table: "LeagueRules",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeftSerieARefundPercent",
                table: "LeagueRules");

            migrationBuilder.DropColumn(
                name: "LeftSerieARefundRoundUp",
                table: "LeagueRules");
        }
    }
}
