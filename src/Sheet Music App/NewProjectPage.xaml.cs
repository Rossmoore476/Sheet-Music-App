using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Windows.Storage;
using System;
using Sheet_Music_App.Models;
using Sheet_Music_App.Storage;
using System.Linq;
using System.IO;

namespace Sheet_Music_App
{
    public sealed partial class NewProjectPage : Page
    {
        private ObservableCollection<PieceViewModel> Pieces { get; } = new ObservableCollection<PieceViewModel>();
        private LocalFolderStorage _storage = new LocalFolderStorage();
        private bool _suppressNavigationPrompt = false;

        public NewProjectPage()
        {
            this.InitializeComponent();
            PiecesListView.ItemsSource = Pieces;
            AddPieceButton.Click += AddPieceButton_Click;
            SaveProjectButton.Click += SaveProjectButton_Click;
            this.Loaded += NewProjectPage_Loaded;
            this.Unloaded += NewProjectPage_Unloaded;
        }

        private void NewProjectPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                this.Frame.Navigating += Frame_Navigating;
            }
        }

        private void NewProjectPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                this.Frame.Navigating -= Frame_Navigating;
            }
        }

        private bool HasUnsavedChanges()
        {
            if (!string.IsNullOrWhiteSpace(ProjectNameTextBox.Text)) return true;
            if (!string.IsNullOrWhiteSpace(ProjectDescriptionTextBox.Text)) return true;
            foreach (var p in Pieces)
            {
                if (!string.IsNullOrWhiteSpace(p.Title)) return true;
                if (!string.IsNullOrWhiteSpace(p.Composer)) return true;
                if (!string.IsNullOrWhiteSpace(p.PdfPath)) return true;
            }
            return false;
        }

        private async void Frame_Navigating(object sender, Microsoft.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
        {
            try
            {
                if (_suppressNavigationPrompt)
                {
                    // allow navigation once after suppression
                    _suppressNavigationPrompt = false;
                    return;
                }

                // If navigating to the same page type (e.g., clicking New Project while already on it), allow without prompting
                if (e.SourcePageType == this.GetType()) return;

                if (!HasUnsavedChanges()) return;

                // cancel navigation and ask the user
                e.Cancel = true;

                var dlg = new ContentDialog
                {
                    Title = "Discard changes?",
                    Content = "You have unsaved changes. If you navigate away they will be lost. Do you want to discard changes?",
                    PrimaryButtonText = "Discard",
                    CloseButtonText = "Cancel"
                };
                if (this.XamlRoot != null) dlg.XamlRoot = this.XamlRoot;

                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    // proceed with the originally requested navigation
                    _suppressNavigationPrompt = true;
                    // Try to navigate to the requested page
                    if (e.SourcePageType != null)
                    {
                        this.Frame?.Navigate(e.SourcePageType, e.Parameter);
                    }
                    else if (e.NavigationMode == Microsoft.UI.Xaml.Navigation.NavigationMode.Back && this.Frame.CanGoBack)
                    {
                        this.Frame.GoBack();
                    }
                }
            }
            catch { }
        }

        private void AddPieceButton_Click(object sender, RoutedEventArgs e)
        {
            Pieces.Add(new PieceViewModel());
        }

        private async void OnChoosePdfClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PieceViewModel vm)
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".pdf");
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

                // Initialize picker with the app window handle to avoid "Invalid window handle" COMException on WinUI3 desktop apps
                if (App.AppWindow != null)
                {
                    var hwnd = WindowNative.GetWindowHandle(App.AppWindow);
                    InitializeWithWindow.Initialize(picker, hwnd);
                }

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    vm.PdfPath = file.Path;
                    vm.PdfFileName = file.Name;
                    // update the button text to indicate a PDF is already chosen
                    if (sender is Button sbtn)
                    {
                        sbtn.Content = "Choose new PDF";
                    }
                }
            }
        }

        private void OnRemovePieceClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PieceViewModel vm)
            {
                Pieces.Remove(vm);
            }
        }

        private async void SaveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
            string name = ProjectNameTextBox.Text?.Trim() ?? string.Empty;
            string description = ProjectDescriptionTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                await ShowMessageAsync("Please enter a project name.");
                return;
            }

            // If there are pieces, validate each has a valid PDF path
            if (Pieces.Count > 0)
            {
                foreach (var p in Pieces)
                {
                    if (string.IsNullOrEmpty(p.PdfPath) || !File.Exists(p.PdfPath))
                    {
                        await ShowMessageAsync("Each piece must have a valid PDF selected.");
                        return;
                    }
                }
            }

            // Construct project model
            var project = new Project
            {
                Name = name,
                Description = description,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
            };

            foreach (var p in Pieces)
            {
                var piece = new Piece { Title = p.Title ?? string.Empty, Composer = p.Composer ?? string.Empty };
                var pdf = new PdfDocument { Id = Guid.NewGuid(), FileName = Path.GetFileName(p.PdfPath) };
                piece.Pdfs.Add(pdf);
                project.Pieces.Add(piece);
            }

            // Set storage root to user's chosen local folder (if configured)
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var provider = (localSettings.Values["StorageProvider"] as string) ?? "Local";
            if (provider == "Local")
            {
                var root = localSettings.Values["LocalStoragePath"] as string;
                if (!string.IsNullOrEmpty(root))
                {
                    await _storage.SetRootPathAsync(root);
                    project.Storage.Provider = "Local";
                    project.Storage.Path = root;
                }
            }

            // Create project folder and copy PDFs into it (if any pieces exist)
            await _storage.CreateProjectAsync(project);
            var projectFolder = _storage.GetProjectFolderPath(project);

            if (project.Pieces.Count > 0)
            {
                var pdfsFolder = Path.Combine(projectFolder, "pdfs");
                Directory.CreateDirectory(pdfsFolder);

                // Copy files and update project model filenames to copied names
                for (int i = 0; i < Pieces.Count; i++)
                {
                    var src = Pieces[i].PdfPath!;
                    var destName = project.Pieces[i].Pdfs[0].FileName;
                    var destPath = Path.Combine(pdfsFolder, destName);
                    File.Copy(src, destPath, true);
                }

                // Save updated project (in case any metadata changed)
                await _storage.SaveProjectAsync(project);
            }
            else
            {
                // No pieces: project already created by CreateProjectAsync
            }

            await ShowMessageAsync("Project saved.");
            // Refresh navigation in main window so new project appears in the sidebar (don't cause intermediate navigation)
            if (MainWindow.Current != null)
            {
                await MainWindow.Current.PopulateProjectNavItemsAsync(suppressNavigation: true);
            }

            // Navigate to the project's details page
            _suppressNavigationPrompt = true;
            this.Frame?.Navigate(typeof(ProjectDetailsPage), project.Id.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error saving project: " + ex.ToString());
                await ShowMessageAsync("Error saving project: " + ex.Message + "\n\nFull details written to Debug Output.");
            }

        }

        private async Task ShowMessageAsync(string text)
        {
            var dlg = new ContentDialog
            {
                Title = "Sheet Music App",
                Content = text,
                CloseButtonText = "OK"
            };
            // ContentDialog requires a XamlRoot when shown from a page that may not be
            // directly attached to the visual tree in some hosting scenarios. Set it
            // to this page's XamlRoot to ensure the dialog can be displayed.
            if (this.XamlRoot != null)
            {
                dlg.XamlRoot = this.XamlRoot;
            }
            await dlg.ShowAsync();
        }
    }

    class PieceViewModel : INotifyPropertyChanged
    {
        private string? _title;
        private string? _composer;
        private string? _pdfPath;
        private string? _pdfFileName;

        public string? Title { get => _title; set { _title = value; Notify(); } }
        public string? Composer { get => _composer; set { _composer = value; Notify(); } }
        public string? PdfPath { get => _pdfPath; set { _pdfPath = value; Notify(); } }
        public string? PdfFileName { get => _pdfFileName; set { _pdfFileName = value; Notify(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
