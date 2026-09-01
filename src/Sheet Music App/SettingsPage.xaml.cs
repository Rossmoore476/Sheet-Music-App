using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;
using WinRT.Interop;
using CommunityToolkit.WinUI.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.IO;

namespace Sheet_Music_App
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            InitializeStorageSettings();
            InitializeThemeAndNav();
        }

        private void InitializeThemeAndNav()
        {
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

        private void InitializeStorageSettings()
        {
            // detect OneDrive path via environment variable
            var oneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
            bool hasOneDrive = !string.IsNullOrEmpty(oneDrivePath) && Directory.Exists(oneDrivePath);

            var storageCombo = this.FindName("StorageProviderCombo") as ComboBox;
            var localPanel = this.FindName("LocalFolderPanel") as StackPanel;
            var localPathBox = this.FindName("LocalFolderPathTextBox") as TextBox;

            if (storageCombo != null)
            {
                // Load saved preference
                var localSettings = ApplicationData.Current.LocalSettings;
                var savedProvider = localSettings.Values["StorageProvider"] as string;
                var savedPath = localSettings.Values["LocalStoragePath"] as string;

                if (string.IsNullOrEmpty(savedProvider))
                {
                    // default to OneDrive if available, otherwise Local
                    savedProvider = hasOneDrive ? "OneDrive" : "Local";
                }

                if (savedProvider == "OneDrive")
                {
                    storageCombo.SelectedIndex = 0;
                    if (localPanel != null) localPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    storageCombo.SelectedIndex = 1;
                    if (localPanel != null) localPanel.Visibility = Visibility.Visible;
                }

                // Set path textbox
                if (!string.IsNullOrEmpty(savedPath))
                {
                    if (localPathBox != null) localPathBox.Text = savedPath;
                }
                else
                {
                    // default local path to %USERPROFILE%\Documents\Sheet Music App (avoid OneDrive redirected Documents)
                    var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var docs = Path.Combine(userProfile, "Documents");
                    var defaultPath = Path.Combine(docs, "Sheet Music App");
                    if (localPathBox != null) localPathBox.Text = defaultPath;
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

        private void StorageProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            var localPanel = this.FindName("LocalFolderPanel") as StackPanel;
            var localPathBox = this.FindName("LocalFolderPathTextBox") as TextBox;
            if (combo == null) return;

            if (combo.SelectedIndex == 1)
            {
                // Local selected
                if (localPanel != null) localPanel.Visibility = Visibility.Visible;
                ApplicationData.Current.LocalSettings.Values["StorageProvider"] = "Local";

                // Ensure LocalStoragePath is set to the user's Documents/Sheet Music App when switching to Local
                var savedPath = ApplicationData.Current.LocalSettings.Values["LocalStoragePath"] as string;
                var oneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
                var docs = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
                var defaultPath = Path.Combine(docs, "Sheet Music App");

                if (string.IsNullOrEmpty(savedPath) ||
                    (!string.IsNullOrEmpty(oneDrivePath) && savedPath != null && savedPath.StartsWith(oneDrivePath, StringComparison.OrdinalIgnoreCase)))
                {
                    ApplicationData.Current.LocalSettings.Values["LocalStoragePath"] = defaultPath;
                    if (localPathBox != null) localPathBox.Text = defaultPath;
                }
            }
            else
            {
                if (localPanel != null) localPanel.Visibility = Visibility.Collapsed;
                ApplicationData.Current.LocalSettings.Values["StorageProvider"] = "OneDrive";
            }
        }

        private async void BrowseLocalFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            // Initialize FolderPicker with the app window handle for WinUI3 desktop apps
            // This prevents COMException "Invalid window handle" when showing the picker.
            if (App.AppWindow != null)
            {
                var hwnd = WindowNative.GetWindowHandle(App.AppWindow);
                InitializeWithWindow.Initialize(folderPicker, hwnd);
            }

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                var localPathBox = this.FindName("LocalFolderPathTextBox") as TextBox;
                if (localPathBox != null) localPathBox.Text = folder.Path;
                ApplicationData.Current.LocalSettings.Values["LocalStoragePath"] = folder.Path;
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
