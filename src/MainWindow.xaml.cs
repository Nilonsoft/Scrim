using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Scrim.Configuration;

namespace Scrim;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _isExplicitClose = false;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExplicitClose) return;

        var profileManager = ((App)Application.Current).Services.GetService<IProfileManager>();
        bool closeToTray = profileManager?.CurrentProfile?.CloseToTray ?? true;

        if (closeToTray)
        {
            e.Cancel = true;
            this.Hide();
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (this.WindowState == WindowState.Minimized)
        {
            this.Hide();
        }
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        RestoreWindow();
    }

    private void ShowApp_Click(object sender, RoutedEventArgs e)
    {
        RestoreWindow();
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        _isExplicitClose = true;
        Application.Current.Shutdown();
    }

    private void RestoreWindow()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }
}