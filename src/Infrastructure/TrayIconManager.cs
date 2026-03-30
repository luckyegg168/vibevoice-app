using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Logging;
using VibeVoice.Models;
using VibeVoice.Services;

namespace VibeVoice.Infrastructure;

/// <summary>
/// Manages the system tray icon and context menu.
/// </summary>
public class TrayIconManager : IDisposable
{
    private TaskbarIcon? _taskbarIcon;
    private readonly ILogger<TrayIconManager> _logger;
    private readonly SettingsService _settingsService;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<ChineseMode>? ChineseModeChanged;

    public TrayIconManager(ILogger<TrayIconManager> logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public void Initialize()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = "VibeVoice — 語音輸入助手",
                ContextMenu = BuildContextMenu()
            };

            // Use default WPF icon or a generated one
            SetIcon();

            _taskbarIcon.TrayMouseDoubleClick += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    private void SetIcon()
    {
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _taskbarIcon!.Icon = new Icon(iconPath);
            }
            else
            {
                // Create a simple programmatic icon
                _taskbarIcon!.Icon = SystemIcons.Application;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load tray icon");
            _taskbarIcon!.Icon = SystemIcons.Application;
        }
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "開啟主視窗 (_O)" };
        showItem.Click += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

        var sep1 = new System.Windows.Controls.Separator();

        var traditionalItem = new System.Windows.Controls.MenuItem
        {
            Header = "切換至繁體中文 (_T)",
            IsCheckable = true,
            IsChecked = _settingsService.Current.ChineseMode == ChineseMode.Traditional
        };
        traditionalItem.Click += (_, _) =>
        {
            ChineseModeChanged?.Invoke(this, ChineseMode.Traditional);
            RefreshMenu(menu);
        };

        var simplifiedItem = new System.Windows.Controls.MenuItem
        {
            Header = "切換至簡體中文 (_S)",
            IsCheckable = true,
            IsChecked = _settingsService.Current.ChineseMode == ChineseMode.Simplified
        };
        simplifiedItem.Click += (_, _) =>
        {
            ChineseModeChanged?.Invoke(this, ChineseMode.Simplified);
            RefreshMenu(menu);
        };

        var sep2 = new System.Windows.Controls.Separator();

        var exitItem = new System.Windows.Controls.MenuItem { Header = "結束 (_X)" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(showItem);
        menu.Items.Add(sep1);
        menu.Items.Add(traditionalItem);
        menu.Items.Add(simplifiedItem);
        menu.Items.Add(sep2);
        menu.Items.Add(exitItem);

        return menu;
    }

    private void RefreshMenu(System.Windows.Controls.ContextMenu menu)
    {
        // Rebuild to reflect state changes
        _taskbarIcon!.ContextMenu = BuildContextMenu();
    }

    public void ShowBalloon(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _taskbarIcon?.ShowBalloonTip(title, message, icon);
        });
    }

    public void Dispose()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _taskbarIcon?.Dispose();
            _taskbarIcon = null;
        });
        GC.SuppressFinalize(this);
    }
}
