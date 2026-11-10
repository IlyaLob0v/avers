namespace Avers.Domain.Tests;

public class LegalEntityTests
{
    [Fact]
    public void ClientKind_ByFlag()
    {
        Assert.Equal("Физлицо", new Subscriber().ClientKind);
        Assert.Equal("Юрлицо", new Subscriber { IsLegalEntity = true }.ClientKind);
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("5503254160", "550301001", "1145543048001")]
    [InlineData("550304544505", "", "314550300000012")]
    public void RequisitesError_AcceptsEmptyAndRightLengths(string inn, string kpp, string ogrn) =>
        Assert.Null(Subscriber.RequisitesError(inn, kpp, ogrn));

    [Theory]
    [InlineData("55032541", "", "", "ИНН")]
    [InlineData("55032541ab", "", "", "ИНН")]
    [InlineData("", "5503010", "", "КПП")]
    [InlineData("", "", "11455430480", "ОГРН")]
    public void RequisitesError_NamesWrongField(string inn, string kpp, string ogrn, string field) =>
        Assert.StartsWith(field, Subscriber.RequisitesError(inn, kpp, ogrn));
}
