using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnosticErrorContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [AppLogs]
                SET [UserName] = LEFT([UserName], 256),
                    [UserId] = LEFT([UserId], 450);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "AppLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AppLogs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "AppLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AppLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionName",
                table: "AppLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                table: "AppLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OccurredAtLocal",
                table: "AppLogs",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtUtc",
                table: "AppLogs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "AppLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestMethod",
                table: "AppLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFile",
                table: "AppLogs",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "AppLogs",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [AppLogs]
                SET [OccurredAtUtc] = [CreatedAtUtc],
                    [OccurredAtLocal] = TODATETIMEOFFSET([CreatedAtUtc], '+00:00'),
                    [ReferenceNumber] = CONCAT('LEGACY-', [Id])
                WHERE [OccurredAtUtc] = '0001-01-01T00:00:00.0000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppLogs_CompanyId_OccurredAtUtc",
                table: "AppLogs",
                columns: new[] { "CompanyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLogs_ReferenceNumber",
                table: "AppLogs",
                column: "ReferenceNumber",
                unique: true,
                filter: "[ReferenceNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppLogs_CompanyId_OccurredAtUtc",
                table: "AppLogs");

            migrationBuilder.DropIndex(
                name: "IX_AppLogs_ReferenceNumber",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "FunctionName",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "OccurredAtLocal",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "RequestMethod",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "SourceFile",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "AppLogs");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "AppLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AppLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
