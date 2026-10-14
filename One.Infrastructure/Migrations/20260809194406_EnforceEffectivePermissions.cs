using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceEffectivePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNavigationVisible",
                table: "AppPages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CompanyPermissionVersions",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPermissionVersions", x => x.CompanyId);
                    table.CheckConstraint("CK_CompanyPermissionVersions_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_CompanyPermissionVersions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AppPageId = table.Column<int>(type: "int", nullable: false),
                    PermissionActionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrantedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_AppPageActions_AppPageId_PermissionActionId",
                        columns: x => new { x.AppPageId, x.PermissionActionId },
                        principalTable: "AppPageActions",
                        principalColumns: new[] { "AppPageId", "PermissionActionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPermissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_AppPageId_PermissionActionId",
                table: "UserPermissions",
                columns: new[] { "AppPageId", "PermissionActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_CompanyId_UserId_AppPageId_PermissionActionId",
                table: "UserPermissions",
                columns: new[] { "CompanyId", "UserId", "AppPageId", "PermissionActionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_CompanyId",
                table: "UserPermissions",
                columns: new[] { "UserId", "CompanyId" });

            migrationBuilder.Sql(
                """
                INSERT INTO [CompanyPermissionVersions] ([CompanyId], [Version], [UpdatedAtUtc])
                SELECT [Id], 1, SYSUTCDATETIME()
                FROM [Companies] AS [company]
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [CompanyPermissionVersions] AS [version]
                    WHERE [version].[CompanyId] = [company].[Id]
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyPermissionVersions");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "IsNavigationVisible",
                table: "AppPages");
        }
    }
}
