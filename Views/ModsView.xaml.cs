using System.Windows;
using System.Windows.Controls;

namespace GmuLauncher.Views;

public partial class ModsView : UserControl
{
    public ModsView() { InitializeComponent(); }

    void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    async void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is ModsViewModel vm && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            await vm.AddFilesAsync(files);
    }
}
