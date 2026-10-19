using Avalonia.Headless.XUnit;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class SchemeTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    [AvaloniaFact]
    public async Task Engineer_AddsScheme_AndSeesImage()
    {
        using var app = new DesktopApp("engineer");
        var file = Path.Combine(Path.GetTempPath(), $"Стояки-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(file, Png);
        try
        {
            app.Go(AppModule.Scheme);
            Assert.Null(app.Vm.SchemeImage);
            Assert.True(app.Vm.CanEditSchemes);

            app.Vm.PickSchemeFile = () => Task.FromResult<string?>(file);
            await app.Vm.AddSchemeCommand.ExecuteAsync(null);
            app.Pump();

            Assert.Equal(Path.GetFileNameWithoutExtension(file), app.Vm.SelectedScheme?.Name);
            Assert.Equal("PNG", app.Vm.SelectedScheme?.Format);
            Assert.NotNull(app.Vm.SchemeImage);
            Assert.Equal("", app.Vm.SchemeNote);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [AvaloniaFact]
    public async Task NotAnImage_IsRejected()
    {
        using var app = new DesktopApp("engineer");
        var file = Path.Combine(Path.GetTempPath(), $"ведомость-{Guid.NewGuid():N}.xlsx");
        await File.WriteAllBytesAsync(file, "PK\u0003\u0004"u8.ToArray());
        try
        {
            app.Go(AppModule.Scheme);
            var count = app.Vm.Schemes.Count;

            app.Vm.PickSchemeFile = () => Task.FromResult<string?>(file);
            await app.Vm.AddSchemeCommand.ExecuteAsync(null);

            Assert.Equal("Схема должна быть картинкой JPG или PNG.", app.Vm.Status);
            Assert.Equal(count, app.Vm.Schemes.Count);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [AvaloniaFact]
    public void Reader_CannotEditSchemes()
    {
        using var app = new DesktopApp("novikov");

        Assert.False(app.Vm.CanEditSchemes);
    }
}
