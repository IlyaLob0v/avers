using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Avers.Desktop.Tests;

public class BindingTests
{
    /// <summary>
    /// Run.Text по умолчанию привязан в обе стороны: подпись с форматом «· {0}» переписывает значение обратно
    /// в данные, и окно уходит в бесконечную перепись (переполнение стека).
    /// </summary>
    [Fact]
    public void RunBindings_AreOneWay()
    {
        var views = Path.GetFullPath(Path.Combine(Folder(), "..", "..", "src", "Avers.Desktop"));
        var twoWay = Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"<Run\b[^>]*\{Binding[^}]*\}[^>]*>")
                .Where(m => !m.Value.Contains("Mode=OneWay"))
                .Select(m => $"{Path.GetFileName(file)}: {m.Value}"))
            .ToList();

        Assert.Empty(twoWay);
    }

    private static string Folder([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
