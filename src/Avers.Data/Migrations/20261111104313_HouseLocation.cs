using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class HouseLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Houses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Houses",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Houses");
        }
    }
}
