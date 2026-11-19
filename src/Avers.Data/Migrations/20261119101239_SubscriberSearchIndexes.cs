using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubscriberSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
                CREATE FUNCTION avers_phone_digits(value text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE AS $fn$
                    SELECT pg_catalog.regexp_replace(coalesce(value, ''), '[^0-9]', '', 'g')
                $fn$;
                CREATE INDEX "IX_Subscribers_PhoneDigits" ON "Subscribers" USING gin (avers_phone_digits("Phone") gin_trgm_ops);
                CREATE INDEX "IX_Subscribers_LastName_Lower" ON "Subscribers" (lower("LastName") text_pattern_ops);
                CREATE INDEX "IX_Streets_Name_Lower" ON "Streets" (lower("Name") text_pattern_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX "IX_Subscribers_PhoneDigits";
                DROP INDEX "IX_Subscribers_LastName_Lower";
                DROP INDEX "IX_Streets_Name_Lower";
                DROP FUNCTION avers_phone_digits(text);
                """);
        }
    }
}
