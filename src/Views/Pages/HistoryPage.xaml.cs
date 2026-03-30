using System.Windows.Controls;
using VibeVoice.ViewModels;

namespace VibeVoice.Views.Pages;

public partial class HistoryPage : Page
{
    public HistoryPage(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
