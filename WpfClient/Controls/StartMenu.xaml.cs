using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfClient.Controls;

public partial class StartMenu : UserControl
{
    public event EventHandler? StartRequested;

    public StartMenu()
    {
        InitializeComponent();
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        StartRequested?.Invoke(this, EventArgs.Empty);
    }
}
