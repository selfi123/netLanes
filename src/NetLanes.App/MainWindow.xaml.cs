using System.Windows;
using NetLanes.App.ViewModels;

namespace NetLanes.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.DashboardVM.InitializeAsync();
    }
}