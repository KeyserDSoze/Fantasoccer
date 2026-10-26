using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class GroupCreationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupCreationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RedeemedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupCreationCodes", x => x.Id);
                    table.CheckConstraint("CK_GroupCreationCodes_Redemption", "(\"RedeemedAtUtc\" IS NULL AND \"RedeemedByUserId\" IS NULL AND \"RedeemedGroupId\" IS NULL) OR\n(\"RedeemedAtUtc\" IS NOT NULL AND \"RedeemedByUserId\" IS NOT NULL AND \"RedeemedGroupId\" IS NOT NULL AND \"RevokedAtUtc\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_GroupCreationCodes_Groups_RedeemedGroupId",
                        column: x => x.RedeemedGroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupCreationCodes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupCreationCodes_Users_RedeemedByUserId",
                        column: x => x.RedeemedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupCreationCodes_CodeHash",
                table: "GroupCreationCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupCreationCodes_CreatedAtUtc",
                table: "GroupCreationCodes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCreationCodes_CreatedByUserId",
                table: "GroupCreationCodes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCreationCodes_RedeemedByUserId",
                table: "GroupCreationCodes",
                column: "RedeemedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCreationCodes_RedeemedGroupId",
                table: "GroupCreationCodes",
                column: "RedeemedGroupId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "GroupCreationCodes") THEN
                        RAISE EXCEPTION 'Cannot erase group activation codes and their audit on downgrade. Reconcile explicitly first.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "GroupCreationCodes");
        }
    }
}
