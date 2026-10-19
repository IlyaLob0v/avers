using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchemeFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseSchemeFiles",
                columns: table => new
                {
                    HouseSchemeId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseSchemeFiles", x => x.HouseSchemeId);
                    table.ForeignKey(
                        name: "FK_HouseSchemeFiles_HouseSchemes_HouseSchemeId",
                        column: x => x.HouseSchemeId,
                        principalTable: "HouseSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseSchemeFiles");
        }
    }
}
