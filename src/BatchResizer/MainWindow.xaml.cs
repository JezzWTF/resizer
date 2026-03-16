using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BatchResizer.ViewModels;

namespace BatchResizer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Closing += (_, _) => _vm.SaveSettings();

        // handledEventsToo: true ensures these fire even if a child element marks the event handled
        RootGrid.AddHandler(UIElement.DragOverEvent, new DragEventHandler((_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }), handledEventsToo: true);

        RootGrid.AddHandler(UIElement.DropEvent, new DragEventHandler((_, e) =>
        {
            _vm.IsDragOver = false;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
                _vm.HandleDroppedPaths(paths);
            e.Handled = true;
        }), handledEventsToo: true);

        FolderDropBorder.DragEnter += (_, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                _vm.IsDragOver = true;
        };
        FolderDropBorder.DragLeave += (_, _) => _vm.IsDragOver = false;
    }

    private void OnRecentFoldersDropdownClick(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var menu = new ContextMenu
        {
            PlacementTarget = btn,
            Placement = PlacementMode.Bottom,
        };

        if (_vm.RecentFolders.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(no recent folders)", IsEnabled = false });
        }
        else
        {
            foreach (var path in _vm.RecentFolders)
            {
                var item = new MenuItem { Header = path };
                item.Click += (_, _) => _vm.AddRecentFolderCommand.Execute(path);
                menu.Items.Add(item);
            }
        }

        menu.IsOpen = true;
    }
}
