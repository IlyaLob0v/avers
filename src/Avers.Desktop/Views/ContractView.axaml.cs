using Avalonia.Controls;
using Avers.Desktop.ViewModels;

namespace Avers.Desktop.Views;

public partial class ContractView : UserControl
{
    public ContractView()
    {
        InitializeComponent();
        Registry.DoubleTapped += (_, _) =>
        {
            if (DataContext is ContractsViewModel vm && vm.SelectedContractRow is { } row)
                vm.OpenCardCommand.Execute(row);
        };
    }
}
