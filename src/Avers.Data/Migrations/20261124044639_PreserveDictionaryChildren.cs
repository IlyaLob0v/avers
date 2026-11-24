using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreserveDictionaryChildren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Houses_Streets_StreetId",
                table: "Houses");

            migrationBuilder.DropForeignKey(
                name: "FK_HouseSchemes_Houses_HouseId",
                table: "HouseSchemes");

            migrationBuilder.DropForeignKey(
                name: "FK_IntercomPanels_Houses_HouseId",
                table: "IntercomPanels");

            migrationBuilder.DropForeignKey(
                name: "FK_IntercomTickets_Houses_HouseId",
                table: "IntercomTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_PprEntries_Houses_HouseId",
                table: "PprEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscribers_Houses_HouseId",
                table: "Subscribers");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Houses_HouseId",
                table: "Tickets");

            migrationBuilder.AddForeignKey(
                name: "FK_Houses_Streets_StreetId",
                table: "Houses",
                column: "StreetId",
                principalTable: "Streets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_HouseSchemes_Houses_HouseId",
                table: "HouseSchemes",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_IntercomPanels_Houses_HouseId",
                table: "IntercomPanels",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_IntercomTickets_Houses_HouseId",
                table: "IntercomTickets",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PprEntries_Houses_HouseId",
                table: "PprEntries",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscribers_Houses_HouseId",
                table: "Subscribers",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Houses_HouseId",
                table: "Tickets",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Houses_Streets_StreetId",
                table: "Houses");

            migrationBuilder.DropForeignKey(
                name: "FK_HouseSchemes_Houses_HouseId",
                table: "HouseSchemes");

            migrationBuilder.DropForeignKey(
                name: "FK_IntercomPanels_Houses_HouseId",
                table: "IntercomPanels");

            migrationBuilder.DropForeignKey(
                name: "FK_IntercomTickets_Houses_HouseId",
                table: "IntercomTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_PprEntries_Houses_HouseId",
                table: "PprEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscribers_Houses_HouseId",
                table: "Subscribers");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Houses_HouseId",
                table: "Tickets");

            migrationBuilder.AddForeignKey(
                name: "FK_Houses_Streets_StreetId",
                table: "Houses",
                column: "StreetId",
                principalTable: "Streets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HouseSchemes_Houses_HouseId",
                table: "HouseSchemes",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IntercomPanels_Houses_HouseId",
                table: "IntercomPanels",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IntercomTickets_Houses_HouseId",
                table: "IntercomTickets",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PprEntries_Houses_HouseId",
                table: "PprEntries",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscribers_Houses_HouseId",
                table: "Subscribers",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Houses_HouseId",
                table: "Tickets",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
