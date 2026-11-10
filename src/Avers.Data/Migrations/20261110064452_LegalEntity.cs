using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class LegalEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Director",
                table: "Subscribers",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Inn",
                table: "Subscribers",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsLegalEntity",
                table: "Subscribers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kpp",
                table: "Subscribers",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalAddress",
                table: "Subscribers",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ogrn",
                table: "Subscribers",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            // Льгота «юридическое лицо» стала типом клиента.
            migrationBuilder.Sql("""
                update "Subscribers" set "IsLegalEntity" = true, "Benefit" = 'не применяется'
                 where "Benefit" = 'юридическое лицо';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                update "Subscribers" set "Benefit" = 'юридическое лицо' where "IsLegalEntity";
                """);

            migrationBuilder.DropColumn(
                name: "Director",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "Inn",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "IsLegalEntity",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "Kpp",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "LegalAddress",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "Ogrn",
                table: "Subscribers");
        }
    }
}
