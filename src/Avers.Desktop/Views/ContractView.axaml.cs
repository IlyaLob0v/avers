using Avalonia.Controls;
using Avers.Desktop.ViewModels;

namespace Avers.Desktop.Views;

public partial class ContractView : UserControl
{
    public ContractView()
    {
        InitializeComponent();
        // Жёлтая строка — давно не было оплаты; строки переиспользуются, поэтому класс ставим каждый раз.
        Registry.LoadingRow += (_, e) => e.Row.Classes.Set("late", e.Row.DataContext is ContractRow { IsLate: true });
        Registry.DoubleTapped += (_, _) =>
        {
            if (DataContext is MainViewModel vm && vm.SelectedContractRow is { } row)
                vm.OpenCardCommand.Execute(row);
        };
    }
}
