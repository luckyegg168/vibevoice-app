using System.Windows.Controls;
using VibeVoice.ViewModels;

namespace VibeVoice.Views.Pages;

public partial class DashboardPage : Page
{
    public DashboardPage(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
