using System.Windows.Controls;
using VibeVoice.ViewModels;

namespace VibeVoice.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
