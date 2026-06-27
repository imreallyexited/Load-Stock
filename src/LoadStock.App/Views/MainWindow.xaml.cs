using System.ComponentModel;
using System.Windows;
using LoadStock.App.ViewModels;

namespace LoadStock.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _reallyClosing;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    /// <summary>Gerçek çıkış için kapatmaya izin ver (tepsi menüsündeki "Çıkış").</summary>
    public void AllowClose() => _reallyClosing = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyClosing)
        {
            // Kapatma butonu pencereyi tepsiye gizler, uygulamayı kapatmaz.
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    private void AddProduct_Click(object sender, RoutedEventArgs e)
    {
        var dialogVm = new AddProductViewModel(_vm.Store, _vm.Resolver);
        var dlg = new AddProductDialog(dialogVm) { Owner = this };
        dlg.ShowDialog();
        if (dialogVm.Saved)
            _vm.Load();
    }
}
