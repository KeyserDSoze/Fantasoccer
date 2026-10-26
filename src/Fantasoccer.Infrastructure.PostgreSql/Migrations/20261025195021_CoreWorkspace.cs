using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fantasoccer.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CoreWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoreImportCheckpoints",
                columns: table => new
                {
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    Container = table.Column<string>(type: "text", nullable: false),
                    BlobName = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MappingVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreImportCheckpoints", x => new { x.SourceId, x.Container, x.BlobName });
                });

            migrationBuilder.CreateTable(
                name: "CoreMigrationState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<string>(type: "text", nullable: true),
                    InteractiveWritesStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoreMigrationState", x => x.Id);
                    table.CheckConstraint("CK_CoreMigrationState_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "FantasyTeamSeasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyTeamSeasons", x => x.Id);
                    table.UniqueConstraint("AK_FantasyTeamSeasons_GroupId_BasketId_SeasonIndex_Id", x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id });
                    table.ForeignKey(
                        name: "FK_FantasyTeamSeasons_BasketSeasons_GroupId_BasketId_SeasonInd~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex },
                        principalTable: "BasketSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacyIdentityMaps",
                columns: table => new
                {
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    LegacyKey = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyIdentityMaps", x => new { x.SourceId, x.Kind, x.LegacyKey });
                });

            migrationBuilder.CreateTable(
                name: "TeamOwnerships",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonIndex = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamOwnerships", x => new { x.GroupId, x.TeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TeamOwnerships_FantasyTeamSeasons_GroupId_BasketId_SeasonIn~",
                        columns: x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId },
                        principalTable: "FantasyTeamSeasons",
                        principalColumns: new[] { "GroupId", "BasketId", "SeasonIndex", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamOwnerships_GroupMemberships_GroupId_UserId",
                        columns: x => new { x.GroupId, x.UserId },
                        principalTable: "GroupMemberships",
                        principalColumns: new[] { "GroupId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FantasyTeamSeasons_GroupId_BasketId_SeasonIndex_NormalizedN~",
                table: "FantasyTeamSeasons",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyIdentityMaps_SourceId_Kind_TargetId",
                table: "LegacyIdentityMaps",
                columns: new[] { "SourceId", "Kind", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamOwnerships_GroupId_BasketId_SeasonIndex_TeamId",
                table: "TeamOwnerships",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamOwnerships_GroupId_BasketId_SeasonIndex_UserId",
                table: "TeamOwnerships",
                columns: new[] { "GroupId", "BasketId", "SeasonIndex", "UserId" },
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.CreateIndex(
                name: "IX_TeamOwnerships_GroupId_TeamId",
                table: "TeamOwnerships",
                columns: new[] { "GroupId", "TeamId" },
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.CreateIndex(
                name: "IX_TeamOwnerships_GroupId_UserId",
                table: "TeamOwnerships",
                columns: new[] { "GroupId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoreImportCheckpoints");

            migrationBuilder.DropTable(
                name: "CoreMigrationState");

            migrationBuilder.DropTable(
                name: "LegacyIdentityMaps");

            migrationBuilder.DropTable(
                name: "TeamOwnerships");

            migrationBuilder.DropTable(
                name: "FantasyTeamSeasons");
        }
    }
}
