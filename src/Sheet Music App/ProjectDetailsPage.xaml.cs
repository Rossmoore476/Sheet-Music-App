using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using Sheet_Music_App.Storage;
using System.Threading.Tasks;
using Sheet_Music_App.Models;

namespace Sheet_Music_App
{
    public sealed partial class ProjectDetailsPage : Page
    {
        private LocalFolderStorage? _storage;
        private Guid? _projectId;

        public ProjectDetailsPage()
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
                    TitleText.Text = proj.Name;
                    DescriptionText.Text = proj.Description;
                }
                else
                {
                    TitleText.Text = "Project not found";
                    DescriptionText.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                TitleText.Text = "Error loading project";
                DescriptionText.Text = ex.Message;
            }
        }
    }
}
