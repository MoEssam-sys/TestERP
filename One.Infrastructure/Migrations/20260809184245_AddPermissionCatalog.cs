using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameResourceKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameResourceKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppModuleId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameResourceKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppPages_AppModules_AppModuleId",
                        column: x => x.AppModuleId,
                        principalTable: "AppModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppPageActions",
                columns: table => new
                {
                    AppPageId = table.Column<int>(type: "int", nullable: false),
                    PermissionActionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPageActions", x => new { x.AppPageId, x.PermissionActionId });
                    table.ForeignKey(
                        name: "FK_AppPageActions_AppPages_AppPageId",
                        column: x => x.AppPageId,
                        principalTable: "AppPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppPageActions_PermissionActions_PermissionActionId",
                        column: x => x.PermissionActionId,
                        principalTable: "PermissionActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppModules_IsActive_SortOrder",
                table: "AppModules",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AppModules_Key",
                table: "AppModules",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPageActions_PermissionActionId",
                table: "AppPageActions",
                column: "PermissionActionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPages_AppModuleId_IsActive_SortOrder",
                table: "AppPages",
                columns: new[] { "AppModuleId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPages_Key",
                table: "AppPages",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPages_Route",
                table: "AppPages",
                column: "Route",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionActions_IsActive_SortOrder",
                table: "PermissionActions",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionActions_Key",
                table: "PermissionActions",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPageActions");

            migrationBuilder.DropTable(
                name: "AppPages");

            migrationBuilder.DropTable(
                name: "PermissionActions");

            migrationBuilder.DropTable(
                name: "AppModules");
        }
    }
}
