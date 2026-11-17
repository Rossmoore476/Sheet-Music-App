using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Sheet_Music_App.Storage;
using Sheet_Music_App.Models;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
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

        // Pages are rasterized above their natural size so they stay crisp when zoomed in.
        private const double RenderScale = 2.0;
        private const float ZoomStep = 1.25f;
        // Match PdfPagesPanel.Padding in the XAML: 24 on the left and right, 24 at the bottom only
        // (no top padding, so the first page sits right under the ribbon).
        private const double PagesHorizontalPadding = 48;
        private const double PagesVerticalPadding = 24;

        private enum FitMode { None, Width, Height }

        private double _widestPageWidth;
        private double _tallestPageHeight;
        private FitMode _fitMode = FitMode.Width;

        private enum RibbonDisplayMode { AlwaysShow, TabsOnly }

        private const string RibbonDisplayModeSettingKey = "RibbonDisplayMode";
        private RibbonDisplayMode _ribbonDisplayMode = RibbonDisplayMode.AlwaysShow;
        private Microsoft.UI.Xaml.Controls.SelectorBarItem? _lastSelectedTab;

        public ProjectFullscreenPage()
        {
            this.InitializeComponent();
            AddZoomKeyboardAccelerators();

            // Ctrl+F1 toggles the ribbon display mode, as in Word.
            AddAccelerator(VirtualKey.F1, ToggleRibbonDisplayMode);

            // handledEventsToo: the document ScrollViewer marks pointer events handled.
            AddHandler(PointerPressedEvent, new PointerEventHandler(Page_PointerPressed), true);
            KeyDown += Page_KeyDown;

            SetRibbonDisplayMode(LoadRibbonDisplayMode(), save: false);
        }

        private void RibbonSelector_SelectionChanged(Microsoft.UI.Xaml.Controls.SelectorBar sender, Microsoft.UI.Xaml.Controls.SelectorBarSelectionChangedEventArgs? args)
        {
            // Use the sender.SelectedItem (new selection) rather than IsSelected flags which may reflect previous state
            var selectedItem = sender?.SelectedItem as Microsoft.UI.Xaml.Controls.SelectorBarItem;

            DrawRibbon.Visibility = object.ReferenceEquals(selectedItem, DrawItem) ? Visibility.Visible : Visibility.Collapsed;
            InsertRibbon.Visibility = object.ReferenceEquals(selectedItem, InsertItem) ? Visibility.Visible : Visibility.Collapsed;
            ViewRibbon.Visibility = object.ReferenceEquals(selectedItem, ViewItem) ? Visibility.Visible : Visibility.Collapsed;

            if (selectedItem != null)
            {
                _lastSelectedTab = selectedItem;

                // In "Show tabs only" mode no tab is selected while the ribbon is closed,
                // so selecting one (click, touch or keyboard) is what opens it.
                ShowRibbonOverlay();
            }
        }

        #region Ribbon display options

        private static RibbonDisplayMode LoadRibbonDisplayMode()
        {
            try
            {
                var value = ApplicationData.Current.LocalSettings.Values[RibbonDisplayModeSettingKey] as string;
                return value == nameof(RibbonDisplayMode.TabsOnly) ? RibbonDisplayMode.TabsOnly : RibbonDisplayMode.AlwaysShow;
            }
            catch
            {
                return RibbonDisplayMode.AlwaysShow;
            }
        }

        private void SetRibbonDisplayMode(RibbonDisplayMode mode, bool save)
        {
            _ribbonDisplayMode = mode;
            AlwaysShowRibbonItem.IsChecked = mode == RibbonDisplayMode.AlwaysShow;
            ShowTabsOnlyItem.IsChecked = mode == RibbonDisplayMode.TabsOnly;

            if (mode == RibbonDisplayMode.AlwaysShow)
            {
                // Docked in its own row: the document starts below it.
                Grid.SetRow(RibbonHost, 1);
                RibbonHost.BorderThickness = new Thickness(0);
                RibbonHost.Visibility = Visibility.Visible;

                // A docked ribbon always shows a tab: bring back the last one used.
                RibbonSelectorBar.SelectedItem ??= _lastSelectedTab ?? DrawItem;
            }
            else
            {
                // Floats over the top of the document while open, so opening it doesn't move the music.
                Grid.SetRow(RibbonHost, 2);
                RibbonHost.BorderThickness = new Thickness(0, 0, 0, 1);
                RibbonHost.Visibility = Visibility.Collapsed;

                // Closed ribbon: no tab is selected, so no tab shows the selection indicator.
                RibbonSelectorBar.SelectedItem = null;
            }
            UpdateHeaderDivider();

            if (save)
            {
                try
                {
                    ApplicationData.Current.LocalSettings.Values[RibbonDisplayModeSettingKey] = mode.ToString();
                }
                catch
                {
                    // Preference just won't persist.
                }
            }
        }

        private void ToggleRibbonDisplayMode() =>
            SetRibbonDisplayMode(_ribbonDisplayMode == RibbonDisplayMode.AlwaysShow ? RibbonDisplayMode.TabsOnly : RibbonDisplayMode.AlwaysShow, save: true);

        private void AlwaysShowRibbonItem_Click(object sender, RoutedEventArgs e) =>
            SetRibbonDisplayMode(RibbonDisplayMode.AlwaysShow, save: true);

        private void ShowTabsOnlyItem_Click(object sender, RoutedEventArgs e) =>
            SetRibbonDisplayMode(RibbonDisplayMode.TabsOnly, save: true);

        private bool IsRibbonOverlayOpen =>
            _ribbonDisplayMode == RibbonDisplayMode.TabsOnly && RibbonHost.Visibility == Visibility.Visible;

        private void ShowRibbonOverlay()
        {
            if (_ribbonDisplayMode == RibbonDisplayMode.TabsOnly)
            {
                RibbonHost.Visibility = Visibility.Visible;
                UpdateHeaderDivider();
            }
        }

        private void HideRibbonOverlay()
        {
            if (_ribbonDisplayMode == RibbonDisplayMode.TabsOnly)
            {
                RibbonHost.Visibility = Visibility.Collapsed;
                RibbonSelectorBar.SelectedItem = null;
                UpdateHeaderDivider();
            }
        }

        /// <summary>
        /// While the floating ribbon is open, drop the divider under the tabs so the tabs and ribbon
        /// read as one surface; the floating ribbon draws its own divider along its bottom edge.
        /// </summary>
        private void UpdateHeaderDivider()
        {
            HeaderBackground.BorderThickness = IsRibbonOverlayOpen ? new Thickness(0) : new Thickness(0, 0, 0, 1);
        }


        private void Page_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Clicking anywhere other than the ribbon or its tabs closes the floating ribbon.
            // (The display options menu is a popup outside this tree, so it doesn't count as clicking off.)
            if (!IsRibbonOverlayOpen) return;

            var source = e.OriginalSource as DependencyObject;
            if (IsWithin(source, RibbonHost) || IsWithin(source, RibbonSelectorBar)) return;

            HideRibbonOverlay();
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape && IsRibbonOverlayOpen)
            {
                HideRibbonOverlay();
                e.Handled = true;
            }
        }

        private static bool IsWithin(DependencyObject? element, DependencyObject ancestor)
        {
            while (element != null)
            {
                if (ReferenceEquals(element, ancestor)) return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        #endregion


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

        private async void PresentButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ContentDialog { Title = "Present", Content = "Present not implemented.", CloseButtonText = "OK" };
            if (this.XamlRoot != null) dlg.XamlRoot = this.XamlRoot;
            await dlg.ShowAsync();
        }

        #region Zoom

        private void AddZoomKeyboardAccelerators()
        {
            // Ctrl + '=' / numpad '+' zooms in, Ctrl + '-' / numpad '-' zooms out, Ctrl + 0 fits to width.
            // (Ctrl + mouse wheel, pinch and touchpad gestures are handled natively by the ScrollViewer.)
            AddAccelerator((VirtualKey)187, () => ZoomBy(ZoomStep));       // VK_OEM_PLUS
            AddAccelerator(VirtualKey.Add, () => ZoomBy(ZoomStep));
            AddAccelerator((VirtualKey)189, () => ZoomBy(1 / ZoomStep));   // VK_OEM_MINUS
            AddAccelerator(VirtualKey.Subtract, () => ZoomBy(1 / ZoomStep));
            AddAccelerator(VirtualKey.Number0, () => ApplyFit(FitMode.Width));
            AddAccelerator(VirtualKey.NumberPad0, () => ApplyFit(FitMode.Width));
        }

        private void AddAccelerator(VirtualKey key, Action action)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = VirtualKeyModifiers.Control };
            accelerator.Invoked += (_, args) =>
            {
                action();
                args.Handled = true;
            };
            KeyboardAccelerators.Add(accelerator);
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e) => ZoomBy(ZoomStep);

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => ZoomBy(1 / ZoomStep);

        private void FitWidthButton_Click(object sender, RoutedEventArgs e) => ApplyFit(FitMode.Width);

        private void FitHeightButton_Click(object sender, RoutedEventArgs e) => ApplyFit(FitMode.Height);

        /// <summary>Multiplies the current zoom factor, keeping the centre of the viewport in place.</summary>
        private void ZoomBy(float multiplier)
        {
            _fitMode = FitMode.None;
            ZoomTo(ClampZoom(PdfScrollViewer.ZoomFactor * multiplier), disableAnimation: false);
        }

        private float ClampZoom(double zoom) =>
            (float)Math.Clamp(zoom, PdfScrollViewer.MinZoomFactor, PdfScrollViewer.MaxZoomFactor);

        private void ApplyFit(FitMode mode)
        {
            _fitMode = mode;
            if (GetFitZoomFactor(mode) is not float zoom) return;

            if (mode == FitMode.Height && GetCurrentPage() is FrameworkElement page)
            {
                // Centre the page the user is currently reading so the whole page is on screen.
                var horizontal = Math.Max(0, (PdfPagesPanel.ActualWidth * zoom - PdfScrollViewer.ViewportWidth) / 2);
                var vertical = Math.Max(0, (page.ActualOffset.Y + page.ActualHeight / 2) * zoom - PdfScrollViewer.ViewportHeight / 2);
                PdfScrollViewer.ChangeView(horizontal, vertical, zoom, disableAnimation: true);
            }
            else
            {
                ZoomTo(zoom, disableAnimation: true);
            }
        }

        private float? GetFitZoomFactor(FitMode mode)
        {
            double viewport, content;
            switch (mode)
            {
                case FitMode.Width:
                    viewport = PdfScrollViewer.ViewportWidth;
                    content = _widestPageWidth + PagesHorizontalPadding;
                    break;
                case FitMode.Height:
                    viewport = PdfScrollViewer.ViewportHeight;
                    content = _tallestPageHeight + PagesVerticalPadding;
                    break;
                default:
                    return null;
            }

            // No pages measured yet (only padding) means there's nothing to fit.
            if (viewport <= 0 || _widestPageWidth <= 0 || _tallestPageHeight <= 0) return null;
            return ClampZoom(viewport / content);
        }

        /// <summary>The page under the vertical centre of the viewport.</summary>
        private FrameworkElement? GetCurrentPage()
        {
            var centreY = (PdfScrollViewer.VerticalOffset + PdfScrollViewer.ViewportHeight / 2) / PdfScrollViewer.ZoomFactor;
            FrameworkElement? last = null;
            foreach (var child in PdfPagesPanel.Children)
            {
                if (child is not FrameworkElement page) continue;
                if (centreY < page.ActualOffset.Y + page.ActualHeight + PdfPagesPanel.Spacing) return page;
                last = page;
            }
            return last;
        }

        private void ZoomTo(float targetZoom, bool disableAnimation)
        {
            var ratio = targetZoom / PdfScrollViewer.ZoomFactor;

            // Offsets are in zoomed coordinates; scale them so the viewport centre stays anchored.
            var halfWidth = PdfScrollViewer.ViewportWidth / 2;
            var halfHeight = PdfScrollViewer.ViewportHeight / 2;
            var horizontal = Math.Max(0, (PdfScrollViewer.HorizontalOffset + halfWidth) * ratio - halfWidth);
            var vertical = Math.Max(0, (PdfScrollViewer.VerticalOffset + halfHeight) * ratio - halfHeight);

            PdfScrollViewer.ChangeView(horizontal, vertical, targetZoom, disableAnimation);
        }

        private static string FormatZoom(float zoom) => $"{Math.Round(zoom * 100)}%";

        private void PdfScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            var zoom = PdfScrollViewer.ZoomFactor;

            // Don't overwrite what the user is typing.
            if (ZoomLevelBox.FocusState == FocusState.Unfocused)
            {
                ZoomLevelBox.Text = FormatZoom(zoom);
            }
            ZoomInButton.IsEnabled = zoom < PdfScrollViewer.MaxZoomFactor - 0.001f;
            ZoomOutButton.IsEnabled = zoom > PdfScrollViewer.MinZoomFactor + 0.001f;

            // A pinch or Ctrl+wheel zoom by the user leaves fit mode.
            if (!e.IsIntermediate && GetFitZoomFactor(_fitMode) is float fitZoom && Math.Abs(zoom - fitZoom) > 0.005f)
            {
                _fitMode = FitMode.None;
            }
        }

        private void PdfScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Keep the chosen fit as the window resizes, until the user picks their own zoom.
            if (_fitMode != FitMode.None)
            {
                ApplyFit(_fitMode);
            }
        }

        private void ZoomLevelBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                CommitZoomLevelBox();
                ZoomLevelBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                ZoomLevelBox.Text = FormatZoom(PdfScrollViewer.ZoomFactor);
                ZoomLevelBox.SelectAll();
                e.Handled = true;
            }
        }

        private void ZoomLevelBox_LostFocus(object sender, RoutedEventArgs e) => CommitZoomLevelBox();

        /// <summary>Applies a typed zoom level ("150" or "150%"); invalid input reverts to the current zoom.</summary>
        private void CommitZoomLevelBox()
        {
            var text = ZoomLevelBox.Text.Trim().TrimEnd('%').Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var percent) && percent > 0)
            {
                var target = ClampZoom(percent / 100);
                _fitMode = FitMode.None;
                ZoomTo(target, disableAnimation: true);
                ZoomLevelBox.Text = FormatZoom(target);
            }
            else
            {
                ZoomLevelBox.Text = FormatZoom(PdfScrollViewer.ZoomFactor);
            }
        }

        #endregion

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
            _widestPageWidth = 0;
            _tallestPageHeight = 0;
            _fitMode = FitMode.Width;

            if (_storage == null) return;

            ShowLoadingIndicator(true);
            try
            {
                await RenderPdfPagesAsync(proj, _storage);
            }
            finally
            {
                ShowLoadingIndicator(false);
            }
        }

        private void ShowLoadingIndicator(bool show)
        {
            // Toggle Visibility as well as IsActive: an inactive ProgressRing plays a fade-out,
            // which left it lingering over the document after loading finished.
            LoadingRing.IsActive = show;
            LoadingRing.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task RenderPdfPagesAsync(Project proj, LocalFolderStorage storage)
        {
            var projectFolder = storage.GetProjectFolderPath(proj);
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
                            PdfPagesPanel.Children.Add(await RenderPageAsync(pdfDoc, i, piece.Title));

                            // The music is visible once the first page is in; later pages keep streaming in behind it.
                            if (PdfPagesPanel.Children.Count == 1) ShowLoadingIndicator(false);
                        }
                    }
                    catch
                    {
                        // ignore rendering errors for individual files
                    }
                }
            }
        }

        private async Task<UIElement> RenderPageAsync(WinPdf.PdfDocument pdfDoc, uint pageIndex, string pieceTitle)
        {
            using var page = pdfDoc.GetPage(pageIndex);
            using var ras = new InMemoryRandomAccessStream();

            // PdfPage.Size is in DIPs; render at a higher pixel density so zooming stays sharp.
            var pageWidth = page.Size.Width;
            var pageHeight = page.Size.Height;
            var options = new WinPdf.PdfPageRenderOptions
            {
                DestinationWidth = (uint)Math.Round(pageWidth * RenderScale),
                DestinationHeight = (uint)Math.Round(pageHeight * RenderScale),
            };
            await page.RenderToStreamAsync(ras, options);
            ras.Seek(0);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(ras);

            // A fixed DIP size keeps the layout stable; the ScrollViewer's zoom handles scaling.
            var img = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                Width = pageWidth,
                Height = pageHeight,
            };
            AutomationProperties.SetName(img, string.IsNullOrEmpty(pieceTitle)
                ? $"Page {pageIndex + 1}"
                : $"{pieceTitle}, page {pageIndex + 1}");

            // Page cards add a 1px border on each side.
            var cardHeight = pageHeight + 2;
            if (pageWidth > _widestPageWidth || cardHeight > _tallestPageHeight)
            {
                _widestPageWidth = Math.Max(_widestPageWidth, pageWidth);
                _tallestPageHeight = Math.Max(_tallestPageHeight, cardHeight);
                if (_fitMode != FitMode.None)
                {
                    // Wait for layout so the new page has a size and position.
                    DispatcherQueue.TryEnqueue(() => ApplyFit(_fitMode));
                }
            }

            return new Border
            {
                Child = img,
                // Sheet music is printed on white regardless of the app theme.
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
        }
    }
}
