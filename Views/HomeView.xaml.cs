using System.Collections.Specialized;
using System.Windows.Controls;

namespace GmuLauncher.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is HomeViewModel vm)
                ((INotifyCollectionChanged)vm.Logs).CollectionChanged += (_, _) => LogScroll.ScrollToEnd();   // تمرير تلقائي لآخر سطر
        };
    }
}
