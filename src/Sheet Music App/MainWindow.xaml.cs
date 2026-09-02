using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Sheet_Music_App
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // kept Category types for potential template use; project nav items are populated dynamically
        private Sheet_Music_App.Storage.LocalFolderStorage? _navStorage;
        public static new MainWindow? Current { get; private set; }
        // track last committed selected item so we can revert UI selection when navigation is cancelled
        private object? _lastSelectedItem;
        // currently displayed project id (Tag) when ProjectDetailsPage is shown
        private string? _currentProjectId;

        public MainWindow() 
        {
            InitializeComponent();
            Current = this;

            // Populate project nav items dynamically from storage
            _ = PopulateProjectNavItemsAsync();

            // Wire up back handling and navigation tracking so the back button works between pages
            nvSample.BackRequested += NvSample_BackRequested;
            ContentFrame.Navigated += ContentFrame_Navigated;
            // remember initial selection
            _lastSelectedItem = nvSample.SelectedItem;
            // initial page selection will be handled after nav items are populated
        }

        public async System.Threading.Tasks.Task PopulateProjectNavItemsAsync(bool suppressNavigation = false)
        {
            // respect user's configured local storage path if set
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var provider = (localSettings.Values["StorageProvider"] as string) ?? "Local";
            var root = localSettings.Values["LocalStoragePath"] as string;
            if (provider == "Local" && !string.IsNullOrEmpty(root))
            {
                _navStorage = new Sheet_Music_App.Storage.LocalFolderStorage(root);
            }
            else
            {
                _navStorage = new Sheet_Music_App.Storage.LocalFolderStorage();
            }

            var summaries = (await _navStorage.ListProjectsAsync()).ToList();

            if (summaries.Count == 0)
            {
                HomeNavItem.Visibility = Visibility.Collapsed;
                ProjectsHeader.Visibility = Visibility.Collapsed;
                if (!suppressNavigation)
                {
                    nvSample.SelectedItem = NewProjectNavItem;
                    ContentFrame.Navigate(typeof(NewProjectPage));
                    this.Title = "New Project - Sheet Music App";
                }
                return;
            }

            HomeNavItem.Visibility = Visibility.Visible;
            ProjectsHeader.Visibility = Visibility.Visible;

            int insertIndex = nvSample.MenuItems.IndexOf(NewProjectNavItem);
            if (insertIndex < 0) insertIndex = nvSample.MenuItems.Count;

            // Remove any existing project items that were previously inserted between the ProjectsHeader and NewProjectNavItem
            var headerIndex = nvSample.MenuItems.IndexOf(ProjectsHeader);
            if (headerIndex >= 0)
            {
                // Items directly after the header up to (but not including) the NewProjectNavItem are project items
                while (nvSample.MenuItems.Count > headerIndex + 1)
                {
                    var next = nvSample.MenuItems[headerIndex + 1];
                    if (next == NewProjectNavItem) break;
                    nvSample.MenuItems.RemoveAt(headerIndex + 1);
                }

                // Recompute insertIndex in case it changed
                insertIndex = nvSample.MenuItems.IndexOf(NewProjectNavItem);
                if (insertIndex < 0) insertIndex = nvSample.MenuItems.Count;
            }

            foreach (var s in summaries)
            {
                var navItem = new NavigationViewItem
                {
                    Content = s.Name,
                    Icon = new SymbolIcon(Symbol.Library),
                    Tag = s.Id.ToString()
                };
                ToolTipService.SetToolTip(navItem, s.Location);
                nvSample.MenuItems.Insert(insertIndex++, navItem);
            }

            // Choose Home as initial page (unless caller requested no navigation)
            nvSample.SelectedItem = HomeNavItem;
            if (!suppressNavigation)
            {
                ContentFrame.Navigate(typeof(HomePage));
                this.Title = "Home - Sheet Music App";
            }
        }

        public void ApplyTheme(ElementTheme theme)
        {
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }
        }

        public void SetNavStyle(NavigationViewPaneDisplayMode mode)
        {
            nvSample.PaneDisplayMode = mode;
        }

        public NavigationViewPaneDisplayMode GetNavStyle()
        {
            return nvSample.PaneDisplayMode;
        }

        private void NvSample_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                // navigate to SettingsPage when the Settings item is invoked
                ContentFrame.Navigate(typeof(SettingsPage));
                this.Title = "Settings - Sheet Music App";
                return;
            }

            // handle other item invocations by Tag
            // revert visual selection to last committed item so pages can cancel navigation without leaving the UI selected
            try
            {
                if (_lastSelectedItem != null)
                {
                    nvSample.SelectedItem = _lastSelectedItem;
                }
            }
            catch { }

            if (args.InvokedItemContainer?.Tag is string tag)
            {
                if (tag == "Home")
                {
                    ContentFrame.Navigate(typeof(HomePage));
                    this.Title = "Home - Sheet Music App";
                    return;
                }

                if (tag == "NewProject")
                {
                    // If we're already on the NewProjectPage, do nothing to avoid resetting the form
                    if (ContentFrame.Content is NewProjectPage)
                    {
                        return;
                    }
                    ContentFrame.Navigate(typeof(NewProjectPage));
                    this.Title = "New Project - Sheet Music App";
                    return;
                }

                // If a project item was clicked, navigate to ProjectDetailsPage using the project id stored in Tag
                if (Guid.TryParse(tag, out var projectId))
                {
                    // If already viewing this project's details, do nothing
                    if (ContentFrame.Content is ProjectDetailsPage && _currentProjectId != null && _currentProjectId == tag)
                    {
                        return;
                    }

                    ContentFrame.Navigate(typeof(ProjectDetailsPage), tag);
                    // Optionally set the title to the project's name if we can resolve it later; use Id for now
                    this.Title = "Project - Sheet Music App";
                    return;
                }

                // fallback: navigate to Home
                ContentFrame.Navigate(typeof(HomePage));
                this.Title = "Home - Sheet Music App";
            }

            // no navigation for TreeView items (they are shown inside the Categories nav item)
        }

        // model and selector types moved to namespace scope so XAML can reference them as `local:Category` etc.

        private void NvSample_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            nvSample.IsBackEnabled = ContentFrame.CanGoBack;

            var sourcePageType = e.SourcePageType;
            if (sourcePageType == typeof(HomePage))
            {
                nvSample.SelectedItem = HomeNavItem;
                _lastSelectedItem = nvSample.SelectedItem;
                _currentProjectId = null;
            }
            else if (sourcePageType == typeof(NewProjectPage))
            {
                nvSample.SelectedItem = NewProjectNavItem;
                _lastSelectedItem = nvSample.SelectedItem;
                _currentProjectId = null;
            }
            else if (sourcePageType == typeof(ProjectDetailsPage))
            {
                // Try to select the nav item corresponding to the project id passed as parameter
                if (e.Parameter is string idStr)
                {
                    // Find the navigation view item with Tag == idStr
                    foreach (var mi in nvSample.MenuItems)
                    {
                        if (mi is NavigationViewItem nvi && nvi.Tag is string tag && tag == idStr)
                        {
                            nvSample.SelectedItem = nvi;
                            _lastSelectedItem = nvSample.SelectedItem;
                            _currentProjectId = idStr;
                            this.Title = (nvi.Content?.ToString() ?? "Project") + " - Sheet Music App";
                        return;
                        }
                    }
                }
            }
        }


    }

    // Namespace-level model and selector so XAML can resolve types like `local:Category` and use the selector as a resource.
    
    public class CategoryBase { }

    public class Category : CategoryBase
    {
        public string Name { get; set; } = string.Empty;
        public string Tooltip { get; set; } = string.Empty;
        public Symbol Glyph { get; set; }
    }

    public class Separator : CategoryBase { }

    public class Header : CategoryBase
    {
        public string Name { get; set; }
    }

    [ContentProperty(Name = "ItemTemplate")]
    public class MenuItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ItemTemplate { get; set; }
        public DataTemplate? SeparatorTemplate { get; set; }
        public DataTemplate? HeaderTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item)
        {
            return item is Separator ? SeparatorTemplate : item is Header ? HeaderTemplate : ItemTemplate;
        }
    }

}
