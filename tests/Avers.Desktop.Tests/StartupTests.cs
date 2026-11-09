using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Desktop.Data;
using Avers.Desktop.Views;

namespace Avers.Desktop.Tests;

/// <summary>С чем открывается программа: сервер из avers.json, учебная база только по --demo, иначе — сообщение.</summary>
public sealed class StartupTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("avers-startup-").FullName;

    private void WriteConfig(string text) => File.WriteAllText(Path.Combine(_directory, ServerAddress.FileName), text);

    [Fact]
    public void NoConfig_IsProblem_NotDemo()
    {
        var mode = ServerAddress.Choose([], _directory);

        Assert.False(mode.Demo);
        Assert.Null(mode.Server);
        Assert.Contains("Не задан адрес сервера", mode.Problem);
    }

    [Fact]
    public void BrokenConfig_IsProblem_NotDemo()
    {
        WriteConfig("{ server: ");

        var mode = ServerAddress.Choose([], _directory);

        Assert.False(mode.Demo);
        Assert.Contains(ServerAddress.FileName, mode.Problem);
    }

    [Fact]
    public void ConfigWithoutServer_IsProblem()
    {
        WriteConfig("{ }");

        Assert.Contains("Не задан адрес сервера", ServerAddress.Choose([], _directory).Problem);
    }

    [Fact]
    public void Config_GivesServer()
    {
        WriteConfig("""{ "server": "https://192.168.1.10" }""");

        var mode = ServerAddress.Choose([], _directory);

        Assert.Equal(new Uri("https://192.168.1.10/"), mode.Server);
        Assert.False(mode.Demo);
        Assert.Null(mode.Problem);
    }

    [Fact]
    public void DemoKey_OpensDemo_EvenWithoutConfig()
    {
        var mode = ServerAddress.Choose(["--demo"], _directory);

        Assert.True(mode.Demo);
        Assert.Null(mode.Problem);
    }

    [Theory]
    [InlineData("http://192.168.1.10:5115", true)]
    [InlineData("http://avers-srv:5115", true)]
    [InlineData("https://192.168.1.10", false)]
    [InlineData("http://localhost:5115", false)]
    [InlineData("http://127.0.0.1:5115", false)]
    public void PlainHttp_OutsideThisComputer_IsInsecure(string address, bool insecure) =>
        Assert.Equal(insecure, ServerAddress.IsInsecure(new Uri(address)));

    [AvaloniaFact]
    public void NoConfig_ShowsMessageWindow_InsteadOfDemoBase()
    {
        var window = App.CreateMainWindow([], _directory);
        window.Show();

        Assert.IsNotType<MainWindow>(window);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Не задан адрес сервера") == true);
        window.Close();
    }

    [AvaloniaFact]
    public void HttpServer_WarnsOnLoginScreen()
    {
        WriteConfig("""{ "server": "http://192.168.1.10:5115" }""");

        var window = App.CreateMainWindow([], _directory);

        var vm = Assert.IsType<ViewModels.MainViewModel>(window.DataContext);
        Assert.Contains("Связь с сервером не защищена", vm.ConnectionWarning);
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
