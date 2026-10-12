using System.Reflection;
using System.Windows;

namespace GmuLauncher.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        WindowEx.DarkTitle(this);
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"{Loc.T("About_Version")} {v?.ToString(3)}";
    }
}
