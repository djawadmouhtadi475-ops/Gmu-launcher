using System.Windows;

namespace GmuLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        WindowEx.DarkTitle(this);
        InitializeComponent();
        Closed += (_, _) => { if (!App.Switching) Application.Current.Shutdown(); };
    }

    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool compact = ActualWidth < 980;
        if (DataContext is MainViewModel vm && vm.IsCompact != compact)
        {
            vm.IsCompact = compact;
            SideCol.Width = new GridLength(compact ? 76 : 230);
        }
    }
}
