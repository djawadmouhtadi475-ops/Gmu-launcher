using System.Windows;

namespace GmuLauncher.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        WindowEx.DarkTitle(this);
        InitializeComponent();
        var vm = new LoginViewModel();
        vm.LoggedIn += () => DialogResult = true;
        DataContext = vm;
    }
}
