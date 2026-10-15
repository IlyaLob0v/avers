namespace Avers.Desktop.Tests;

public class DemoDatabaseTests
{
    [Fact]
    public void SecondInstanceWaitsForDatabaseLock()
    {
        var file = Path.Combine(Path.GetTempPath(), $"avers-lock-test-{Guid.NewGuid():N}.db");
        try
        {
            using (DemoDatabase.Lock(file))
            {
                Assert.Throws<IOException>(() => DemoDatabase.Lock(file, TimeSpan.FromMilliseconds(300)));
            }

            using (DemoDatabase.Lock(file, TimeSpan.Zero)) { }
        }
        finally
        {
            File.Delete(file + ".lock");
        }
    }
}
