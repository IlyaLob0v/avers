using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Avers.Desktop.Tests;

public class AddressSuggestionTests
{
    [AvaloniaFact]
    public void TypingStreet_ShowsMatchingSuggestions_WithoutChoosingASubscriber()
    {
        using var app = new DesktopApp("orlov", selectRecord: false);
        var street = app.Window.GetVisualDescendants().OfType<AutoCompleteBox>()
            .SingleOrDefault(box => box.IsEffectivelyVisible && AutomationProperties.GetName(box) == "Улица для поиска абонента");
        Assert.NotNull(street);

        street.Focus();
        app.Window.KeyTextInput("Воло");
        app.Pump();

        Assert.Equal("Воло", app.Vm.FindStreet);
        Assert.True(street.IsDropDownOpen);
        Assert.Contains("Волочаевская", street.ItemsSource!.Cast<string>());
        Assert.Null(app.Vm.SelectedSubscriber);

        app.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        app.Window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        app.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        app.Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        app.Pump();
        Assert.Equal("Волочаевская", app.Vm.FindStreet);
        Assert.False(street.IsDropDownOpen);
        Assert.Null(app.Vm.SelectedSubscriber);
    }
}
