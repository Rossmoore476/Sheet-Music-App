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
        public ObservableCollection<CategoryBase> Categories { get; } = new ObservableCollection<CategoryBase>();
        public static new MainWindow? Current { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Current = this;

            // populate navigation categories after InitializeComponent so UI bindings are available
            Categories.Add(new Category { Name = "Category 1", Glyph = Symbol.Home, Tooltip = "This is category 1" });
            Categories.Add(new Category { Name = "Category 2", Glyph = Symbol.Keyboard, Tooltip = "This is category 2" });
            Categories.Add(new Category { Name = "Category 3", Glyph = Symbol.Library, Tooltip = "This is category 3" });
            Categories.Add(new Category { Name = "Category 4", Glyph = Symbol.Mail, Tooltip = "This is category 4" });

            // If there are no projects, hide Home and the Projects header and don't add project items.
            if (Categories.Count == 0)
            {
                HomeNavItem.Visibility = Visibility.Collapsed;
                ProjectsHeader.Visibility = Visibility.Collapsed;
            }
            else
            {
                HomeNavItem.Visibility = Visibility.Visible;
                ProjectsHeader.Visibility = Visibility.Visible;
                // insert project items between the header and the New Project item
                int insertIndex = nvSample.MenuItems.IndexOf(NewProjectNavItem);
                if (insertIndex < 0)
                {
                    // fallback: append to the end
                    insertIndex = nvSample.MenuItems.Count;
                }

                foreach (var catBase in Categories)
                {
                    if (catBase is Category cat)
                    {
                        var navItem = new NavigationViewItem
                        {
                            Content = cat.Name,
                            Icon = new SymbolIcon(Symbol.Library),
                            Tag = cat.Name
                        };

                        ToolTipService.SetToolTip(navItem, cat.Tooltip);
                        nvSample.MenuItems.Insert(insertIndex++, navItem);
                    }
                }
            }

            // Wire up back handling and navigation tracking so the back button works between pages
            nvSample.BackRequested += NvSample_BackRequested;
            ContentFrame.Navigated += ContentFrame_Navigated;

            // Choose initial page: Home if there are projects, otherwise NewProject
            if (Categories.Count > 0)
            {
                nvSample.SelectedItem = HomeNavItem;
                ContentFrame.Navigate(typeof(HomePage));
                this.Title = "Home - Sheet Music App";
            }
            else
            {
                nvSample.SelectedItem = NewProjectNavItem;
                ContentFrame.Navigate(typeof(NewProjectPage));
                this.Title = "New Project - Sheet Music App";
            }
        }

        public void ApplyTheme(ElementTheme theme)
        {
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }
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
                    ContentFrame.Navigate(typeof(NewProjectPage));
                    this.Title = "New Project - Sheet Music App";
                    return;
                }

                // If a project item was clicked, navigate to project page (if implemented) or show Home
                ContentFrame.Navigate(typeof(HomePage));
                this.Title = tag + " - Sheet Music App";
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
            }
            else if (sourcePageType == typeof(NewProjectPage))
            {
                nvSample.SelectedItem = NewProjectNavItem;
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
