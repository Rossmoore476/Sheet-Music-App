using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Sheet_Music_App.Storage;
using Sheet_Music_App.Models;

namespace Sheet_Music_App
{
    public sealed partial class ProjectFullscreenPage : Page
    {
        private LocalFolderStorage? _storage;
        private Guid? _projectId;

        public ProjectFullscreenPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string idStr && Guid.TryParse(idStr, out var id))
            {
                _projectId = id;
                _ = LoadProjectAsync(id);
            }
        }

        private async Task LoadProjectAsync(Guid id)
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                var provider = (localSettings.Values["StorageProvider"] as string) ?? "Local";
                var root = localSettings.Values["LocalStoragePath"] as string;
                if (provider == "Local" && !string.IsNullOrEmpty(root))
                {
                    _storage = new LocalFolderStorage(root);
                }
                else
                {
                    _storage = new LocalFolderStorage();
                }

                var proj = await _storage.LoadProjectAsync(id);
                if (proj != null)
                {
                    FullscreenTitle.Text = proj.Name;
                }
                else
                {
                    FullscreenTitle.Text = "Project not found";
                }
            }
            catch
            {
                FullscreenTitle.Text = "Error loading project";
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.CloseProjectFullscreen();
        }
    }
}
