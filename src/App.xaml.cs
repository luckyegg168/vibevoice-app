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
        _trayIconManager.ExitRequested += (_, _) => ExplicitShutdown();
        _trayIconManager.ChineseModeChanged += (_, mode) =>
        {
            var settings = _serviceProvider.GetRequiredService<SettingsService>();
            settings.Update(s => s.ChineseMode = mode);
        };

        var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
        if (!settingsService.Current.StartMinimized)
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
            _mainWindow.Closing += OnMainWindowClosing;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = _serviceProvider?.GetService<SettingsService>();
        if (settings?.Current.MinimizeToTray == true)
        {
            e.Cancel = true;
            _mainWindow!.Hide();
        }
        else
        {
            _mainWindow = null;
        }
    }

    private void ExplicitShutdown()
    {
        _trayIconManager?.Dispose();
        _serviceProvider?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
