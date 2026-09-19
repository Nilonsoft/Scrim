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
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var profileManager = ((App)Application.Current).Services.GetService<IProfileManager>();
        var profile = profileManager?.CurrentProfile;
        if (profile != null)
        {
            this.Title = $"Scrim Console - {profile.ProfileName} (:{profile.Port})";
            if (TrayIcon != null)
            {
                TrayIcon.ToolTipText = $"Scrim Console - {profile.ProfileName} (:{profile.Port})";
            }
        }

        try
        {
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(iconPath) && TrayIcon != null)
            {
                TrayIcon.Icon = new System.Drawing.Icon(iconPath);
            }
        }
        catch { }
    }

    private void BlazorWebView_Initialized(object? sender, Microsoft.AspNetCore.Components.WebView.BlazorWebViewInitializedEventArgs e)
    {
        e.WebView.CoreWebView2.NewWindowRequested += (s, args) =>
        {
            args.Handled = true;
            if (!string.IsNullOrEmpty(args.Uri))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = args.Uri,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        };
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExplicitClose) return;

        var profileManager = ((App)Application.Current).Services.GetService<IProfileManager>();
        var profile = profileManager?.CurrentProfile;
        
        if (profile == null) return;

        var mode = profile.CloseMode;

        if (mode == CloseToTrayMode.Ask)
        {
            var prompt = new Scrim.UI.Windows.ClosePromptWindow();
            prompt.Owner = this;
            var result = prompt.ShowDialog();
            
            if (result == true)
            {
                mode = prompt.ChosenMode;
                if (prompt.RememberChoice)
                {
                    profile.CloseMode = mode;
                    profileManager!.SaveProfile(profile);
                }
            }
            else
            {
                e.Cancel = true;
                return;
            }
        }

        if (mode == CloseToTrayMode.MinimizeToTray)
        {
            e.Cancel = true;
            this.Hide();
        }
        else if (mode == CloseToTrayMode.Close)
        {
            _isExplicitClose = true;
            Application.Current.Shutdown();
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        // Minimize stays visible on the Windows taskbar; closing (X) closes to tray.
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