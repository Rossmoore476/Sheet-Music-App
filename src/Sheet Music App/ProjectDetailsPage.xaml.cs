using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using Sheet_Music_App.Storage;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Text;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using System.Threading.Tasks;
using System.IO;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Sheet_Music_App.Models;

namespace Sheet_Music_App
{


    public sealed partial class ProjectDetailsPage : Page
    {
        private LocalFolderStorage? _storage;
        private Guid? _projectId;
        private Project? _project;
        private ObservableCollection<ProjectPieceViewModel> _pieces = new ObservableCollection<ProjectPieceViewModel>(); // noop
        // editable pieces collection - type defined at namespace scope
        private ObservableCollection<PieceEditableViewModel> _editablePieces = new ObservableCollection<PieceEditableViewModel>();

        public ProjectDetailsPage()
        {
            this.InitializeComponent();
            // ItemsSource is set when the project is loaded to the editable pieces collection
            TitleEditButton.Click += TitleEditButton_Click;
            DescriptionEditButton.Click += DescriptionEditButton_Click;
            TitleEditBox.KeyDown += TitleEditBox_KeyDown;
            DescriptionEditBox.KeyDown += DescriptionEditBox_KeyDown;
            // noop patch: ensure file registers an update
        }

        private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_projectId != null && MainWindow.Current != null)
            {
                MainWindow.Current.ShowProjectFullscreen(_projectId.ToString());
            }
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

