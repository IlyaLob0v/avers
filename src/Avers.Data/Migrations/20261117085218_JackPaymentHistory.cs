using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class JackPaymentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jack_archive");

            migrationBuilder.AddColumn<bool>(
                name: "ImportedFromJack",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "payment_history",
                schema: "jack_archive",
                columns: table => new
                {
                    payid = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<int>(type: "integer", nullable: false),
                    period = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    pay_type_id = table.Column<int>(type: "integer", nullable: false),
                    connection_type_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    recorded_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_history", x => x.payid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_history_paid_at",
                schema: "jack_archive",
                table: "payment_history",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_history_subscriber_id_period",
                schema: "jack_archive",
                table: "payment_history",
                columns: new[] { "subscriber_id", "period" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_history",
                schema: "jack_archive");

            migrationBuilder.DropColumn(
                name: "ImportedFromJack",
                table: "Payments");
        }
    }
}
