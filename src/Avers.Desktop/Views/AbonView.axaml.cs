using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Avers.Desktop.Views;

public partial class AbonView : UserControl
{
    public AbonView()
    {
        InitializeComponent();

        // Выбранная улица и дом внизу экрана не прокручивают весь экран к себе:
        // оператор смотрит на абонента и платежи, список улиц прокручивается сам.
        StreetsAndHouses.AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true, RoutingStrategies.Bubble);
    }
}
