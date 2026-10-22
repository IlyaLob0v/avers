using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class OekAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OekAccount",
                table: "Subscribers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_OekAccount",
                table: "Subscribers",
                column: "OekAccount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscribers_OekAccount",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "OekAccount",
                table: "Subscribers");
        }
    }
}
