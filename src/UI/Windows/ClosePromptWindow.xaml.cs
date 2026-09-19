using System.Windows;
using Scrim.Configuration;

namespace Scrim.UI.Windows
{
    public partial class ClosePromptWindow : Window
    {
        public bool RememberChoice => RememberCheckBox.IsChecked == true;
        public CloseToTrayMode ChosenMode { get; private set; } = CloseToTrayMode.Ask;

        public ClosePromptWindow()
        {
            InitializeComponent();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            ChosenMode = CloseToTrayMode.MinimizeToTray;
            this.DialogResult = true;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ChosenMode = CloseToTrayMode.Close;
            this.DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