        private async void AddPieceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null || _storage == null) return;

            var panel = new StackPanel { Spacing = 8 };
            var titleBox = new TextBox { PlaceholderText = "Piece Title" };
            var composerBox = new TextBox { PlaceholderText = "Composer" };
            var chooseButton = new Button { Content = "Choose PDF" };
            var fileNameText = new TextBlock { FontSize = 12, Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"] };

            string? chosenPath = null;

            chooseButton.Click += async (s, ev) =>
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".pdf");
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                if (App.AppWindow != null)
                {
                    var hwnd = WindowNative.GetWindowHandle(App.AppWindow);
                    InitializeWithWindow.Initialize(picker, hwnd);
                }
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    chosenPath = file.Path;
                    chooseButton.Content = "Change PDF";
                    fileNameText.Text = file.Name;
                }
            };

            panel.Children.Add(titleBox);
            panel.Children.Add(composerBox);
            panel.Children.Add(chooseButton);
            panel.Children.Add(fileNameText);

            var dlg = new ContentDialog
            {
                Title = "Add Piece",
                Content = panel,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel"
            };
            if (this.XamlRoot != null) dlg.XamlRoot = this.XamlRoot;

            var res = await dlg.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                var title = titleBox.Text?.Trim() ?? string.Empty;
                var composer = composerBox.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(composer))
                {
                    await ShowMessageAsync("Please enter a title or composer for the piece.");
                    return;
                }

                var newPiece = new Piece { Title = title, Composer = composer };
                if (!string.IsNullOrEmpty(chosenPath) && File.Exists(chosenPath))
                {
                    var pdf = new PdfDocument { Id = Guid.NewGuid(), FileName = Path.GetFileName(chosenPath) };
                    newPiece.Pdfs.Add(pdf);
                    var projectFolder = _storage.GetProjectFolderPath(_project);
                    var pdfsFolder = Path.Combine(projectFolder, "pdfs");
                    Directory.CreateDirectory(pdfsFolder);
                    var destPath = Path.Combine(pdfsFolder, pdf.FileName);
                    File.Copy(chosenPath, destPath, true);
                }

                _project.Pieces.Add(newPiece);
                await _storage.SaveProjectAsync(_project);

                var idx = _editablePieces.Count + 1;
                var vm = new PieceEditableViewModel { Id = newPiece.Id, Index = idx, Title = newPiece.Title, Composer = newPiece.Composer };
                _editablePieces.Add(vm);
                _pieces.Add(new ProjectPieceViewModel { Index = idx, Title = newPiece.Title, Composer = newPiece.Composer });
                UpdatePieceMoveFlags();

                if (MainWindow.Current != null) await MainWindow.Current.PopulateProjectNavItemsAsync(suppressNavigation: true);
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
            if (this.XamlRoot != null) dlg.XamlRoot = this.XamlRoot;
            await dlg.ShowAsync();
        }

        private async void TitleEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (TitleEditBox.Visibility == Visibility.Visible)
            {
                // Save
                await SaveTitleAsync();
            }
            else
            {
                // Enter edit mode
                TitleText.Visibility = Visibility.Collapsed;
                TitleEditBox.Visibility = Visibility.Visible;
                TitleEditBox.Focus(FocusState.Programmatic);
                if (TitleEditIcon != null) TitleEditIcon.Symbol = Symbol.Save;
            }
        }

        private async void DescriptionEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (DescriptionEditBox.Visibility == Visibility.Visible)
            {
                await SaveDescriptionAsync();
            }
            else
            {
                DescriptionText.Visibility = Visibility.Collapsed;
                DescriptionEditBox.Visibility = Visibility.Visible;
                DescriptionEditBox.Focus(FocusState.Programmatic);
                if (DescriptionEditIcon != null) DescriptionEditIcon.Symbol = Symbol.Save;
            }
        }

        private async void TitleEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                await SaveTitleAsync();
            }
        }

        private async void DescriptionEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                try
                {
                    var state = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);
                    bool shiftDown = (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                    if (shiftDown)
                    {
                        // allow newline
                        return;
                    }

                    // otherwise save
                    e.Handled = true;
                    await SaveDescriptionAsync();
                }
                catch
                {
                    // fallback: attempt save
                    e.Handled = true;
                    await SaveDescriptionAsync();
                }
            }
        }

        private async Task SaveTitleAsync()
        {
            if (_project == null) return;
            var newTitle = TitleEditBox.Text?.Trim() ?? string.Empty;
            if (newTitle == _project.Name)
            {
                // no change, just exit edit mode
                TitleEditBox.Visibility = Visibility.Collapsed;
                TitleText.Visibility = Visibility.Visible;
                if (TitleEditIcon != null) TitleEditIcon.Symbol = Symbol.Edit;
                return;
            }

            _project.Name = newTitle;
            await _storage!.SaveProjectAsync(_project);
            TitleText.Text = newTitle;
            TitleEditBox.Visibility = Visibility.Collapsed;
            TitleText.Visibility = Visibility.Visible;
            if (TitleEditIcon != null) TitleEditIcon.Symbol = Symbol.Edit;

            // refresh nav labels
            if (MainWindow.Current != null) await MainWindow.Current.PopulateProjectNavItemsAsync(suppressNavigation: true);
                            // noop: ensure file registers an update
        }

        private async Task SaveDescriptionAsync()
        {
            if (_project == null) return;
            var newDesc = DescriptionEditBox.Text ?? string.Empty;
            if (newDesc == _project.Description)
            {
                DescriptionEditBox.Visibility = Visibility.Collapsed;
                DescriptionText.Visibility = Visibility.Visible;
                if (DescriptionEditIcon != null) DescriptionEditIcon.Symbol = Symbol.Edit;
                return;
            }

            _project.Description = newDesc;
            await _storage!.SaveProjectAsync(_project);
            if (string.IsNullOrWhiteSpace(newDesc))
            {
                DescriptionText.Text = "No description. Click edit to add.";
                DescriptionText.FontStyle = FontStyle.Italic;
            }
            else
            {
                DescriptionText.Text = newDesc;
                DescriptionText.FontStyle = FontStyle.Normal;
            }
            DescriptionEditBox.Visibility = Visibility.Collapsed;
            DescriptionText.Visibility = Visibility.Visible;
            if (DescriptionEditIcon != null) DescriptionEditIcon.Symbol = Symbol.Edit;

            // refresh nav labels if needed
            if (MainWindow.Current != null) await MainWindow.Current.PopulateProjectNavItemsAsync(suppressNavigation: true);
        }

        private async void EditPieceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                Guid id;
                if (btn.CommandParameter is Guid gid)
                {
                    id = gid;
                }
                else if (btn.CommandParameter is string s && Guid.TryParse(s, out var parsed))
                {
                    id = parsed;
                }
                else if (btn.DataContext is PieceEditableViewModel vmctx)
                {
                    id = vmctx.Id;
                }
                else
                {
                    return;
                }

                var vm = _editablePieces.FirstOrDefault(p => p.Id == id);
                if (vm == null) return;

                if (vm.IsEditing)
                {
                    // save changes
                    var piece = _project?.Pieces.FirstOrDefault(p => p.Id == vm.Id);
                    if (piece != null)
                    {
                        piece.Title = vm.Title;
                        piece.Composer = vm.Composer;
                        await _storage!.SaveProjectAsync(_project);

                        // update non-editable list so the UI reflects the new values
                        var idx = _editablePieces.IndexOf(vm);
                        if (idx >= 0 && idx < _pieces.Count)
                        {
                            // _pieces is zero-based; vm.Index is 1-based
                            _pieces[idx] = new ProjectPieceViewModel { Index = vm.Index, Title = vm.Title, Composer = vm.Composer };
                        }

                        // rebuild index mapping for editable collection
                        int i = 1;
                        foreach (var ed in _editablePieces)
                        {
                            ed.Index = i;
                            i++;
                        }
                    }
                    // update move flags
                    UpdatePieceMoveFlags();
                    vm.IsEditing = false;
                }
                else
                {
                    vm.IsEditing = true;
                }
            }
        }

        private async void DeletePieceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item)
            {
                if (item.CommandParameter is Guid id)
                {
                    var vm = _editablePieces.FirstOrDefault(p => p.Id == id);
                    if (vm == null) return;

                    var dlg = new ContentDialog
                    {
                        Title = "Delete piece?",
                        Content = "Are you sure you want to delete the piece '" + vm.Title + "'? This cannot be undone.",
                        PrimaryButtonText = "Delete",
                        CloseButtonText = "Cancel"
                    };
                    if (this.XamlRoot != null) dlg.XamlRoot = this.XamlRoot;

                    var res = await dlg.ShowAsync();
                    if (res == ContentDialogResult.Primary)
                    {
                        try
                        {
                            var piece = _project?.Pieces.FirstOrDefault(p => p.Id == id);
                            if (piece != null)
                            {
                                _project.Pieces.Remove(piece);
                                await _storage!.SaveProjectAsync(_project);
                            }

                            _editablePieces.Remove(vm);
                            // reindex
                            int i = 1;
                            foreach (var ed in _editablePieces)
                            {
                                ed.Index = i++;
                            }

                            if (_editablePieces.Count == 0)
                            {
                                NoPiecesText.Visibility = Visibility.Visible;
                                PiecesItemsControl.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                UpdatePieceMoveFlags();
                            }

                            if (MainWindow.Current != null) await MainWindow.Current.PopulateProjectNavItemsAsync(suppressNavigation: true);
                        }
                        catch (Exception ex)
                        {
                            var err = new ContentDialog
                            {
                                Title = "Delete failed",
                                Content = "Failed to delete piece: " + ex.Message,
                                CloseButtonText = "OK"
                            };
                            if (this.XamlRoot != null) err.XamlRoot = this.XamlRoot;
                            await err.ShowAsync();
                        }
                    }
                }
            }
        }

        private void UpdatePieceMoveFlags()
        {
            for (int i = 0; i < _editablePieces.Count; i++)
            {
                _editablePieces[i].Index = i + 1;
                _editablePieces[i].CanMoveUp = i > 0;
                _editablePieces[i].CanMoveDown = i < _editablePieces.Count - 1;
            }
            // also update display list indexes
            for (int i = 0; i < _pieces.Count; i++)
            {
                _pieces[i].Index = i + 1;
            }
        }

        private async void MovePieceUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item)
            {
                Guid id;
                if (item.CommandParameter is Guid gid) id = gid;
                else if (item.CommandParameter is string s && Guid.TryParse(s, out var parsed)) id = parsed;
                else return;

                var idx = _editablePieces.ToList().FindIndex(p => p.Id == id);
                if (idx <= 0) return;

                // swap in editable collection
                var temp = _editablePieces[idx - 1];
                _editablePieces[idx - 1] = _editablePieces[idx];
                _editablePieces[idx] = temp;

                // swap in project model
                var pieceModelIdx = _project?.Pieces.FindIndex(p => p.Id == id) ?? -1;
                if (pieceModelIdx > 0)
                {
                    var tmpModel = _project.Pieces[pieceModelIdx - 1];
                    _project.Pieces[pieceModelIdx - 1] = _project.Pieces[pieceModelIdx];
                    _project.Pieces[pieceModelIdx] = tmpModel;
                    await _storage!.SaveProjectAsync(_project);
                }

                UpdatePieceMoveFlags();
            }
        }

        private async void MovePieceDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item)
            {
                Guid id;
                if (item.CommandParameter is Guid gid) id = gid;
                else if (item.CommandParameter is string s && Guid.TryParse(s, out var parsed)) id = parsed;
                else return;

                var idx = _editablePieces.ToList().FindIndex(p => p.Id == id);
                if (idx < 0 || idx >= _editablePieces.Count - 1) return;

                // swap
                var temp = _editablePieces[idx + 1];
                _editablePieces[idx + 1] = _editablePieces[idx];
                _editablePieces[idx] = temp;

                // swap in model
                var pieceModelIdx = _project?.Pieces.FindIndex(p => p.Id == id) ?? -1;
                if (pieceModelIdx >= 0 && pieceModelIdx < _project.Pieces.Count - 1)
                {
                    var tmpModel = _project.Pieces[pieceModelIdx + 1];
                    _project.Pieces[pieceModelIdx + 1] = _project.Pieces[pieceModelIdx];
                    _project.Pieces[pieceModelIdx] = tmpModel;
                    await _storage!.SaveProjectAsync(_project);
                }

                UpdatePieceMoveFlags();
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
                    _project = proj;
                    TitleText.Text = proj.Name;
                    TitleEditBox.Text = proj.Name;

                    // Description: show placeholder when empty, but keep edit box text empty so user sees a blank field when editing
                    if (string.IsNullOrWhiteSpace(proj.Description))
                    {
                        DescriptionText.Text = "No description. Click edit to add.";
                        DescriptionText.FontStyle = FontStyle.Italic;
                        DescriptionText.Foreground = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"];
                        DescriptionEditBox.Text = string.Empty;
                    }
                    else
                    {
                        DescriptionText.Text = proj.Description;
                        DescriptionText.FontStyle = FontStyle.Normal;
                        DescriptionEditBox.Text = proj.Description;
                    }

                    CreatedText.Text = "Created " + proj.Created.ToLocalTime().ToString("d");

                    _pieces.Clear();
                    _editablePieces.Clear();
                    int idx = 1;
                    foreach (var p in proj.Pieces)
                    {
                        _pieces.Add(new ProjectPieceViewModel { Index = idx, Title = p.Title, Composer = p.Composer });
                        _editablePieces.Add(new PieceEditableViewModel { Id = p.Id, Index = idx, Title = p.Title, Composer = p.Composer });
                        idx++;
                    }

                    // update move flags
                    UpdatePieceMoveFlags();

                    // Show placeholder when no pieces
                    if (_editablePieces.Count == 0)
                    {
                        NoPiecesText.Visibility = Visibility.Visible;
                        PiecesItemsControl.Visibility = Visibility.Collapsed;
                        // bind ItemsControl regardless of visibility so the UI is always backed by the editable collection
                        PiecesItemsControl.ItemsSource = _editablePieces;
                    }
                    else
                    {
                        NoPiecesText.Visibility = Visibility.Collapsed;
                        PiecesItemsControl.Visibility = Visibility.Visible;
                        PiecesItemsControl.ItemsSource = _editablePieces;
                    }
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
