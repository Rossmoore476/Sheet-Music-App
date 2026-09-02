using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using Sheet_Music_App.Storage;
using System.Threading.Tasks;
using System.Linq;
using Sheet_Music_App.Models;
using System;

namespace Sheet_Music_App
{
    public sealed partial class HomePage : Page
    {
        private ObservableCollection<ProjectViewModel> Projects { get; } = new ObservableCollection<ProjectViewModel>();
        private LocalFolderStorage _storage;

        public HomePage()
        {
            this.InitializeComponent();

            ProjectsItemsControl.ItemsSource = Projects;
            CreateNewProjectButton.Click += CreateNewProjectButton_Click;
            this.Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProjectsAsync();
        }

        private async Task LoadProjectsAsync()
        {
            // respect user's configured local storage path if set
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

            Projects.Clear();

            var summaries = (await _storage.ListProjectsAsync()).ToList();
            foreach (var s in summaries)
            {
                try
                {
                    var proj = await _storage.LoadProjectAsync(s.Id);
                    if (proj == null) continue;

                    var vm = new ProjectViewModel
                    {
                        Id = proj.Id,
                        Name = proj.Name,
                        Description = proj.Description
                    };

                    int idx = 1;
                    foreach (var p in proj.Pieces)
                    {
                        vm.Pieces.Add(new ProjectPieceViewModel { Index = idx++, Title = p.Title, Composer = p.Composer });
                    }

                    vm.NoPiecesNote = vm.Pieces.Count == 0 ? "No pieces added." : string.Empty;

                    Projects.Add(vm);
                }
                catch { /* ignore individual project load failures */ }
            }
        }

        private void CreateNewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to NewProjectPage using this page's Frame
            this.Frame?.Navigate(typeof(NewProjectPage));
        }

        private void EditProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.CommandParameter is Guid id)
                {
                    this.Frame?.Navigate(typeof(ProjectDetailsPage), id.ToString());
                }
                else if (btn.CommandParameter is string idStr && Guid.TryParse(idStr, out var gid))
                {
                    this.Frame?.Navigate(typeof(ProjectDetailsPage), idStr);
                }
            }
        }

        private async void DeleteProjectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.CommandParameter is Guid projectId)
            {
                var projVm = Projects.FirstOrDefault(p => p.Id == projectId);
                if (projVm == null) return;

                var dlg = new ContentDialog
                {
                    Title = "Delete project?",
                    Content = "Are you sure you want to delete the project '" + projVm.Name + "'? This cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel"
                };
                if (this.XamlRoot != null) dlg.XamlRoot = this.XamlRoot;

                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    try
                    {
                        await _storage.DeleteProjectAsync(projectId);
                        Projects.Remove(projVm);

                        // Refresh the navigation in the main window so the deleted project is removed
                        if (MainWindow.Current != null)
                        {
                            await MainWindow.Current.PopulateProjectNavItemsAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        var err = new ContentDialog
                        {
                            Title = "Delete failed",
                            Content = "Failed to delete project: " + ex.Message,
                            CloseButtonText = "OK"
                        };
                        if (this.XamlRoot != null) err.XamlRoot = this.XamlRoot;
                        await err.ShowAsync();
                    }
                }
            }
        }
    }

    public class ProjectViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ObservableCollection<ProjectPieceViewModel> Pieces { get; } = new ObservableCollection<ProjectPieceViewModel>();
        public string NoPiecesNote { get; set; } = string.Empty;
    }

    public class ProjectPieceViewModel
    {
        public int Index { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Composer { get; set; } = string.Empty;
    }
}
