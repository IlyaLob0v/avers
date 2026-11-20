using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentYearTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentYearTotals",
                columns: table => new
                {
                    SubscriberId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Charged = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unpaid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentYearTotals", x => new { x.SubscriberId, x.Year });
                    table.ForeignKey(
                        name: "FK_PaymentYearTotals_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.Sql("""
                LOCK TABLE "Payments" IN SHARE ROW EXCLUSIVE MODE;
                INSERT INTO "PaymentYearTotals" ("SubscriberId", "Year", "Charged", "Paid", "Unpaid")
                SELECT "SubscriberId", "Year", sum("Tariff"), sum(coalesce("Amount", 0)),
                       count(*) FILTER (WHERE "Amount" IS NULL)::integer
                  FROM "Payments" GROUP BY "SubscriberId", "Year";

                CREATE FUNCTION avers_payment_years_insert() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    INSERT INTO "PaymentYearTotals" AS totals ("SubscriberId", "Year", "Charged", "Paid", "Unpaid")
                    SELECT "SubscriberId", "Year", sum("Tariff"), sum(coalesce("Amount", 0)),
                           count(*) FILTER (WHERE "Amount" IS NULL)::integer
                      FROM avers_new_payments GROUP BY "SubscriberId", "Year" ORDER BY "SubscriberId", "Year"
                    ON CONFLICT ("SubscriberId", "Year") DO UPDATE SET
                        "Charged" = totals."Charged" + EXCLUDED."Charged",
                        "Paid" = totals."Paid" + EXCLUDED."Paid",
                        "Unpaid" = totals."Unpaid" + EXCLUDED."Unpaid";
                    RETURN NULL;
                END $fn$;

                CREATE FUNCTION avers_payment_years_update() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    INSERT INTO "PaymentYearTotals" AS totals ("SubscriberId", "Year", "Charged", "Paid", "Unpaid")
                    SELECT "SubscriberId", "Year", sum(charged), sum(paid), sum(unpaid)::integer
                      FROM (
                        SELECT "SubscriberId", "Year", "Tariff" AS charged, coalesce("Amount", 0) AS paid,
                               CASE WHEN "Amount" IS NULL THEN 1 ELSE 0 END AS unpaid FROM avers_new_payments
                        UNION ALL
                        SELECT "SubscriberId", "Year", -"Tariff", -coalesce("Amount", 0),
                               CASE WHEN "Amount" IS NULL THEN -1 ELSE 0 END FROM avers_old_payments
                      ) changes GROUP BY "SubscriberId", "Year" ORDER BY "SubscriberId", "Year"
                    ON CONFLICT ("SubscriberId", "Year") DO UPDATE SET
                        "Charged" = totals."Charged" + EXCLUDED."Charged",
                        "Paid" = totals."Paid" + EXCLUDED."Paid",
                        "Unpaid" = totals."Unpaid" + EXCLUDED."Unpaid";
                    RETURN NULL;
                END $fn$;

                CREATE FUNCTION avers_payment_years_delete() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    UPDATE "PaymentYearTotals" AS totals SET
                        "Charged" = totals."Charged" - removed.charged,
                        "Paid" = totals."Paid" - removed.paid,
                        "Unpaid" = totals."Unpaid" - removed.unpaid
                    FROM (
                        SELECT "SubscriberId", "Year", sum("Tariff") AS charged, sum(coalesce("Amount", 0)) AS paid,
                               count(*) FILTER (WHERE "Amount" IS NULL)::integer AS unpaid
                        FROM avers_old_payments GROUP BY "SubscriberId", "Year"
                    ) removed
                    WHERE totals."SubscriberId" = removed."SubscriberId" AND totals."Year" = removed."Year";
                    RETURN NULL;
                END $fn$;

                CREATE FUNCTION avers_payment_years_truncate() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    DELETE FROM "PaymentYearTotals";
                    RETURN NULL;
                END $fn$;

                CREATE TRIGGER avers_payment_years_insert AFTER INSERT ON "Payments"
                    REFERENCING NEW TABLE AS avers_new_payments FOR EACH STATEMENT EXECUTE FUNCTION avers_payment_years_insert();
                CREATE TRIGGER avers_payment_years_update AFTER UPDATE ON "Payments"
                    REFERENCING OLD TABLE AS avers_old_payments NEW TABLE AS avers_new_payments
                    FOR EACH STATEMENT EXECUTE FUNCTION avers_payment_years_update();
                CREATE TRIGGER avers_payment_years_delete AFTER DELETE ON "Payments"
                    REFERENCING OLD TABLE AS avers_old_payments FOR EACH STATEMENT EXECUTE FUNCTION avers_payment_years_delete();
                CREATE TRIGGER avers_payment_years_truncate AFTER TRUNCATE ON "Payments"
                    FOR EACH STATEMENT EXECUTE FUNCTION avers_payment_years_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER avers_payment_years_insert ON "Payments";
                DROP TRIGGER avers_payment_years_update ON "Payments";
                DROP TRIGGER avers_payment_years_delete ON "Payments";
                DROP TRIGGER avers_payment_years_truncate ON "Payments";
                DROP FUNCTION avers_payment_years_insert();
                DROP FUNCTION avers_payment_years_update();
                DROP FUNCTION avers_payment_years_delete();
                DROP FUNCTION avers_payment_years_truncate();
                """);
            migrationBuilder.DropTable(
                name: "PaymentYearTotals");
        }
    }
}
