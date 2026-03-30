using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VibeVoice.Infrastructure;
using VibeVoice.Services;
using VibeVoice.ViewModels;
using VibeVoice.Views;

namespace VibeVoice;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private TrayIconManager? _trayIconManager;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Initialize tray icon
        _trayIconManager = _serviceProvider.GetRequiredService<TrayIconManager>();
        _trayIconManager.Initialize();
        _trayIconManager.ShowWindowRequested += (_, _) => ShowMainWindow();
        _trayIconManager.ExitRequested += (_, _) => Shutdown();
        _trayIconManager.ChineseModeChanged += (_, mode) =>
        {
            var settings = _serviceProvider.GetRequiredService<SettingsService>();
            settings.Update(s => s.ChineseMode = mode);
        };

        ShowMainWindow();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Debug);
            b.AddDebug();
        });

        // Services
        services.AddSingleton<SettingsService>();
        services.AddSingleton<AudioRecordingService>();
        services.AddSingleton<TranscriptionService>();
        services.AddSingleton<ChineseConversionService>();
        services.AddSingleton<TextInjectionService>();
        services.AddSingleton<SendQueueService>();
        services.AddSingleton<HistoryService>();

        // Infrastructure
        services.AddSingleton<GlobalHotkeyManager>();
        services.AddSingleton<TrayIconManager>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

