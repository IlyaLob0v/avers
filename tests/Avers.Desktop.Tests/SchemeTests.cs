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
            Assert.True(app.Vm.SchemeScreen.CanEditSchemes);

            app.Vm.SchemeScreen.PickSchemeFile = () => Task.FromResult<string?>(file);
            await app.Vm.SchemeScreen.AddSchemeCommand.ExecuteAsync(null);
            app.Pump();

            Assert.Equal(Path.GetFileNameWithoutExtension(file), app.Vm.SchemeScreen.SelectedScheme?.Name);
            Assert.Equal("PNG", app.Vm.SchemeScreen.SelectedScheme?.Format);
            Assert.NotNull(app.Vm.SchemeScreen.SchemeImage);
            Assert.Equal("", app.Vm.SchemeScreen.SchemeNote);
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
            var count = app.Vm.SchemeScreen.Schemes.Count;

            app.Vm.SchemeScreen.PickSchemeFile = () => Task.FromResult<string?>(file);
            await app.Vm.SchemeScreen.AddSchemeCommand.ExecuteAsync(null);

            Assert.Equal("Схема должна быть картинкой JPG или PNG.", app.Vm.Status);
            Assert.Equal(count, app.Vm.SchemeScreen.Schemes.Count);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [AvaloniaFact]
    public async Task Engineer_RenamesAndDeletesScheme()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Scheme);
        var count = app.Vm.SchemeScreen.Schemes.Count;
        var file = Path.Combine(Path.GetTempPath(), $"IMG-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(file, Png);
        try
        {
            app.Vm.SchemeScreen.PickSchemeFile = () => Task.FromResult<string?>(file);
            await app.Vm.SchemeScreen.AddSchemeCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(file);
        }

        app.Vm.SchemeScreen.SchemeName = "Стояки, подъезд 1";
        await app.Vm.SchemeScreen.RenameSchemeCommand.ExecuteAsync(null);
        Assert.Equal("Стояки, подъезд 1", app.Vm.SchemeScreen.SelectedScheme?.Name);

        await app.Vm.SchemeScreen.DeleteSchemeCommand.ExecuteAsync(null);
        Assert.Equal(count + 1, app.Vm.SchemeScreen.Schemes.Count);
        await app.Vm.SchemeScreen.DeleteSchemeCommand.ExecuteAsync(null);
        app.Pump();

        Assert.Equal("Схема «Стояки, подъезд 1» удалена.", app.Vm.Status);
        Assert.Equal(count, app.Vm.SchemeScreen.Schemes.Count);
    }

    [AvaloniaFact]
    public void Reader_SeesDemoSchemePicture()
    {
        using var app = new DesktopApp("novikov");

        app.Go(AppModule.Scheme);

        Assert.Equal("PNG", app.Vm.SchemeScreen.SelectedScheme?.Format);
        Assert.NotNull(app.Vm.SchemeScreen.SchemeImage);
        Assert.Equal("", app.Vm.SchemeScreen.SchemeNote);
    }

    [AvaloniaFact]
    public void Reader_CannotEditSchemes()
    {
        using var app = new DesktopApp("novikov");

        Assert.False(app.Vm.SchemeScreen.CanEditSchemes);
    }
}
