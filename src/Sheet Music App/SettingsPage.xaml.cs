using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Sheet_Music_App
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();

            // set initial selection based on current requested theme
            if (MainWindow.Current != null && MainWindow.Current.Content is FrameworkElement root)
            {
                var theme = root.RequestedTheme;
                switch (theme)
                {
                    case ElementTheme.Light:
                        ThemeComboBox.SelectedIndex = 1; // Light
                        break;
                    case ElementTheme.Dark:
                        ThemeComboBox.SelectedIndex = 2; // Dark
                        break;
                    default:
                        ThemeComboBox.SelectedIndex = 0; // System
                        break;
                }
            }
            else
            {
                ThemeComboBox.SelectedIndex = 0;
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item && MainWindow.Current != null)
            {
                var tag = item.Tag as string ?? "Default";
                if (tag == "Light")
                {
                    MainWindow.Current.ApplyTheme(ElementTheme.Light);
                }
                else if (tag == "Dark")
                {
                    MainWindow.Current.ApplyTheme(ElementTheme.Dark);
                }
                else
                {
                    MainWindow.Current.ApplyTheme(ElementTheme.Default);
                }
            }
        }
    }
}
