using System.Windows.Controls;

namespace NetLanes.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void DataGridRow_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.Item is ViewModels.ApplicationRowViewModel app)
        {
            row.IsSelected = true;
            row.Focus();
            if (DataContext is ViewModels.DashboardViewModel vm)
            {
                vm.SelectedApplication = app;
            }
        }
    }

    private async void OnLaneMenuItemClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Core.Models.Lane lane)
        {
            if (DataContext is ViewModels.DashboardViewModel vm)
            {
                await vm.AssignToLaneAsync(lane);
            }
        }
    }

    private async void OnResetToUnlimitedClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.DashboardViewModel vm)
        {
            await vm.ResetToUnlimitedAsync(null);
        }
    }
}
