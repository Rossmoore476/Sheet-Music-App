using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;
using CommunityToolkit.WinUI.Controls;

namespace Sheet_Music_App
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            // set initial theme selection in the ComboBox based on current requested theme
            // use FindName to locate the ComboBox in XAML so this code doesn't rely on a generated field
            var themeCombo = this.FindName("ThemeCombo") as ComboBox;
            if (themeCombo != null)
            {
                if (MainWindow.Current != null && MainWindow.Current.Content is FrameworkElement root)
                {
                    var theme = root.RequestedTheme;
                    switch (theme)
                    {
                        case ElementTheme.Light:
                            themeCombo.SelectedIndex = 0;
                            break;
                        case ElementTheme.Dark:
                            themeCombo.SelectedIndex = 1;
                            break;
                        default:
                            themeCombo.SelectedIndex = 2;
                            break;
                    }
                }
                else
                {
                    themeCombo.SelectedIndex = 2;
                }
            }

            var navCombo = this.FindName("NavStyleCombo") as ComboBox;
            if (navCombo != null) 
            {
                if (MainWindow.Current != null)
                {
                    var mode = MainWindow.Current.GetNavStyle();
                    navCombo.SelectedIndex = mode == NavigationViewPaneDisplayMode.Top ? 1 : 0;
                }
                else
                {
                    navCombo.SelectedIndex = 0; // default to Left
                }
            }
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainWindow.Current == null) return;

            var combo = sender as ComboBox;
            if (combo == null) return;

            if (combo.SelectedIndex == 0)
            {
                MainWindow.Current.ApplyTheme(ElementTheme.Light);
            }
            else if (combo.SelectedIndex == 1)
            {
                MainWindow.Current.ApplyTheme(ElementTheme.Dark);
            }
            else
            {
                MainWindow.Current.ApplyTheme(ElementTheme.Default);
            }
        }

        private void NavStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainWindow.Current == null) return;

            var combo = sender as ComboBox;
            if (combo == null) return;

            // PaneDisplayMode can be Left, Top, LeftMinimal, LeftCompact etc. We'll map "Left" and "Top".
            if (combo.SelectedItem is ComboBoxItem item && (item.Content as string) == "Top")
            {
                MainWindow.Current.SetNavStyle(NavigationViewPaneDisplayMode.Top);
            }
            else
            {
                MainWindow.Current.SetNavStyle(NavigationViewPaneDisplayMode.Left);
            }
        }


        private async void Link_Licence(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.gnu.org/licenses/agpl-3.0.html"));
        }

        private async void Link_GitHub(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/Rossmoore476/Sheet-Music-App"));
        }

        private async void Link_Feedback(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/Rossmoore476/Sheet-Music-App/issues/new"));
        }

    }
}
