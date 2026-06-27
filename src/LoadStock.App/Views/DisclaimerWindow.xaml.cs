using System.Windows;

namespace LoadStock.App.Views;

public partial class DisclaimerWindow : Window
{
    public DisclaimerWindow()
    {
        InitializeComponent();
    }

    private void Accept_Toggled(object sender, RoutedEventArgs e)
    {
        AcceptButton.IsEnabled = AcceptCheck.IsChecked == true;
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Decline_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
