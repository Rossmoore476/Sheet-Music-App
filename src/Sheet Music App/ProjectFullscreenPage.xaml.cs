using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Threading.Tasks;
using Sheet_Music_App.Storage;
using Sheet_Music_App.Models;
using Windows.Storage;
using Windows.Storage.Streams;
using WinPdf = global::Windows.Data.Pdf;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using System.IO;

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

        private void RibbonSelector_SelectionChanged(Microsoft.UI.Xaml.Controls.SelectorBar sender, Microsoft.UI.Xaml.Controls.SelectorBarSelectionChangedEventArgs? args)
        {
            // Use the sender.SelectedItem (new selection) rather than IsSelected flags which may reflect previous state
            var selectedItem = sender?.SelectedItem as Microsoft.UI.Xaml.Controls.SelectorBarItem;

            DrawRibbon.Visibility = object.ReferenceEquals(selectedItem, DrawItem) ? Visibility.Visible : Visibility.Collapsed;
            InsertRibbon.Visibility = object.ReferenceEquals(selectedItem, InsertItem) ? Visibility.Visible : Visibility.Collapsed;
            EditRibbon.Visibility = object.ReferenceEquals(selectedItem, EditItem) ? Visibility.Visible : Visibility.Collapsed;
        }


        private async void FileSaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ContentDialog { Title = "Save", Content = "Save not implemented.", CloseButtonText = "OK" };
            if (this.XamlRoot != null) dlg.XamlRoot = this.XamlRoot;
            await dlg.ShowAsync();
        }

        private void FileCloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.CloseProjectFullscreen();
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
                // After loading basic project info, load and render PDF pages
                if (proj != null)
                {
                    await LoadAndRenderPdfPagesAsync(proj);
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

        private async Task LoadAndRenderPdfPagesAsync(Project proj)
        {
            PdfPagesPanel.Children.Clear();

            if (_storage == null) return;

            var projectFolder = _storage.GetProjectFolderPath(proj);
            var pdfsFolder = Path.Combine(projectFolder, "pdfs");
            if (!Directory.Exists(pdfsFolder)) return;

            // Iterate pieces in order and render each PDF's pages
            foreach (var piece in proj.Pieces)
            {
                foreach (var pdfMeta in piece.Pdfs)
                {
                    var filePath = Path.Combine(pdfsFolder, pdfMeta.FileName);
                    if (!File.Exists(filePath)) continue;

                    try
                    {
                        var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
                        var pdfDoc = await WinPdf.PdfDocument.LoadFromFileAsync(storageFile);
                        for (uint i = 0; i < pdfDoc.PageCount; i++)
                        {
                            using (var page = pdfDoc.GetPage(i))
                            {
                                using (var ras = new InMemoryRandomAccessStream())
                                {
                                    var options = new WinPdf.PdfPageRenderOptions();
                                    // render at default size; viewer will scale
                                    await page.RenderToStreamAsync(ras, options);
                                    ras.Seek(0);

                                    var bitmap = new BitmapImage();
                                    await bitmap.SetSourceAsync(ras);

                                    var img = new Microsoft.UI.Xaml.Controls.Image
                                    {
                                        Source = bitmap,
                                        Stretch = Stretch.Uniform,
                                        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                                        Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12)
                                    };

                                    this.PdfPagesPanel.Children.Add(img);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore rendering errors for individual files
                    }
                }
            }
        }
    }
}
