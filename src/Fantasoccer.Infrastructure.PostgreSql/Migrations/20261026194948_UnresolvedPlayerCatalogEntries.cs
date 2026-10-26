using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class UnresolvedPlayerCatalogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnresolvedPlayerCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    DocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArchiveReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    JsonPath = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceOrdinal = table.Column<int>(type: "integer", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalPayloadJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnresolvedPlayerCatalogEntries", x => x.Id);
                    table.CheckConstraint("CK_UnresolvedPlayerCatalogEntries_Ordinal", "\"SourceOrdinal\" >= 0");
                    table.ForeignKey(
                        name: "FK_UnresolvedPlayerCatalogEntries_Seasons_SeasonIndex",
                        column: x => x.SeasonIndex,
                        principalTable: "Seasons",
                        principalColumn: "SeasonIndex",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnresolvedPlayerCatalogEntries_SeasonIndex",
                table: "UnresolvedPlayerCatalogEntries",
                column: "SeasonIndex");

            migrationBuilder.CreateIndex(
                name: "IX_UnresolvedPlayerCatalogEntries_SourceId_DocumentSha256_Sour~",
                table: "UnresolvedPlayerCatalogEntries",
                columns: new[] { "SourceId", "DocumentSha256", "SourceOrdinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "UnresolvedPlayerCatalogEntries") THEN
                        RAISE EXCEPTION 'Cannot downgrade while unresolved catalog source records exist. No source data was deleted.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "UnresolvedPlayerCatalogEntries");
        }
    }
}
