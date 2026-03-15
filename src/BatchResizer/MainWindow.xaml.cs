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

        Drop += OnWindowDrop;
        DragOver += OnWindowDragOver;
        Closing += (_, _) => _vm.SaveSettings();

        FolderDropBorder.DragEnter += (_, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                _vm.IsDragOver = true;
        };
        FolderDropBorder.DragLeave += (_, _) => _vm.IsDragOver = false;
        FolderDropBorder.Drop += (_, e) =>
        {
            _vm.IsDragOver = false;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
                _vm.HandleDroppedPaths(paths);
            e.Handled = true;
        };
        FolderDropBorder.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            _vm.HandleDroppedPaths(paths);
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
