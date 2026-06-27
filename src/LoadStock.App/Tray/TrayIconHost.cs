using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using LoadStock.App.Views;

namespace LoadStock.App.Tray;

/// <summary>
/// Sistem tepsisi ikonu ve menüsünü yönetir. Pencere kapatıldığında uygulama
/// tepside çalışmaya devam eder; gerçek çıkış yalnızca tepsi menüsünden yapılır.
/// </summary>
public sealed class TrayIconHost : IDisposable
{
    private readonly MainWindow _mainWindow;
    private TaskbarIcon? _icon;

    public TrayIconHost(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void Initialize()
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "LoadStock",
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/tray.ico", UriKind.Absolute)),
        };

        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Aç" };
        open.Click += (_, _) => ShowMainWindow();

        var quit = new MenuItem { Header = "Çıkış" };
        quit.Click += (_, _) =>
        {
            _mainWindow.AllowClose();
            System.Windows.Application.Current.Shutdown();
        };

        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);
        _icon.ContextMenu = menu;

        _icon.TrayLeftMouseDown += (_, _) => ShowMainWindow();
        _icon.ForceCreate();
    }

    public void ShowMainWindow()
    {
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
