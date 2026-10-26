using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalParticipantAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_EmailCanonical",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_EmailCanonical",
                table: "Users",
                sql: "\"Email\" IS NULL OR (\"Email\" = lower(btrim(\"Email\")) AND length(\"Email\") > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Users" WHERE "Email" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade while participants without login emails exist. No users were changed.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_EmailCanonical",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_EmailCanonical",
                table: "Users",
                sql: "\"Email\" = lower(btrim(\"Email\")) AND length(\"Email\") > 0");
        }
    }
}
