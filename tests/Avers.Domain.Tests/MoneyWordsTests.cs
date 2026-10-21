using Avers.Domain;

namespace Avers.Domain.Tests;

public class MoneyWordsTests
{
    [Theory]
    [InlineData("590", "590,00 руб. (пятьсот девяносто рублей 00 копеек)")]
    [InlineData("750", "750,00 руб. (семьсот пятьдесят рублей 00 копеек)")]
    [InlineData("1800", "1800,00 руб. (одна тысяча восемьсот рублей 00 копеек)")]
    [InlineData("2044.32", "2044,32 руб. (две тысячи сорок четыре рубля 32 копейки)")]
    [InlineData("21.01", "21,01 руб. (двадцать один рубль 01 копейка)")]
    [InlineData("0", "0,00 руб. (ноль рублей 00 копеек)")]
    [InlineData("1115000", "1115000,00 руб. (один миллион сто пятнадцать тысяч рублей 00 копеек)")]
    public void Rubles_WritesSumInFiguresAndWords(string sum, string text) =>
        Assert.Equal(text, MoneyWords.Rubles(decimal.Parse(sum, System.Globalization.CultureInfo.InvariantCulture)));
}
