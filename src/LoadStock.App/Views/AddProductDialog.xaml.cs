using System.Windows;
using LoadStock.App.ViewModels;

namespace LoadStock.App.Views;

public partial class AddProductDialog : Window
{
    private readonly AddProductViewModel _vm;

    public AddProductDialog(AddProductViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveCommand.Execute(null);
        if (_vm.Saved)
            Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
