using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class CourtCaseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "CourtCases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Receipts",
                table: "CourtCases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "StateDuty",
                table: "CourtCases",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WritSentDate",
                table: "CourtCases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WritSentTo",
                table: "CourtCases",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "CourtCases");

            migrationBuilder.DropColumn(
                name: "Receipts",
                table: "CourtCases");

            migrationBuilder.DropColumn(
                name: "StateDuty",
                table: "CourtCases");

            migrationBuilder.DropColumn(
                name: "WritSentDate",
                table: "CourtCases");

            migrationBuilder.DropColumn(
                name: "WritSentTo",
                table: "CourtCases");
        }
    }
}
