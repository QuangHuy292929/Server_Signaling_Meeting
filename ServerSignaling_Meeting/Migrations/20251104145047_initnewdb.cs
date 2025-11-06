using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServerSignaling_Meeting.Migrations
{
    /// <inheritdoc />
    public partial class initnewdb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("12701bc8-7687-4279-9b83-dd09c5da5a90"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("9314b2a0-a93d-40ad-8549-f30e1fd5837c"));

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AspNetUsers");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("7e5620c5-f91f-477c-8ee0-6ab246299dac"), null, "Admin", "ADMIN" },
                    { new Guid("89e9fc9b-27de-412d-bd65-d519790d2079"), null, "User", "USER" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("7e5620c5-f91f-477c-8ee0-6ab246299dac"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("89e9fc9b-27de-412d-bd65-d519790d2079"));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AspNetUsers",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("12701bc8-7687-4279-9b83-dd09c5da5a90"), null, "User", "USER" },
                    { new Guid("9314b2a0-a93d-40ad-8549-f30e1fd5837c"), null, "Admin", "ADMIN" }
                });
        }
    }
}
