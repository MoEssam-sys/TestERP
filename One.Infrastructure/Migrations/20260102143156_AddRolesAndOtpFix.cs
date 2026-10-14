using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndOtpFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "11111111-1111-1111-1111-111111111111",
                column: "ConcurrencyStamp",
                value: "d83bf49f-e859-47b1-924d-3436e76090c3");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "22222222-2222-2222-2222-222222222222",
                column: "ConcurrencyStamp",
                value: "028a9b0b-7709-4de4-8474-5acb39b246bc");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "33333333-3333-3333-3333-333333333333",
                column: "ConcurrencyStamp",
                value: "fe17ebb9-8b6d-4a9e-9c74-6631ec04022b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "44444444-4444-4444-4444-444444444444",
                column: "ConcurrencyStamp",
                value: "ccf21f85-6966-4c85-bc33-64a70f03dcad");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "99999999-9999-9999-9999-999999999999",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "008266b2-599f-437f-a834-f195838d48ff", "125841d5-91fc-4508-921f-7ad746b4789a" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "11111111-1111-1111-1111-111111111111",
                column: "ConcurrencyStamp",
                value: "d862dff6-f2b0-47dd-b719-4209b64755fd");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "22222222-2222-2222-2222-222222222222",
                column: "ConcurrencyStamp",
                value: "96bd2adc-9ca2-4d56-9e7d-09dde6aa0d39");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "33333333-3333-3333-3333-333333333333",
                column: "ConcurrencyStamp",
                value: "fb9b9596-5e03-4840-8586-4eeb586f7310");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "44444444-4444-4444-4444-444444444444",
                column: "ConcurrencyStamp",
                value: "b8830df0-0b41-407e-ab91-a4e99a866acc");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "99999999-9999-9999-9999-999999999999",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "0fb792a9-5741-4fe5-8837-ae07a808d4ef", "66510681-24f7-45eb-9b69-42291bca66c8" });
        }
    }
}
