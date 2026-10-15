using Avers.Domain;

namespace Avers.Domain.Tests;

public class NaturalOrderTests
{
    [Fact]
    public void HouseNumbers_SortLikePeople()
    {
        string[] numbers = ["10", "2", "19Д", "9", "19", "12/1", "19А", "", "корп"];

        var sorted = numbers.Order(NaturalOrder.Instance).ToArray();

        Assert.Equal(["2", "9", "10", "12/1", "19", "19А", "19Д", "", "корп"], sorted);
    }
}
