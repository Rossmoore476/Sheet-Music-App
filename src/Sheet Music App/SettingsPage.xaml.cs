using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;

namespace Sheet_Music_App
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();

            // set initial radio selection based on current requested theme
            if (MainWindow.Current != null && MainWindow.Current.Content is FrameworkElement root)
            {
                var theme = root.RequestedTheme;
                switch (theme)
                {
                    case ElementTheme.Light:
                        LightRadio.IsChecked = true;
                        break;
                    case ElementTheme.Dark:
                        DarkRadio.IsChecked = true;
                        break;
                    default:
                        SystemRadio.IsChecked = true;
                        break;
                }
            }
            else
            {
                SystemRadio.IsChecked = true;
            }
        }


        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current == null) return;

            if (LightRadio.IsChecked == true)
            {
                MainWindow.Current.ApplyTheme(ElementTheme.Light);
            }
            else if (DarkRadio.IsChecked == true)
            {
                MainWindow.Current.ApplyTheme(ElementTheme.Dark);
            }
            else
            {
                MainWindow.Current.ApplyTheme(ElementTheme.Default);
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
