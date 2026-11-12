namespace Avers.Server.Tests;

/// <summary>Тест только для PostgreSQL: без переменной AVERS_TEST_POSTGRES он показывается пропущенным, а не пройденным.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AVERS_TEST_POSTGRES") is null)
            Skip = "Нужен PostgreSQL: задайте AVERS_TEST_POSTGRES.";
    }
}
