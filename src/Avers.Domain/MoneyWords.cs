namespace Avers.Domain;

/// <summary>Сумма цифрами и прописью для писем юристов: «590,00 руб. (пятьсот девяносто рублей 00 копеек)».</summary>
public static class MoneyWords
{
    private static readonly string[] Hundreds = ["", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот"];
    private static readonly string[] Tens = ["", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто"];
    private static readonly string[] Teens = ["десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"];
    private static readonly string[] Units = ["", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять"];

    public static string Rubles(decimal sum)
    {
        var rubles = (long)Math.Floor(sum);
        var kopecks = (int)Math.Round((sum - rubles) * 100);
        var words = rubles == 0 ? "ноль" : string.Join(" ", new[]
        {
            Group(rubles / 1_000_000 % 1000, false, "миллион", "миллиона", "миллионов"),
            Group(rubles / 1000 % 1000, true, "тысяча", "тысячи", "тысяч"),
            Group(rubles % 1000, false, "", "", "")
        }.Where(x => x != ""));
        return $"{rubles},{kopecks:00} руб. ({words} {Plural(rubles, "рубль", "рубля", "рублей")} {kopecks:00} {Plural(kopecks, "копейка", "копейки", "копеек")})";
    }

    private static string Group(long n, bool feminine, string one, string few, string many)
    {
        if (n == 0) return "";
        var parts = new List<string> { Hundreds[n / 100] };
        var rest = n % 100;
        if (rest is >= 10 and < 20)
            parts.Add(Teens[rest - 10]);
        else
        {
            parts.Add(Tens[rest / 10]);
            parts.Add(feminine && rest % 10 is 1 or 2 ? (rest % 10 == 1 ? "одна" : "две") : Units[rest % 10]);
        }
        parts.Add(Plural(n, one, few, many));
        return string.Join(" ", parts.Where(x => x != ""));
    }

    private static string Plural(long n, string one, string few, string many) =>
        (n % 100) is >= 11 and <= 14 ? many : (n % 10) switch { 1 => one, 2 or 3 or 4 => few, _ => many };
}
