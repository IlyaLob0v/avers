using Avalonia.Headless.XUnit;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class LegalCalculatorTests
{
    [AvaloniaFact]
    public void WithoutEventDate_AsksForDate_ThenChangesResultForChosenStage()
    {
        using var app = new DesktopApp("lawyer");
        app.Go(AppModule.Legal);
        var calculator = app.Vm.LegalScreen;
        Assert.Contains("дат", calculator.LegalRemaining);

        calculator.LegalStage = calculator.LegalStages.Single(s => s.Title == "Возражения на судебный приказ");
        Assert.Contains("дат", calculator.LegalRemaining);
        calculator.LegalStart = new DateTime(2026, 8, 3);
        var shortDeadline = calculator.LegalRemaining;
        calculator.LegalStage = calculator.LegalStages.Single(s => s.Title == "Ответ на претензию");
        Assert.NotEqual(shortDeadline, calculator.LegalRemaining);
        Assert.Contains("02.09.2026", calculator.LegalRemaining);
    }
}
