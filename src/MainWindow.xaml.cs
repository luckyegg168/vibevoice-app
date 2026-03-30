using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using VibeVoice.ViewModels;
using VibeVoice.Views.Pages;

namespace VibeVoice.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm;
    private DashboardPage? _dashboardPage;
    private HistoryPage? _historyPage;
    private SettingsPage? _settingsPage;

    public MainWindow(MainViewModel vm, SettingsViewModel settingsVm)
    {
        _vm = vm;
        InitializeComponent();

        // Pre-create pages
        _dashboardPage = new DashboardPage(vm);
        _historyPage = new HistoryPage(vm);
        _settingsPage = new SettingsPage(settingsVm);

        // Navigate to dashboard on startup
        ContentFrame.Navigate(_dashboardPage);
    }

    private void NavDashboard_Click(object sender, MouseButtonEventArgs e)
        => ContentFrame.Navigate(_dashboardPage ??= new DashboardPage(_vm));

    private void NavHistory_Click(object sender, MouseButtonEventArgs e)
        => ContentFrame.Navigate(_historyPage ??= new HistoryPage(_vm));

    private void NavSettings_Click(object sender, MouseButtonEventArgs e)
        => ContentFrame.Navigate(_settingsPage);
}
