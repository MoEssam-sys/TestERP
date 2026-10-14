using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionsAndEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppModules_IsActive_SortOrder",
                table: "AppModules");

            migrationBuilder.AddColumn<bool>(
                name: "IsAssignableToPlans",
                table: "AppModules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DurationInDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionPlans_Duration", "[DurationInDays] BETWEEN 1 AND 3650");
                    table.CheckConstraint("CK_SubscriptionPlans_Price", "[Price] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CompanySubscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencySnapshot = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DurationInDaysSnapshot = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySubscriptions", x => x.Id);
                    table.UniqueConstraint("AK_CompanySubscriptions_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_CompanySubscriptions_Dates", "[EndsAtUtc] > [StartsAtUtc]");
                    table.CheckConstraint("CK_CompanySubscriptions_Duration", "[DurationInDaysSnapshot] BETWEEN 1 AND 3650");
                    table.CheckConstraint("CK_CompanySubscriptions_Price", "[PriceSnapshot] >= 0");
                    table.ForeignKey(
                        name: "FK_CompanySubscriptions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanySubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanPermissions",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<int>(type: "int", nullable: false),
                    AppPageId = table.Column<int>(type: "int", nullable: false),
                    PermissionActionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanPermissions", x => new { x.SubscriptionPlanId, x.AppPageId, x.PermissionActionId });
                    table.ForeignKey(
                        name: "FK_PlanPermissions_AppPageActions_AppPageId_PermissionActionId",
                        columns: x => new { x.AppPageId, x.PermissionActionId },
                        principalTable: "AppPageActions",
                        principalColumns: new[] { "AppPageId", "PermissionActionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanPermissions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyEntitlements",
                columns: table => new
                {
                    CompanySubscriptionId = table.Column<long>(type: "bigint", nullable: false),
                    AppPageId = table.Column<int>(type: "int", nullable: false),
                    PermissionActionId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    IsCustomized = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyEntitlements", x => new { x.CompanySubscriptionId, x.AppPageId, x.PermissionActionId });
                    table.ForeignKey(
                        name: "FK_CompanyEntitlements_AppPageActions_AppPageId_PermissionActionId",
                        columns: x => new { x.AppPageId, x.PermissionActionId },
                        principalTable: "AppPageActions",
                        principalColumns: new[] { "AppPageId", "PermissionActionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyEntitlements_CompanySubscriptions_CompanyId_CompanySubscriptionId",
                        columns: x => new { x.CompanyId, x.CompanySubscriptionId },
                        principalTable: "CompanySubscriptions",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionChangeLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanySubscriptionId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionChangeLogs_CompanySubscriptions_CompanyId_CompanySubscriptionId",
                        columns: x => new { x.CompanyId, x.CompanySubscriptionId },
                        principalTable: "CompanySubscriptions",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppModules_IsActive_IsAssignableToPlans_SortOrder",
                table: "AppModules",
                columns: new[] { "IsActive", "IsAssignableToPlans", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEntitlements_AppPageId_PermissionActionId",
                table: "CompanyEntitlements",
                columns: new[] { "AppPageId", "PermissionActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEntitlements_CompanyId_AppPageId_PermissionActionId",
                table: "CompanyEntitlements",
                columns: new[] { "CompanyId", "AppPageId", "PermissionActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEntitlements_CompanyId_CompanySubscriptionId",
                table: "CompanyEntitlements",
                columns: new[] { "CompanyId", "CompanySubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_CompanyId",
                table: "CompanySubscriptions",
                column: "CompanyId",
                unique: true,
                filter: "[Status] IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_CompanyId_Status_EndsAtUtc",
                table: "CompanySubscriptions",
                columns: new[] { "CompanyId", "Status", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_SubscriptionPlanId",
                table: "CompanySubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanPermissions_AppPageId_PermissionActionId",
                table: "PlanPermissions",
                columns: new[] { "AppPageId", "PermissionActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionChangeLogs_CompanyId_CompanySubscriptionId_OccurredAtUtc",
                table: "SubscriptionChangeLogs",
                columns: new[] { "CompanyId", "CompanySubscriptionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Code",
                table: "SubscriptionPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_IsActive_Name",
                table: "SubscriptionPlans",
                columns: new[] { "IsActive", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyEntitlements");

            migrationBuilder.DropTable(
                name: "PlanPermissions");

            migrationBuilder.DropTable(
                name: "SubscriptionChangeLogs");

            migrationBuilder.DropTable(
                name: "CompanySubscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_AppModules_IsActive_IsAssignableToPlans_SortOrder",
                table: "AppModules");

            migrationBuilder.DropColumn(
                name: "IsAssignableToPlans",
                table: "AppModules");

            migrationBuilder.CreateIndex(
                name: "IX_AppModules_IsActive_SortOrder",
                table: "AppModules",
                columns: new[] { "IsActive", "SortOrder" });
        }
    }
}
